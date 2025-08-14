<template>
  <div class="h-screen flex flex-col bg-gray-50">
    <!-- Header/Menu Bar matching legacy eStacker -->
    <header class="bg-white border-b border-gray-200 px-4 py-2 flex items-center justify-between">
      <div class="flex items-center space-x-4">
        <h1 class="text-lg font-semibold text-gray-900">
          Hydra Due Diligence - eStacker Web
        </h1>
        
        <!-- Enhanced Breadcrumb showing current selection -->
        <nav class="flex items-center space-x-2 text-sm text-gray-500" v-if="mainStore.selectionPath.length > 0">
          <span>•</span>
          <template v-for="(item, index) in mainStore.selectionPath" :key="index">
            <button
              v-if="item.type === 'loan' && mainStore.selectedDocument"
              @click="navigateToLoanLevel"
              class="text-hydra-600 hover:text-hydra-700 hover:underline cursor-pointer"
              title="Back to Loan Selection"
            >
              {{ item.name }}
            </button>
            <span v-else class="text-gray-700">{{ item.name }}</span>
            <span v-if="index < mainStore.selectionPath.length - 1" class="text-gray-400">→</span>
          </template>
          <!-- Document indication -->
          <template v-if="mainStore.selectedDocument">
            <span class="text-gray-400">→</span>
            <span class="text-gray-700 max-w-xs truncate">
              {{ mainStore.selectedDocument.originalName }}
            </span>
            <span class="text-xs text-gray-400 bg-gray-100 px-2 py-1 rounded">
              {{ mainStore.selectedDocument.documentType || 'Unclassified' }}
            </span>
          </template>
        </nav>
      </div>
      
      <div class="flex items-center space-x-4">
        <!-- User info -->
        <div class="text-sm text-gray-600" v-if="mainStore.currentUser">
          Logged in: {{ mainStore.currentUser.name }}
        </div>
      </div>
    </header>

    <!-- Main Content Area - Four Panel Layout (Legacy Style) -->
    <div class="flex-1 flex overflow-hidden">
        <!-- Left Panel - Document Grid and Actions -->
        <div 
          class="bg-white border-r border-gray-200 flex flex-col"
          :style="{ width: `${mainStore.panelSizes.left}px` }"
        >
          <LeftPanel />
        </div>

        <!-- Resize Handle for Left Panel -->
        <div 
          class="resize-handle"
          @mousedown="startResize('left', $event)"
        ></div>

        <!-- Center Panel - PDF Viewer (now without vertical toolbar integrated) -->
        <div 
          class="bg-gray-800 flex flex-col flex-1"
          :style="{ width: `${mainStore.panelSizes.center}px` }"
        >
          <!-- PDF viewer only, no toolbar -->
          <div class="h-full flex flex-col bg-gray-800">
            <!-- Document Viewer Toolbar -->
            <div class="bg-white border-b border-gray-200 px-3 py-2 flex items-center justify-between">
              <!-- Left side - Document info -->
              <div class="flex items-center space-x-3 text-sm">
                <div v-if="mainStore.selectedDocument" class="flex items-center space-x-2">
                  <DocumentIcon class="h-4 w-4 text-gray-500" />
                  <span class="font-medium text-gray-900">
                    {{ mainStore.selectedDocument.originalName }}
                  </span>
                  <span class="text-gray-500">•</span>
                  <span class="text-gray-600">
                    {{ mainStore.selectedDocument.pageCount || 0 }} pages
                  </span>
                </div>
                <div v-else class="text-gray-500">
                  No document selected
                </div>
              </div>

              <!-- Right side - Simplified controls -->
              <div class="flex items-center space-x-2">
                <!-- Document Status -->
                <div class="text-xs text-gray-600">
                  <span v-if="mainStore.selectedDocument">
                    {{ mainStore.selectedDocument.documentType || 'Unclassified' }}
                  </span>
                </div>
              </div>
            </div>

            <!-- Main PDF Display -->
            <div class="flex-1 flex bg-gray-700 relative">
              <!-- PDF Display Area -->
              <div class="flex-1 flex flex-col bg-gray-700 relative">
                <!-- Loading State -->
                <div 
                  v-if="mainStore.loading.documentContent" 
                  class="absolute inset-0 flex items-center justify-center bg-gray-800 bg-opacity-75 z-10"
                >
                  <div class="text-center text-white">
                    <div class="spinner mb-4 border-white border-t-transparent"></div>
                    <p class="text-sm">Loading document...</p>
                  </div>
                </div>

                <!-- No Document State -->
                <div 
                  v-else-if="!mainStore.selectedDocument" 
                  class="flex-1 flex items-center justify-center"
                >
                  <div class="text-center text-gray-400">
                    <DocumentIcon class="h-16 w-16 mx-auto mb-4 opacity-50" />
                    <p class="text-lg font-medium">No Document Selected</p>
                    <p class="text-sm mt-2">Select a document from the left panel to view it here</p>
                  </div>
                </div>

                <!-- Document Display -->
                <div v-else class="flex-1 flex flex-col">
                  <!-- PDF Viewer Component -->
                  <div class="flex-1 relative">
                    <PDFViewer
                      ref="pdfViewerRef"
                      :document-url="mainStore.documentUrl || undefined"
                      :page-number="mainStore.currentPage"
                      :zoom-level="mainStore.zoomLevel"
                      @loaded="handleDocumentLoaded"
                      @error="handleDocumentError"
                      @page-rendered="handlePageRendered"
                      @previous-page="handlePreviousPage"
                      @next-page="handleNextPage"
                      @go-to-page="handleGoToPage"
                    />
                  </div>

                  <!-- Page Status Bar -->
                  <div class="bg-gray-900 px-4 py-1 text-white text-center">
                    <span class="text-xs">
                      Page {{ mainStore.currentPage }} of {{ mainStore.totalPages }} • {{ mainStore.zoomLevel }}%
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Status/Info Bar -->
            <div v-if="mainStore.selectedDocument" class="bg-white border-t border-gray-200 px-4 py-2 text-xs text-gray-600">
              <div class="flex items-center justify-between">
                <span>{{ mainStore.selectedDocument.originalName }}</span>
                <span>{{ mainStore.selectedDocument.pageCount || 0 }} pages • {{ (mainStore.selectedDocument.length / 1024).toFixed(1) }} KB</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Vertical PDF Toolbar (comes AFTER PDF viewer, BEFORE thumbnails) -->
        <VerticalPdfToolbar 
          v-if="mainStore.selectedDocument"
          :selected-document="mainStore.selectedDocument"
          :current-page="mainStore.currentPage"
          :total-pages="mainStore.totalPages"
          @generic-break-added="handleGenericBreakAdded"
          @generic-break-removed="handleGenericBreakRemoved"
          @page-changed="handlePageChanged"
          @zoom-changed="handleZoomChanged"
          @document-rotated="handleDocumentRotated"
          @redaction-started="handleRedactionStarted"
          @document-split="handleDocumentSplit"
          @document-stacked="handleDocumentStacked"
        />

        <!-- Thumbnail Panel (comes AFTER toolbar) - using original ThumbnailView -->
        <div 
          v-if="mainStore.selectedDocument"
          class="w-80 bg-white border-l border-gray-300 flex-shrink-0"
        >
          <ThumbnailView
            :selected-document="mainStore.selectedDocument"
            :current-page="mainStore.currentPage"
            :total-pages="mainStore.totalPages"
            :bookmarks="indexingStore.pendingBookmarks"
            :thumbnails="thumbnails"
            :loading="thumbnailsLoading"
            @page-selected="handlePageSelected"
            @thumbnail-loaded="handleThumbnailLoaded"
          />
        </div>

        <!-- Resize Handle for Right Panel -->
        <div 
          class="resize-handle"
          @mousedown="startResize('right', $event)"
        ></div>

        <!-- Right Panel - Selection and Indexing -->
        <div 
          class="bg-white border-l border-gray-200 flex flex-col"
          :style="{ width: `${mainStore.panelSizes.right}px` }"
        >
          <RightPanel />
        </div>
      </div>

    <!-- Status Bar -->
    <footer class="bg-gray-100 border-t border-gray-200 px-4 py-1 text-xs text-gray-600 flex items-center justify-between">
      <div class="flex items-center space-x-4">
        <span v-if="mainStore.selectedLoan">
          Loan: {{ mainStore.selectedLoan.assetName }} ({{ mainStore.selectedLoan.assetNo }})
        </span>
        <span v-if="mainStore.selectedDocument && mainStore.totalPages > 0">
          Page {{ mainStore.currentPage }} of {{ mainStore.totalPages }}
        </span>
      </div>
      
      <div class="flex items-center space-x-4">
        <span v-if="mainStore.selectedDocument">
          {{ mainStore.selectedDocument.originalName }}
        </span>
        <span v-if="mainStore.isLoading" class="text-hydra-600">
          Loading...
        </span>
        <span>Ready</span>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { useMainStore } from '@/stores/main'
import { useIndexingStore } from '@/stores/indexing'
import LeftPanel from './LeftPanel.vue'
import ThumbnailView from '@/components/indexing/ThumbnailView.vue'
import CenterPanel from './CenterPanel.vue'
import RightPanel from './RightPanel.vue'
import VerticalPdfToolbar from '@/components/tools/VerticalPdfToolbar.vue'
import PDFViewer from '@/components/viewer/PDFViewer.vue'
import type { BookmarkDto, PageThumbnailDto } from '@/types/indexing'
import { pdfService } from '@/services/pdf.service'
import { DocumentIcon } from '@heroicons/vue/24/outline'

const mainStore = useMainStore()
const indexingStore = useIndexingStore()

// Thumbnail management (moved from CenterPanel for better data flow)
const thumbnails = ref<PageThumbnailDto[]>([])
const thumbnailsLoading = ref(false)

// Component refs
const pdfViewerRef = ref<InstanceType<typeof PDFViewer>>()

// Navigation functions
const navigateToLoanLevel = () => {
  // Clear selected document to return to loan list view
  mainStore.selectedDocument = null
  mainStore.selectedDocumentDetails = null
  mainStore.documentUrl = null
  mainStore.currentPage = 1
  mainStore.totalPages = 0
}

// Panel resizing functionality
const isResizing = ref(false)
const resizePanel = ref<'left' | 'right' | null>(null)
const initialMouseX = ref(0)
const initialPanelSize = ref(0)

const startResize = (panel: 'left' | 'right', event: MouseEvent) => {
  isResizing.value = true
  resizePanel.value = panel
  initialMouseX.value = event.clientX
  
  if (panel === 'left') {
    initialPanelSize.value = mainStore.panelSizes.left
  } else if (panel === 'right') {
    initialPanelSize.value = mainStore.panelSizes.right
  }

  document.addEventListener('mousemove', handleResize)
  document.addEventListener('mouseup', stopResize)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}

const handleResize = (event: MouseEvent) => {
  if (!isResizing.value || !resizePanel.value) return

  const deltaX = event.clientX - initialMouseX.value
  const newSize = initialPanelSize.value + (resizePanel.value === 'left' ? deltaX : -deltaX)
  
  // Set minimum and maximum panel sizes
  const minSize = 200
  const maxSize = window.innerWidth * 0.4
  
  if (newSize >= minSize && newSize <= maxSize) {
    if (resizePanel.value === 'left') {
      mainStore.setPanelSizes({ left: newSize })
    } else {
      mainStore.setPanelSizes({ right: newSize })
    }
  }
}

const stopResize = () => {
  isResizing.value = false
  resizePanel.value = null
  document.removeEventListener('mousemove', handleResize)
  document.removeEventListener('mouseup', stopResize)
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
}


// PDF viewer event handlers
const handleDocumentLoaded = async (pageCount: number) => {
  mainStore.totalPages = pageCount
  console.log(`Document loaded with ${pageCount} pages`)
  
  // Generate thumbnails after document loads
  await generateThumbnails()
}

const handleDocumentError = (error: string) => {
  mainStore.setError(`Failed to load document: ${error}`)
}

const handlePageRendered = (pageNumber: number) => {
  console.log(`Page ${pageNumber} rendered successfully`)
}

const handlePreviousPage = () => {
  mainStore.previousPage()
}

const handleNextPage = () => {
  mainStore.nextPage()
}

const handleGoToPage = (page: number) => {
  if (page === -1) {
    mainStore.goToPage(mainStore.totalPages)
  } else {
    mainStore.goToPage(page)
  }
}

// Toolbar event handlers
const handleGenericBreakAdded = (pageIndex: number) => {
  console.log(`Generic break added at page ${pageIndex + 1}`)
}

const handleGenericBreakRemoved = (pageIndex: number) => {
  console.log(`Generic break removed from page ${pageIndex + 1}`)
}

const handlePageChanged = (page: number) => {
  mainStore.currentPage = page
}

const handleZoomChanged = (zoomLevel: number) => {
  mainStore.setZoomLevel(zoomLevel)
}

const handleDocumentRotated = (documentId: number, angle: number) => {
  console.log(`Document ${documentId} rotated ${angle} degrees`)
}

const handleRedactionStarted = (documentId: number) => {
  console.log(`Redaction started for document ${documentId}`)
}

const handleDocumentSplit = (documentId: number) => {
  console.log(`Document split requested for document ${documentId}`)
}

const handleDocumentStacked = (documentId: number) => {
  console.log(`Document stack requested for document ${documentId}`)
}

// Thumbnail generation
const generateThumbnails = async () => {
  console.log('generateThumbnails called:', { documentUrl: !!mainStore.documentUrl, totalPages: mainStore.totalPages })
  if (!mainStore.documentUrl || !mainStore.totalPages) {
    console.log('Skipping thumbnail generation: missing url or pages')
    return
  }
  
  thumbnailsLoading.value = true
  try {
    console.log('Loading PDF document for thumbnails...')
    const pdf = await pdfService.loadDocument(mainStore.documentUrl)
    console.log('PDF loaded, generating thumbnails...')
    
    const thumbnailUrls = await pdfService.generateAllThumbnails(pdf, {
      scale: 0.4,
      maxWidth: 120,
      maxHeight: 102,
      quality: 0.8
    })
    
    console.log(`Generated ${thumbnailUrls.length} thumbnail URLs`)
    
    const thumbnailDtos: PageThumbnailDto[] = thumbnailUrls.map((url, index) => ({
      pageNumber: index + 1,
      thumbnailUrl: url,
      width: 120,
      height: 102,
      hasBookmark: false,
      bookmarkType: undefined,
      documentTypeName: undefined
    }))
    
    thumbnails.value = thumbnailDtos
    console.log('Thumbnails set:', thumbnails.value.length)
  } catch (error) {
    console.error('Failed to generate thumbnails:', error)
    thumbnails.value = []
  } finally {
    thumbnailsLoading.value = false
  }
}

// Thumbnail event handlers
const handleThumbnailsGenerated = (newThumbnails: PageThumbnailDto[]) => {
  thumbnails.value = newThumbnails
}

const handlePageSelected = (pageNumber: number) => {
  mainStore.currentPage = pageNumber
}

const handleThumbnailLoaded = (pageNumber: number) => {
  console.log('Thumbnail loaded for page:', pageNumber)
}

// Watch for document changes to regenerate thumbnails
watch([() => mainStore.selectedDocument, () => mainStore.documentUrl, () => mainStore.totalPages], async ([newDoc, newUrl, totalPages]) => {
  console.log('Thumbnail watch triggered:', { newDoc: !!newDoc, newUrl: !!newUrl, totalPages })
  if (newDoc && newUrl && totalPages > 0) {
    thumbnails.value = []
    console.log('Generating thumbnails for document:', newDoc.originalName)
    await generateThumbnails()
  } else {
    thumbnails.value = []
  }
})

// Cleanup event listeners on unmount
onUnmounted(() => {
  document.removeEventListener('mousemove', handleResize)
  document.removeEventListener('mouseup', stopResize)
})

// Load user offerings on mount
onMounted(() => {
  mainStore.loadUserOfferings()
})
</script>

<style scoped>
/* Prevent text selection during resize */
.resize-handle:active {
  user-select: none;
}
</style>
