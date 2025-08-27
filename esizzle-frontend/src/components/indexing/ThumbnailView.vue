<template>
  <div class="thumbnail-view">
    <!-- Header -->
    <div class="bg-gray-50 px-2 py-2 border-b border-gray-200">
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-medium text-gray-700">Page Thumbnails</h3>
        <div class="flex items-center space-x-2">
          <span v-if="totalPages > 0" class="text-xs text-gray-500">
            {{ totalPages }} pages
          </span>
        </div>
      </div>
    </div>

    <!-- Thumbnails Grid -->
    <div class="flex-1 overflow-y-auto">
      <div v-if="loading" class="p-4 text-center">
        <div class="spinner mx-auto mb-2"></div>
        <p class="text-sm text-gray-600">Loading thumbnails...</p>
      </div>

      <div v-else-if="totalPages === 0" class="p-4 text-center text-sm text-gray-500">
        No document loaded
      </div>

      <div v-else class="thumbnails-container p-2" ref="thumbnailsContainer">
        <div
          v-for="pageNumber in totalPages"
          :key="pageNumber"
          class="thumbnail-wrapper"
        >
          <!-- Break indicator ABOVE this thumbnail (if bookmark exists for this pageIndex) -->
          <div 
            v-if="hasBookmarkOnPage(pageNumber - 1)"
            class="break-indicator mb-2"
          >
            <div class="flex items-center">
              <div class="flex-1 h-px bg-gray-300"></div>
              <div 
                :class="[
                  'px-3 py-1 text-xs font-medium rounded-full border-2 border-white shadow-md mx-2',
                  {
                    'bg-green-100 text-green-800': getBookmarkType(pageNumber - 1) === 'normal',
                    'bg-orange-100 text-orange-800': getBookmarkType(pageNumber - 1) === 'generic'
                  }
                ]"
              >
                {{ getDocumentTypeName(pageNumber - 1) || (getBookmarkType(pageNumber - 1) === 'generic' ? 'GENERIC BREAK' : 'DOCUMENT BREAK') }}
              </div>
              <div class="flex-1 h-px bg-gray-300"></div>
            </div>
          </div>

          <!-- Thumbnail item -->
          <div
            :class="[
              'thumbnail-item cursor-pointer transition-all mb-3',
              {
                'current-page': currentPage === pageNumber
              }
            ]"
            @click="selectPage(pageNumber)"
          >
            <!-- Thumbnail Container -->
            <div class="thumbnail-container">
              <!-- Actual thumbnail image -->
              <img 
                v-if="getThumbnailUrl(pageNumber - 1)"
                :src="getThumbnailUrl(pageNumber - 1)"
                :alt="`Page ${pageNumber} thumbnail`"
                class="thumbnail-image object-contain bg-white border border-gray-200 rounded"
                @load="handleImageLoad(pageNumber)"
                @error="handleImageError(pageNumber)"
              />
              <!-- Fallback when no thumbnail available -->
              <div v-else class="thumbnail-image bg-gray-100 border border-gray-200 rounded shadow-sm">
                <div class="flex flex-col items-center justify-center h-full p-2">
                  <DocumentIcon class="h-12 w-12 text-gray-400 mb-2" />
                  <div class="text-xs text-gray-500 text-center">
                    Page {{ pageNumber }}
                  </div>
                </div>
              </div>

              <!-- Current page indicator -->
              <div 
                v-if="currentPage === pageNumber"
                class="current-page-indicator absolute -top-1 -left-1"
              >
                <div class="w-4 h-4 bg-blue-500 rounded-full border-2 border-white shadow-md"></div>
              </div>
            </div>

            <!-- Page number -->
            <div class="text-center mt-2">
              <span class="text-xs text-gray-600 font-medium">{{ pageNumber }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Legend -->
    <div class="p-2 bg-gray-50 border-t border-gray-200">
      <div class="text-xs text-gray-600">
        <div class="flex items-center justify-between mb-2">
          <span class="font-medium">Legend:</span>
        </div>
        <div class="space-y-1">
          <div class="flex items-center space-x-2">
            <div class="w-3 h-3 bg-blue-500 rounded-full"></div>
            <span>Current page</span>
          </div>
          <div class="flex items-center space-x-2">
            <div class="w-3 h-3 bg-green-500 rounded-full"></div>
            <span>Normal document break</span>
          </div>
          <div class="flex items-center space-x-2">
            <div class="w-3 h-3 bg-orange-500 rounded-full"></div>
            <span>Generic document break</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, watch, nextTick, ref } from 'vue'
import type { BookmarkDto, PageThumbnailDto } from '@/types/indexing'
import type { DocumentSummary } from '@/types/domain'
import { DocumentIcon } from '@heroicons/vue/24/outline'

interface Props {
  selectedDocument: DocumentSummary | null
  currentPage: number
  totalPages: number
  bookmarks: BookmarkDto[]
  thumbnails: PageThumbnailDto[]
  loading: boolean
}

interface Emits {
  (e: 'page-selected', pageNumber: number): void
  (e: 'thumbnail-loaded', pageNumber: number): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const thumbnailsContainer = ref<HTMLElement>()

// Computed properties
const bookmarksByPage = computed(() => {
  const map = new Map<number, BookmarkDto>()
  props.bookmarks.forEach(bookmark => {
    map.set(bookmark.pageIndex, bookmark)
  })
  return map
})

// Methods
const selectPage = (pageNumber: number) => {
  emit('page-selected', pageNumber)
}

const hasBookmarkOnPage = (pageIndex: number): boolean => {
  return bookmarksByPage.value.has(pageIndex)
}

const getBookmarkType = (pageIndex: number): 'normal' | 'generic' | null => {
  const bookmark = bookmarksByPage.value.get(pageIndex)
  if (!bookmark) return null
  return bookmark.isGeneric ? 'generic' : 'normal'
}

const getDocumentTypeName = (pageIndex: number): string => {
  const bookmark = bookmarksByPage.value.get(pageIndex)
  return bookmark?.documentTypeName || ''
}

const getBookmarkTooltip = (pageIndex: number): string => {
  const bookmark = bookmarksByPage.value.get(pageIndex)
  if (!bookmark) return ''
  
  const type = bookmark.isGeneric ? 'Generic' : 'Normal'
  return `${type}: ${bookmark.documentTypeName}`
}

const getThumbnailUrl = (pageIndex: number): string | undefined => {
  // Find thumbnail for this page (pageIndex is 0-based)
  const thumbnail = props.thumbnails.find(t => t.pageNumber === pageIndex + 1)
  return thumbnail?.thumbnailUrl
}

const handleImageLoad = (pageNumber: number) => {
  emit('thumbnail-loaded', pageNumber)
}

const handleImageError = (pageNumber: number) => {
  console.warn(`Failed to load thumbnail for page ${pageNumber}`)
}

const scrollToCurrentPage = () => {
  if (!thumbnailsContainer.value || props.currentPage <= 0) return
  
  nextTick(() => {
    const thumbnailItems = thumbnailsContainer.value?.querySelectorAll('.thumbnail-item')
    if (!thumbnailItems || thumbnailItems.length < props.currentPage) return
    
    const currentThumbnail = thumbnailItems[props.currentPage - 1] as HTMLElement
    if (currentThumbnail) {
      currentThumbnail.scrollIntoView({
        behavior: 'smooth',
        block: 'center'
      })
    }
  })
}

// Watch for current page changes to auto-scroll
watch(() => props.currentPage, () => {
  scrollToCurrentPage()
}, { immediate: false })

// Watch for document changes to reset scroll
watch(() => props.selectedDocument, () => {
  if (thumbnailsContainer.value) {
    thumbnailsContainer.value.scrollTop = 0
  }
})
</script>

<style scoped>
.thumbnail-view {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.thumbnails-container {
  flex: 1;
  overflow-y: auto;
  padding: 4px 1px; /* Further reduced horizontal padding */
}

.thumbnail-item {
  position: relative;
  margin-bottom: 8px; /* Better spacing to match legacy */
  padding: 1px; /* Minimal padding for tightest layout */
  border-radius: 4px;
  transition: all 0.2s ease-in-out;
}

.thumbnail-item:hover {
  background-color: #f9fafb;
  transform: scale(1.02);
}

.thumbnail-container {
  position: relative;
  width: 100%;
  max-width: 140px; /* Back to previous size */
  /* Reduced aspect ratio to eliminate top/bottom whitespace */
  height: 0;
  padding-bottom: 60%; /* Shorter height to eliminate top/bottom whitespace */
  border: 1px solid #e5e7eb; /* Subtle border to define the thumbnail */
  border-radius: 3px; /* Small border radius */
  overflow: hidden;
  margin: 0 auto; /* Center the thumbnails */
  background-color: white; /* White background for thumbnails */
}

.thumbnail-image {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #f3f4f6;
}

/* Current page styling */
.thumbnail-item.current-page .thumbnail-container {
  border-color: #3b82f6;
  box-shadow: 0 0 8px rgba(59, 130, 246, 0.4);
}

/* Break indicator styling */
.break-indicator {
  position: relative;
  z-index: 10;
}

.break-indicator .h-px {
  background: linear-gradient(to right, transparent, #d1d5db 20%, #d1d5db 80%, transparent);
}

/* Wrapper for each thumbnail + break */
.thumbnail-wrapper {
  position: relative;
}

/* Indicators */
.current-page-indicator {
  z-index: 10;
}

/* Spinner styling */
.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid #f3f4f6;
  border-top: 2px solid #dc2626;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* Scrollbar styling */
.thumbnails-container::-webkit-scrollbar {
  width: 6px;
}

.thumbnails-container::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.thumbnails-container::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.thumbnails-container::-webkit-scrollbar-thumb:hover {
  background: #a1a1a1;
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .thumbnail-container {
    height: 100px;
  }
  
  .thumbnail-item {
    margin-bottom: 8px;
  }
}
</style>
