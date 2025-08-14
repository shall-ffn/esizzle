<template>
  <div class="thumbnail-column bg-white border-r border-gray-300 flex flex-col">
    <!-- Header -->
    <div class="bg-gray-50 px-2 py-2 border-b border-gray-200 text-center">
      <h3 class="text-xs font-medium text-gray-700">Pages</h3>
      <div v-if="totalPages > 0" class="text-xs text-gray-500 mt-1">
        {{ totalPages }} total
      </div>
    </div>

    <!-- Thumbnails List -->
    <div class="flex-1 overflow-y-auto">
      <div v-if="loading" class="p-2 text-center">
        <div class="spinner mx-auto mb-2 w-4 h-4"></div>
        <p class="text-xs text-gray-600">Loading...</p>
      </div>

      <div v-else-if="totalPages === 0" class="p-2 text-center text-xs text-gray-500">
        No document loaded
      </div>

      <div v-else class="p-1">
        <div
          v-for="pageNumber in totalPages"
          :key="pageNumber"
          :class="[
            'thumbnail-item mb-2 cursor-pointer transition-all relative',
            {
              'current-page ring-2 ring-blue-500': currentPage === pageNumber,
              'has-bookmark': hasBookmarkOnPage(pageNumber - 1)
            }
          ]"
          @click="selectPage(pageNumber)"
        >
          <!-- Page Number Badge -->
          <div class="absolute top-0 left-0 bg-gray-800 text-white text-xs px-1 rounded-br z-10">
            {{ pageNumber }}
          </div>

          <!-- Bookmark Indicator (matches legacy colors) -->
          <div 
            v-if="hasBookmarkOnPage(pageNumber - 1)"
            :class="[
              'absolute top-0 right-0 w-3 h-3 rounded-bl z-10',
              {
                'bg-orange-500': getBookmarkType(pageNumber - 1) === 'generic',
                'bg-green-600': getBookmarkType(pageNumber - 1) === 'normal'
              }
            ]"
            :title="getBookmarkType(pageNumber - 1) === 'generic' ? 'Generic Break' : 'Document Break'"
          ></div>

          <!-- Thumbnail Container (matches legacy size and aspect ratio) -->
          <div class="thumbnail-container bg-gray-100 border border-gray-300 rounded overflow-hidden">
            <!-- Actual thumbnail image -->
            <img 
              v-if="getThumbnailUrl(pageNumber - 1)"
              :src="getThumbnailUrl(pageNumber - 1)"
              :alt="`Page ${pageNumber} thumbnail`"
              class="thumbnail-image w-full h-auto object-contain bg-white"
              @load="handleImageLoad(pageNumber)"
              @error="handleImageError(pageNumber)"
            />
            
            <!-- Placeholder while loading -->
            <div 
              v-else 
              class="thumbnail-placeholder w-full h-16 bg-gray-200 flex items-center justify-center"
            >
              <DocumentIcon class="h-6 w-6 text-gray-400" />
            </div>
          </div>

          <!-- Page Status Indicators -->
          <div 
            v-if="hasBookmarkOnPage(pageNumber - 1)"
            class="absolute bottom-0 left-0 right-0 bg-black bg-opacity-50 text-white text-xs px-1 py-0.5 text-center"
          >
            {{ getBookmarkDisplayText(pageNumber - 1) }}
          </div>
        </div>
      </div>
    </div>

    <!-- Current Page Info -->
    <div v-if="totalPages > 0" class="bg-gray-50 border-t border-gray-200 px-2 py-1 text-center">
      <div class="text-xs text-gray-600">
        Page {{ currentPage }} of {{ totalPages }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { BookmarkDto, PageThumbnailDto } from '@/types/indexing'
import type { DocumentSummary } from '@/types/domain'
import { DocumentIcon } from '@heroicons/vue/24/outline'
import { isGenericBreak, getBreakDisplayText } from '@/types/indexing'

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
  return isGenericBreak(bookmark) ? 'generic' : 'normal'
}

const getBookmarkDisplayText = (pageIndex: number): string => {
  const bookmark = bookmarksByPage.value.get(pageIndex)
  if (!bookmark) return ''
  return isGenericBreak(bookmark) ? 'GENERIC' : 'BREAK'
}

const getThumbnailUrl = (pageIndex: number): string | null => {
  const thumbnail = props.thumbnails.find(t => t.pageNumber === pageIndex + 1)
  return thumbnail?.thumbnailUrl || null
}

const handleImageLoad = (pageNumber: number) => {
  emit('thumbnail-loaded', pageNumber)
}

const handleImageError = (pageNumber: number) => {
  console.warn(`Failed to load thumbnail for page ${pageNumber}`)
}
</script>

<style scoped>
.thumbnail-column {
  width: 100px;
  min-width: 100px;
  max-width: 100px;
}

.thumbnail-item {
  position: relative;
}

.thumbnail-item.current-page {
  transform: scale(1.02);
}

.thumbnail-container {
  width: 88px;
  height: 74px; /* Maintains ~8.5:11 aspect ratio similar to legacy */
}

.thumbnail-image {
  max-width: 88px;
  max-height: 74px;
}

.thumbnail-placeholder {
  width: 88px;
  height: 74px;
}

/* Custom scrollbar for thumbnail list */
.thumbnail-column .flex-1::-webkit-scrollbar {
  width: 6px;
}

.thumbnail-column .flex-1::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.thumbnail-column .flex-1::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.thumbnail-column .flex-1::-webkit-scrollbar-thumb:hover {
  background: #a1a1a1;
}

/* Smooth transitions */
.thumbnail-item {
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.thumbnail-item:hover {
  transform: scale(1.05);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.current-page {
  box-shadow: 0 0 0 2px #3b82f6;
}

/* Loading spinner styles */
.spinner {
  border: 2px solid #f3f3f3;
  border-top: 2px solid #3498db;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
</style>