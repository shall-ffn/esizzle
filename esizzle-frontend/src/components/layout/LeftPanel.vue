<template>
  <div class="h-full flex flex-col">
    <!-- Document Search and Filters -->
    <div class="p-3 border-b border-gray-200 bg-gray-50">
      <div class="space-y-2">
        <!-- Search Images input matching legacy -->
        <div>
          <input
            type="text"
            placeholder="Search Images"
            class="w-full px-3 py-1 text-sm border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
            v-model="imageSearchTerm"
            @input="handleImageSearch"
          />
        </div>
        
        <!-- Apply Filters section -->
        <div class="flex items-center justify-between">
          <span class="text-xs font-medium text-gray-700">Apply Filters</span>
          <button class="text-xs text-hydra-600 hover:text-hydra-700">
            Clear
          </button>
        </div>
      </div>
    </div>

    <!-- Document Grid -->
    <div class="flex-1 overflow-hidden flex flex-col">
      <!-- Grid Header -->
      <div class="document-grid-header grid grid-cols-6 gap-2 text-xs">
        <div class="col-span-2">Doc Type</div>
        <div class="text-center">Pgs</div>
        <div class="text-center">CST</div>
        <div class="text-center">Status</div>
        <div class="text-center">CD</div>
      </div>

      <!-- Grid Body -->
      <div class="flex-1 overflow-y-auto">
        <div v-if="mainStore.loading.documents" class="p-4 text-center">
          <div class="spinner mx-auto mb-2"></div>
          <p class="text-sm text-gray-600">Loading documents...</p>
        </div>

        <div v-else-if="filteredDocuments.length === 0" class="p-4 text-center text-sm text-gray-500">
          <div v-if="!mainStore.selectedLoan">
            Select a loan to view documents
          </div>
          <div v-else>
            No documents found
          </div>
        </div>

        <div v-else class="space-y-0">
          <div
            v-for="document in filteredDocuments"
            :key="document.id"
            :class="[
              'document-grid-row grid grid-cols-6 gap-2 text-xs cursor-pointer',
              {
                'selected': mainStore.selectedDocument?.id === document.id
              }
            ]"
            @click="selectDocument(document)"
            @dblclick="openDocument(document)"
          >
            <!-- Doc Type with icon -->
            <div class="col-span-2 document-grid-cell flex items-center space-x-1">
              <DocumentIcon class="h-4 w-4 text-gray-400 flex-shrink-0" />
              <FolderIcon 
                v-if="document.documentType" 
                class="h-4 w-4 text-yellow-500 flex-shrink-0" 
              />
              <span class="truncate text-xs">
                {{ document.documentType || 'Unclassified' }}
              </span>
            </div>

            <!-- Page Count -->
            <div class="document-grid-cell text-center">
              {{ document.pageCount || 0 }}
            </div>

            <!-- CST (could be computed status) -->
            <div class="document-grid-cell text-center">
              {{ document.imageStatusTypeId || 0 }}
            </div>

            <!-- Status -->
            <div class="document-grid-cell text-center">
              <div class="flex justify-center">
                <CheckIcon 
                  v-if="!document.corrupted" 
                  class="h-3 w-3 text-green-500" 
                />
                <ExclamationTriangleIcon 
                  v-else 
                  class="h-3 w-3 text-red-500" 
                />
              </div>
            </div>

            <!-- CD (Creation Date) -->
            <div class="document-grid-cell text-center text-xs">
              {{ formatDate(document.dateCreated) }}
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Image Actions Tabs -->
    <div class="border-t border-gray-200 bg-gray-50">
      <!-- Tab Headers -->
      <div class="flex border-b border-gray-200">
        <button
          v-for="tab in actionTabs"
          :key="tab.id"
          :class="[
            'px-3 py-2 text-xs font-medium border-b-2 transition-colors',
            {
              'border-hydra-500 text-hydra-600 bg-white': activeTab === tab.id,
              'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300': activeTab !== tab.id
            }
          ]"
          @click="activeTab = tab.id"
        >
          {{ tab.label }}
        </button>
      </div>

      <!-- Tab Content -->
      <div class="p-3 h-48 overflow-y-auto">
        <!-- Bookmarks Tab -->
        <div v-if="activeTab === 'bookmarks'" class="text-xs text-gray-600">
          <div v-if="!mainStore.selectedDocument">
            Select a document to view bookmarks
          </div>
          <div v-else class="mt-2">
            <BookmarksList
              :bookmarks="indexingStore.pendingBookmarks"
              :current-page="mainStore.currentPage"
              :editable="true"
              :loading="indexingStore.bookmarksLoading"
              :available-document-types="indexingStore.availableDocumentTypes"
              :processing-results="indexingStore.processingResults"
              :selected-bookmark-id="indexingStore.selectedBookmarkId"
              @bookmark-selected="indexingStore.selectBookmark"
              @navigate-to-page="handleNavigateToPage"
              @bookmark-updated="handleBookmarkUpdated"
              @bookmark-deleted="indexingStore.deleteBookmark"
              @clear-all-bookmarks="indexingStore.clearAllBookmarks"
            />
          </div>
        </div>

        <!-- Redactions Tab -->
        <div v-if="activeTab === 'redactions'" class="text-xs text-gray-600">
          <div v-if="!mainStore.selectedDocument">
            Select a document to view redactions
          </div>
          <div v-else-if="mainStore.selectedDocument.isRedacted">
            <div class="flex items-center space-x-1 text-red-600">
              <ExclamationTriangleIcon class="h-3 w-3" />
              <span>Document has redactions</span>
            </div>
          </div>
          <div v-else>
            No redactions applied
          </div>
        </div>

        <!-- Save Status Tab -->
        <div v-if="activeTab === 'saveStatus'" class="text-xs text-gray-600">
          <div v-if="!mainStore.selectedDocument">
            Select a document to view save status
          </div>
          <div v-else>
            <div class="space-y-1">
              <div>Status: Ready</div>
              <div>Last saved: {{ formatDateTime(mainStore.selectedDocument.dateUpdated || mainStore.selectedDocument.dateCreated) }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useMainStore } from '@/stores/main'
import type { DocumentSummary } from '@/types/domain'
import {
  DocumentIcon,
  FolderIcon,
  CheckIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'
import BookmarksList from '@/components/indexing/BookmarksList.vue'
import { useIndexingStore } from '@/stores/indexing'

const mainStore = useMainStore()
const indexingStore = useIndexingStore()

// Local search state
const imageSearchTerm = ref('')

// Action tabs matching legacy eStacker
const actionTabs = [
  { id: 'bookmarks', label: 'Bookmarks' },
  { id: 'redactions', label: 'Redactions' },
  { id: 'saveStatus', label: 'Save Status' }
]

const activeTab = ref('bookmarks')

// Filter documents based on search term
const filteredDocuments = computed(() => {
  if (!imageSearchTerm.value.trim()) {
    return mainStore.availableDocuments
  }
  
  const searchTerm = imageSearchTerm.value.toLowerCase()
  return mainStore.availableDocuments.filter(doc =>
    doc.originalName.toLowerCase().includes(searchTerm) ||
    doc.documentType?.toLowerCase().includes(searchTerm) ||
    doc.comments?.toLowerCase().includes(searchTerm)
  )
})

/*
 * Bookmarks handlers
 */
const handleBookmarkUpdated = (bookmarkId: number, updates: any) => {
  indexingStore.updateBookmark(bookmarkId, updates)
}

const handleNavigateToPage = (pageIndex: number) => {
  // incoming pageIndex is 0-based from BookmarksList
  mainStore.goToPage(pageIndex + 1)
}

// Handle image search input
const handleImageSearch = () => {
  // Real-time filtering is handled by the computed property
  // Could add debouncing here if needed for performance
}

// Document selection
const selectDocument = (document: DocumentSummary) => {
  mainStore.selectDocument(document)
}

// Double-click to open document (same as single click for now)
const openDocument = (document: DocumentSummary) => {
  selectDocument(document)
}

// Date formatting utilities
const formatDate = (date: Date | string) => {
  const dateObj = typeof date === 'string' ? new Date(date) : date
  return dateObj.toLocaleDateString('en-US', {
    month: 'numeric',
    day: 'numeric',
    year: '2-digit'
  })
}

const formatDateTime = (date: Date | string) => {
  const dateObj = typeof date === 'string' ? new Date(date) : date
  return dateObj.toLocaleString('en-US', {
    month: 'numeric',
    day: 'numeric',
    year: '2-digit',
    hour: 'numeric',
    minute: '2-digit'
  })
}

</script>

<style scoped>
/* Additional component-specific styles */
.document-grid-row:hover {
  @apply bg-gray-50;
}

.document-grid-row.selected {
  @apply bg-orange-100 border-l-4 border-l-orange-400;
}
</style>
