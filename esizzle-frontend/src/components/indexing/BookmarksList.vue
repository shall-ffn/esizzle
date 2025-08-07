<template>
  <div class="bookmarks-list">
    <!-- Header -->
    <div class="bg-gray-50 px-3 py-2 border-b border-gray-200">
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-medium text-gray-700">
          Document Bookmarks
          <span v-if="bookmarks.length > 0" class="text-xs text-gray-500 ml-1">
            ({{ bookmarks.length }})
          </span>
        </h3>
        <div class="flex items-center space-x-2">
          <button
            v-if="bookmarks.length > 0"
            @click="clearAllBookmarks"
            class="text-xs text-red-600 hover:text-red-700"
            title="Clear all bookmarks"
          >
            Clear All
          </button>
        </div>
      </div>
    </div>

    <!-- Bookmarks List -->
    <div class="flex-1 overflow-y-auto">
      <div v-if="loading" class="p-4 text-center">
        <div class="spinner mx-auto mb-2"></div>
        <p class="text-sm text-gray-600">Loading bookmarks...</p>
      </div>

      <div v-else-if="bookmarks.length === 0" class="p-4 text-center text-sm text-gray-500">
        No bookmarks created yet
        <div class="mt-2 text-xs text-gray-400">
          Select a document type and click "Set Break" to create bookmarks
        </div>
      </div>

      <div v-else class="bookmarks-container">
        <div
          v-for="bookmark in sortedBookmarks"
          :key="bookmark.id"
          :class="[
            'bookmark-item cursor-pointer transition-all',
            {
              'selected': selectedBookmarkId === bookmark.id,
              'processing': bookmark.resultImageId && processingResults.some(r => r.resultImageId === bookmark.resultImageId)
            }
          ]"
          @click="navigateToBookmark(bookmark)"
        >
          <div class="flex items-center justify-between p-3 border-b border-gray-100">
            <div class="flex-1 min-w-0">
              <div class="flex items-center space-x-2">
                <div
                  :class="[
                    'w-3 h-3 rounded',
                    {
                      'bg-green-500': !bookmark.isGeneric,
                      'bg-orange-500': bookmark.isGeneric
                    }
                  ]"
                ></div>
                <div class="text-sm font-medium text-gray-900 truncate">
                  {{ bookmark.documentTypeName }}
                </div>
              </div>
              
              <div class="flex items-center justify-between mt-1">
                <div class="text-xs text-gray-500">
                  Page {{ bookmark.pageIndex + 1 }}
                </div>
                <div class="text-xs text-gray-400">
                  {{ formatDate(bookmark.dateCreated) }}
                </div>
              </div>

              <!-- Processing status -->
              <div v-if="bookmark.resultImageId" class="mt-2">
                <div class="flex items-center space-x-2">
                  <div class="w-2 h-2 bg-green-500 rounded-full"></div>
                  <span class="text-xs text-green-600">Processed</span>
                </div>
              </div>
            </div>

            <!-- Actions -->
            <div v-if="editable && !bookmark.resultImageId" class="flex items-center space-x-1 ml-2">
              <button
                @click.stop="editBookmark(bookmark)"
                class="p-1 text-gray-400 hover:text-gray-600 transition-colors"
                title="Edit bookmark"
              >
                <PencilIcon class="h-4 w-4" />
              </button>
              
              <button
                @click.stop="deleteBookmark(bookmark.id)"
                class="p-1 text-red-400 hover:text-red-600 transition-colors"
                title="Delete bookmark"
              >
                <TrashIcon class="h-4 w-4" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Edit Modal -->
    <div
      v-if="editingBookmark"
      class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50"
      @click="cancelEdit"
    >
      <div class="bg-white rounded-lg p-6 w-96 max-w-full mx-4" @click.stop>
        <h3 class="text-lg font-medium text-gray-900 mb-4">Edit Bookmark</h3>
        
        <div class="space-y-4">
          <!-- Document Type -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Document Type
            </label>
            <select
              v-model="editForm.imageDocumentTypeId"
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
            >
              <option value="">Select document type...</option>
              <option
                v-for="docType in availableDocumentTypes"
                :key="docType.id"
                :value="docType.id"
              >
                {{ docType.name }}
              </option>
            </select>
          </div>

          <!-- Comments -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Comments
            </label>
            <textarea
              v-model="editForm.comments"
              rows="3"
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
              placeholder="Optional comments..."
            ></textarea>
          </div>

          <!-- Document Date -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Document Date
            </label>
            <input
              type="date"
              v-model="editForm.documentDate"
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
            />
          </div>
        </div>

        <!-- Actions -->
        <div class="flex justify-end space-x-3 mt-6">
          <button
            @click="cancelEdit"
            class="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            @click="saveEdit"
            :disabled="!editForm.imageDocumentTypeId"
            class="px-4 py-2 text-sm font-medium text-white bg-hydra-600 border border-transparent rounded-md hover:bg-hydra-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Save Changes
          </button>
        </div>
      </div>
    </div>

    <!-- Delete Confirmation -->
    <div
      v-if="deletingBookmarkId"
      class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50"
      @click="cancelDelete"
    >
      <div class="bg-white rounded-lg p-6 w-80 max-w-full mx-4" @click.stop>
        <h3 class="text-lg font-medium text-gray-900 mb-4">Delete Bookmark</h3>
        <p class="text-sm text-gray-600 mb-6">
          Are you sure you want to delete this bookmark? This action cannot be undone.
        </p>
        
        <div class="flex justify-end space-x-3">
          <button
            @click="cancelDelete"
            class="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            @click="confirmDelete"
            class="px-4 py-2 text-sm font-medium text-white bg-red-600 border border-transparent rounded-md hover:bg-red-700"
          >
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { BookmarkDto, DocumentTypeDto, ProcessingResultDto, UpdateBookmarkRequest } from '@/types/indexing'
import {
  PencilIcon,
  TrashIcon
} from '@heroicons/vue/24/outline'

interface Props {
  bookmarks: BookmarkDto[]
  currentPage: number
  editable: boolean
  loading: boolean
  availableDocumentTypes: DocumentTypeDto[]
  processingResults: ProcessingResultDto[]
  selectedBookmarkId?: number | null
}

interface Emits {
  (e: 'bookmark-selected', bookmarkId: number): void
  (e: 'bookmark-updated', bookmarkId: number, updates: UpdateBookmarkRequest): void
  (e: 'bookmark-deleted', bookmarkId: number): void
  (e: 'navigate-to-page', pageIndex: number): void
  (e: 'clear-all-bookmarks'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const editingBookmark = ref<BookmarkDto | null>(null)
const deletingBookmarkId = ref<number | null>(null)
const editForm = ref({
  imageDocumentTypeId: 0,
  comments: '',
  documentDate: ''
})

// Computed properties
const sortedBookmarks = computed(() => {
  return [...props.bookmarks].sort((a, b) => a.pageIndex - b.pageIndex)
})

// Methods
const navigateToBookmark = (bookmark: BookmarkDto) => {
  emit('bookmark-selected', bookmark.id)
  emit('navigate-to-page', bookmark.pageIndex)
}

const editBookmark = (bookmark: BookmarkDto) => {
  editingBookmark.value = bookmark
  editForm.value = {
    imageDocumentTypeId: bookmark.imageDocumentTypeId,
    comments: parseCommentsFromText(bookmark.text),
    documentDate: parseDocumentDateFromText(bookmark.text) || new Date().toISOString().split('T')[0]
  }
}

const cancelEdit = () => {
  editingBookmark.value = null
  editForm.value = {
    imageDocumentTypeId: 0,
    comments: '',
    documentDate: ''
  }
}

const saveEdit = () => {
  if (!editingBookmark.value || !editForm.value.imageDocumentTypeId) return

  const updates: UpdateBookmarkRequest = {
    documentTypeId: editForm.value.imageDocumentTypeId,
    documentTypeName: props.availableDocumentTypes.find(dt => dt.id === editForm.value.imageDocumentTypeId)?.name,
    comments: editForm.value.comments,
    documentDate: editForm.value.documentDate ? new Date(editForm.value.documentDate) : undefined
  }

  emit('bookmark-updated', editingBookmark.value.id, updates)
  cancelEdit()
}

const deleteBookmark = (bookmarkId: number) => {
  deletingBookmarkId.value = bookmarkId
}

const cancelDelete = () => {
  deletingBookmarkId.value = null
}

const confirmDelete = () => {
  if (deletingBookmarkId.value) {
    emit('bookmark-deleted', deletingBookmarkId.value)
    deletingBookmarkId.value = null
  }
}

const clearAllBookmarks = () => {
  emit('clear-all-bookmarks')
}

const formatDate = (date: Date | string) => {
  const dateObj = typeof date === 'string' ? new Date(date) : date
  return dateObj.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  })
}

// Parse bookmark text field (pipe-delimited format)
const parseCommentsFromText = (text: string): string => {
  const parts = text.split(' | ')
  return parts[3] || ''
}

const parseDocumentDateFromText = (text: string): string | null => {
  const parts = text.split(' | ')
  return parts[2] || null
}
</script>

<style scoped>
.bookmarks-list {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 200px;
}

.bookmarks-container {
  max-height: 300px;
  overflow-y: auto;
}

.bookmark-item {
  transition: all 0.2s ease-in-out;
}

.bookmark-item:hover {
  background-color: #f9fafb;
}

.bookmark-item.selected {
  background-color: #fef3c7;
  border-left: 4px solid #f59e0b;
}

.bookmark-item.processing {
  background-color: #f0fdf4;
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
.bookmarks-container::-webkit-scrollbar {
  width: 6px;
}

.bookmarks-container::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.bookmarks-container::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.bookmarks-container::-webkit-scrollbar-thumb:hover {
  background: #a1a1a1;
}
</style>
