import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  DocumentTypeDto,
  BookmarkDto,
  ProcessingResultDto,
  ProcessingSessionDto,
  CreateBookmarkRequest,
  UpdateBookmarkRequest,
  ProcessBookmarksRequest,
  ValidationResult,
  SaveProcessingType,
  SaveProgress,
  SaveError,
  IndexingChangeSummary,
  DocumentMetadata
} from '@/types/indexing'
import type { DocumentSummary, Offering } from '@/types/domain'
import { indexingApi } from '@/services/indexing-api'

export const useIndexingStore = defineStore('indexing', () => {
  // === STATE ===
  
  // Current workflow state
  const indexingMode = ref(false)
  const selectedDocumentType = ref<DocumentTypeDto | null>(null)
  
  // Document types (filtered by offering)
  const availableDocumentTypes = ref<DocumentTypeDto[]>([])
  const documentTypesLoading = ref(false)
  
  // Bookmarks management
  const pendingBookmarks = ref<BookmarkDto[]>([])
  const bookmarksLoading = ref(false)
  const selectedBookmarkId = ref<number | null>(null)
  
  // Processing results
  const processingResults = ref<ProcessingResultDto[]>([])
  const processingSession = ref<ProcessingSessionDto | null>(null)
  
  // UI state
  const currentBookmarkPage = ref<number | null>(null)
  const showBookmarksList = ref(false)
  const showProcessingResults = ref(false)
  const saving = ref(false)
  const saveProgress = ref<SaveProgress | null>(null)
  
  // Error state
  const lastError = ref<SaveError | null>(null)

  // === COMPUTED ===
  
  const hasUnsavedChanges = computed(() => {
    return pendingBookmarks.value.length > 0 || selectedDocumentType.value !== null
  })
  
  const canSave = computed(() => {
    return !saving.value && (pendingBookmarks.value.length > 0 || selectedDocumentType.value !== null)
  })
  
  const changeSummary = computed((): IndexingChangeSummary => ({
    pendingBookmarks: pendingBookmarks.value.length,
    selectedDocumentType: selectedDocumentType.value?.name || null,
    hasUnsavedChanges: hasUnsavedChanges.value,
    canSave: canSave.value
  }))
  
  const bookmarksByPage = computed(() => {
    const map = new Map<number, BookmarkDto>()
    pendingBookmarks.value.forEach(bookmark => {
      map.set(bookmark.pageIndex, bookmark)
    })
    return map
  })

  // === ACTIONS ===
  
  // Initialize indexing mode
  const enableIndexingMode = () => {
    indexingMode.value = true
    showBookmarksList.value = false
  }
  
  const disableIndexingMode = () => {
    indexingMode.value = false
    clearWorkingState()
  }
  
  // Document types management
  const loadDocumentTypesForOffering = async (offeringId: number): Promise<void> => {
    documentTypesLoading.value = true
    lastError.value = null
    
    try {
      const types = await indexingApi.getDocumentTypes(offeringId)
      availableDocumentTypes.value = types
    } catch (error) {
      lastError.value = {
        type: 'database',
        code: 'LOAD_DOC_TYPES_FAILED',
        message: `Failed to load document types: ${error}`,
        recoverable: true,
        recovery_action: 'Retry loading document types'
      }
      throw error
    } finally {
      documentTypesLoading.value = false
    }
  }
  
  const selectDocumentType = (docType: DocumentTypeDto) => {
    selectedDocumentType.value = docType
  }
  
  const clearDocumentTypeSelection = () => {
    selectedDocumentType.value = null
  }
  
  // Bookmark management
  const loadBookmarksForDocument = async (documentId: number): Promise<void> => {
    bookmarksLoading.value = true
    lastError.value = null
    
    try {
      const bookmarks = await indexingApi.getBookmarks(documentId)
      pendingBookmarks.value = bookmarks
    } catch (error) {
      lastError.value = {
        type: 'database',
        code: 'LOAD_BOOKMARKS_FAILED',
        message: `Failed to load bookmarks: ${error}`,
        recoverable: true,
        recovery_action: 'Retry loading bookmarks'
      }
      throw error
    } finally {
      bookmarksLoading.value = false
    }
  }
  
  const createBookmark = async (documentId: number, pageIndex: number): Promise<void> => {
    if (!selectedDocumentType.value) {
      throw new Error('No document type selected')
    }
    
    const request: CreateBookmarkRequest = {
      imageId: documentId,
      pageIndex,
      documentTypeId: selectedDocumentType.value.id,
      documentTypeName: selectedDocumentType.value.name,
      documentDate: new Date(),
      comments: ''
    }
    
    try {
      const bookmark = await indexingApi.createBookmark(documentId, request)
      pendingBookmarks.value.push(bookmark)
      
      // Clear selection after creating bookmark (legacy behavior)
      selectedDocumentType.value = null
      
      // Update UI state
      currentBookmarkPage.value = pageIndex
    } catch (error) {
      lastError.value = {
        type: 'database',
        code: 'CREATE_BOOKMARK_FAILED',
        message: `Failed to create bookmark: ${error}`,
        recoverable: true,
        recovery_action: 'Try creating the bookmark again'
      }
      throw error
    }
  }
  
  const updateBookmark = async (bookmarkId: number, updates: UpdateBookmarkRequest): Promise<void> => {
    const bookmarkIndex = pendingBookmarks.value.findIndex(b => b.id === bookmarkId)
    if (bookmarkIndex === -1) return
    
    try {
      const updatedBookmark = await indexingApi.updateBookmark(
        pendingBookmarks.value[bookmarkIndex].imageId,
        bookmarkId,
        updates
      )
      
      // Update local state
      pendingBookmarks.value[bookmarkIndex] = updatedBookmark
    } catch (error) {
      lastError.value = {
        type: 'database',
        code: 'UPDATE_BOOKMARK_FAILED',
        message: `Failed to update bookmark: ${error}`,
        recoverable: true,
        recovery_action: 'Try updating the bookmark again'
      }
      throw error
    }
  }
  
  const deleteBookmark = async (bookmarkId: number): Promise<void> => {
    const bookmark = pendingBookmarks.value.find(b => b.id === bookmarkId)
    if (!bookmark) return
    
    try {
      await indexingApi.deleteBookmark(bookmark.imageId, bookmarkId)
      
      // Remove from local state
      pendingBookmarks.value = pendingBookmarks.value.filter(b => b.id !== bookmarkId)
      
      // Clear selection if this was selected
      if (selectedBookmarkId.value === bookmarkId) {
        selectedBookmarkId.value = null
      }
    } catch (error) {
      lastError.value = {
        type: 'database',
        code: 'DELETE_BOOKMARK_FAILED',
        message: `Failed to delete bookmark: ${error}`,
        recoverable: true,
        recovery_action: 'Try deleting the bookmark again'
      }
      throw error
    }
  }
  
  const clearAllBookmarks = async (): Promise<void> => {
    const bookmarkIds = pendingBookmarks.value.map(b => b.id)
    
    try {
      for (const bookmarkId of bookmarkIds) {
        const bookmark = pendingBookmarks.value.find(b => b.id === bookmarkId)
        if (bookmark) {
          await indexingApi.deleteBookmark(bookmark.imageId, bookmarkId)
        }
      }
      
      // Clear local state
      pendingBookmarks.value = []
      selectedBookmarkId.value = null
      currentBookmarkPage.value = null
    } catch (error) {
      lastError.value = {
        type: 'database',
        code: 'CLEAR_BOOKMARKS_FAILED',
        message: `Failed to clear bookmarks: ${error}`,
        recoverable: true,
        recovery_action: 'Try clearing bookmarks again'
      }
      throw error
    }
  }
  
  const selectBookmark = (bookmarkId: number) => {
    selectedBookmarkId.value = bookmarkId
    const bookmark = pendingBookmarks.value.find(b => b.id === bookmarkId)
    if (bookmark) {
      currentBookmarkPage.value = bookmark.pageIndex
    }
  }
  
  // Save processing
  const validateSaveProcess = (): ValidationResult => {
    const errors: string[] = []
    const warnings: string[] = []
    
    // Check for invalid bookmarks
    const invalidBookmarks = pendingBookmarks.value.filter(b => !b.imageDocumentTypeId)
    if (invalidBookmarks.length > 0) {
      errors.push(`${invalidBookmarks.length} bookmarks missing document type`)
    }
    
    // Check for overlapping page ranges
    const sortedBookmarks = [...pendingBookmarks.value].sort((a, b) => a.pageIndex - b.pageIndex)
    for (let i = 1; i < sortedBookmarks.length; i++) {
      if (sortedBookmarks[i].pageIndex === sortedBookmarks[i-1].pageIndex) {
        errors.push(`Multiple bookmarks on page ${sortedBookmarks[i].pageIndex + 1}`)
      }
    }
    
    return { valid: errors.length === 0, errors, warnings }
  }
  
  const getProcessingType = (): SaveProcessingType => {
    const bookmarkCount = pendingBookmarks.value.length
    
    if (bookmarkCount === 0) {
      return 'simple'
    } else if (bookmarkCount === 1 && pendingBookmarks.value[0].pageIndex === 0) {
      return 'index_only'
    } else {
      return 'document_splitting'
    }
  }
  
  const executeSaveProcess = async (documentId: number): Promise<void> => {
    if (saving.value) return
    
    try {
      // 1. Disable UI and show progress
      saving.value = true
      saveProgress.value = { current: 0, total: 4, message: 'Validating document...' }
      
      // 2. Validate before save
      const validation = validateSaveProcess()
      if (!validation.valid) {
        throw new Error(validation.errors.join(', '))
      }
      
      saveProgress.value = { current: 1, total: 4, message: 'Processing bookmarks...' }
      
      // 3. Determine processing type
      const processingType = getProcessingType()
      
      // 4. Execute appropriate save workflow
      let result: ProcessingSessionDto
      
      switch (processingType) {
        case 'simple':
          result = await saveImageDataOnly(documentId)
          break
        case 'index_only':
          result = await saveWithIndexOnly(documentId)
          break
        case 'document_splitting':
          result = await processDocumentSplitting(documentId)
          break
      }
      
      saveProgress.value = { current: 3, total: 4, message: 'Updating interface...' }
      
      // 5. Update UI with results
      processingSession.value = result
      await refreshProcessingResults(documentId)
      clearWorkingState()
      
      saveProgress.value = { current: 4, total: 4, message: 'Save completed successfully' }
      
    } catch (error) {
      lastError.value = {
        type: 'processing',
        code: 'SAVE_FAILED',
        message: `Save process failed: ${error}`,
        recoverable: true,
        recovery_action: 'Try saving again'
      }
      throw error
    } finally {
      saving.value = false
      saveProgress.value = null
    }
  }
  
  const saveImageDataOnly = async (documentId: number): Promise<ProcessingSessionDto> => {
    const metadata: DocumentMetadata = {
      documentTypeId: selectedDocumentType.value?.id,
      documentDate: new Date(),
      comments: ''
    }
    
    return await indexingApi.saveImageData(documentId, metadata)
  }
  
  const saveWithIndexOnly = async (documentId: number): Promise<ProcessingSessionDto> => {
    const request: ProcessBookmarksRequest = {
      bookmarks: pendingBookmarks.value,
      processingMode: 'rename'
    }
    
    return await indexingApi.processBookmarks(documentId, request)
  }
  
  const processDocumentSplitting = async (documentId: number): Promise<ProcessingSessionDto> => {
    const request: ProcessBookmarksRequest = {
      bookmarks: pendingBookmarks.value,
      processingMode: 'split'
    }
    
    return await indexingApi.processBookmarks(documentId, request)
  }
  
  // Processing results management
  const refreshProcessingResults = async (documentId: number): Promise<void> => {
    try {
      const results = await indexingApi.getProcessingResults(documentId)
      processingResults.value = results
      showProcessingResults.value = results.length > 0
    } catch (error) {
      console.warn('Failed to refresh processing results:', error)
    }
  }
  
  const pollProcessingStatus = async (sessionId: string): Promise<void> => {
    const maxAttempts = 30 // 5 minutes max
    let attempts = 0
    
    const poll = async () => {
      try {
        const session = await indexingApi.getProcessingStatus(sessionId)
        processingSession.value = session
        
        if (session.status === 'completed' || session.status === 'error') {
          return // Polling complete
        }
        
        if (attempts < maxAttempts) {
          attempts++
          setTimeout(poll, 10000) // Poll every 10 seconds
        }
      } catch (error) {
        console.warn('Failed to poll processing status:', error)
      }
    }
    
    await poll()
  }
  
  // Utility functions
  const clearWorkingState = () => {
    selectedDocumentType.value = null
    selectedBookmarkId.value = null
    currentBookmarkPage.value = null
    lastError.value = null
  }
  
  const clearError = () => {
    lastError.value = null
  }
  
  const resetStore = () => {
    indexingMode.value = false
    selectedDocumentType.value = null
    availableDocumentTypes.value = []
    documentTypesLoading.value = false
    pendingBookmarks.value = []
    bookmarksLoading.value = false
    selectedBookmarkId.value = null
    processingResults.value = []
    processingSession.value = null
    currentBookmarkPage.value = null
    showBookmarksList.value = false
    showProcessingResults.value = false
    saving.value = false
    saveProgress.value = null
    lastError.value = null
  }

  // === RETURN ===
  return {
    // State
    indexingMode,
    selectedDocumentType,
    availableDocumentTypes,
    documentTypesLoading,
    pendingBookmarks,
    bookmarksLoading,
    selectedBookmarkId,
    processingResults,
    processingSession,
    currentBookmarkPage,
    showBookmarksList,
    showProcessingResults,
    saving,
    saveProgress,
    lastError,
    
    // Computed
    hasUnsavedChanges,
    canSave,
    changeSummary,
    bookmarksByPage,
    
    // Actions
    enableIndexingMode,
    disableIndexingMode,
    loadDocumentTypesForOffering,
    selectDocumentType,
    clearDocumentTypeSelection,
    loadBookmarksForDocument,
    createBookmark,
    updateBookmark,
    deleteBookmark,
    clearAllBookmarks,
    selectBookmark,
    validateSaveProcess,
    getProcessingType,
    executeSaveProcess,
    refreshProcessingResults,
    pollProcessingStatus,
    clearWorkingState,
    clearError,
    resetStore
  }
})
