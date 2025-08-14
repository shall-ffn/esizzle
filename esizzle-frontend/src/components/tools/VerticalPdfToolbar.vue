<template>
  <div class="vertical-pdf-toolbar bg-gray-100 border-l border-gray-300 flex flex-col items-center py-3 px-2">
    <!-- Generic Break Tools (Primary section - matches legacy) -->
    <div class="flex flex-col items-center space-y-2 pb-3 border-b border-gray-300">
      <!-- Add Generic Break (Legacy icon only) -->
      <button
        @click="addGenericBreak"
        :disabled="!canAddGenericBreak"
        :class="[
          'w-8 h-8 flex items-center justify-center transition-all',
          {
            'hover:bg-gray-200': canAddGenericBreak,
            'opacity-50 cursor-not-allowed': !canAddGenericBreak
          }
        ]"
        title="Add Generic Break"
      >
        <img src="@/assets/icons/generic-break.png" class="h-6 w-6" alt="Add Generic Break" />
      </button>
      
      <!-- Remove Generic Break (Legacy icon only) -->
      <button
        @click="removeGenericBreak"
        :disabled="!canRemoveGenericBreak"
        :class="[
          'w-8 h-8 flex items-center justify-center transition-all',
          {
            'hover:bg-gray-200': canRemoveGenericBreak,
            'opacity-50 cursor-not-allowed': !canRemoveGenericBreak
          }
        ]"
        title="Remove Generic Break"
      >
        <img src="@/assets/icons/remove-break.png" class="h-6 w-6" alt="Remove Generic Break" />
      </button>
    </div>

    <!-- Page Navigation -->
    <div class="flex flex-col items-center space-y-2 py-3 border-b border-gray-300">
      <!-- Previous Page -->
      <button
        @click="previousPage"
        :disabled="!canGoPrevious"
        :class="[
          'w-8 h-8 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm',
          {
            'hover:bg-gray-50 text-gray-700': canGoPrevious,
            'bg-gray-100 text-gray-400 cursor-not-allowed': !canGoPrevious
          }
        ]"
        title="Previous Page"
      >
        <ChevronUpIcon class="h-4 w-4" />
      </button>

      <!-- Page Number Display -->
      <div class="text-xs text-center text-gray-600 px-1">
        <div class="font-medium">Page</div>
        <div>{{ currentPage }}</div>
      </div>

      <!-- Next Page -->
      <button
        @click="nextPage"
        :disabled="!canGoNext"
        :class="[
          'w-8 h-8 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm',
          {
            'hover:bg-gray-50 text-gray-700': canGoNext,
            'bg-gray-100 text-gray-400 cursor-not-allowed': !canGoNext
          }
        ]"
        title="Next Page"
      >
        <ChevronDownIcon class="h-4 w-4" />
      </button>
    </div>

    <!-- PDF Manipulation Tools -->
    <div class="flex flex-col items-center space-y-1 py-2 border-b border-gray-300">
      <!-- Zoom In -->
      <button
        @click="zoomIn"
        :disabled="!selectedDocument"
        class="w-7 h-7 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm hover:bg-gray-50 text-gray-700 disabled:bg-gray-100 disabled:text-gray-400 disabled:cursor-not-allowed"
        title="Zoom In"
      >
        <MagnifyingGlassPlusIcon class="h-3 w-3" />
      </button>

      <!-- Zoom Out -->
      <button
        @click="zoomOut"
        :disabled="!selectedDocument"
        class="w-7 h-7 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm hover:bg-gray-50 text-gray-700 disabled:bg-gray-100 disabled:text-gray-400 disabled:cursor-not-allowed"
        title="Zoom Out"
      >
        <MagnifyingGlassMinusIcon class="h-3 w-3" />
      </button>

      <!-- Fit to Width -->
      <button
        @click="fitToWidth"
        :disabled="!selectedDocument"
        class="w-7 h-7 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm hover:bg-gray-50 text-gray-700 disabled:bg-gray-100 disabled:text-gray-400 disabled:cursor-not-allowed"
        title="Fit to Width"
      >
        <RectangleStackIcon class="h-3 w-3" />
      </button>
    </div>

    <!-- Document Processing Tools -->
    <div class="flex flex-col items-center space-y-1 py-2 border-b border-gray-300">
      <!-- Rotate -->
      <button
        @click="rotateDocument"
        :disabled="!selectedDocument || loadingRotation"
        :class="[
          'w-7 h-7 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm',
          {
            'hover:bg-gray-50 text-gray-700': selectedDocument && !loadingRotation,
            'bg-gray-100 text-gray-400 cursor-not-allowed': !selectedDocument || loadingRotation
          }
        ]"
        title="Rotate 90°"
      >
        <ArrowPathIcon class="h-3 w-3" />
      </button>

      <!-- Redact -->
      <button
        @click="startRedaction"
        :disabled="!selectedDocument || loadingRedaction"
        :class="[
          'w-7 h-7 rounded border flex items-center justify-center transition-all shadow-sm',
          {
            'bg-red-50 border-red-300 hover:bg-red-100 text-red-700': selectedDocument && !loadingRedaction,
            'bg-gray-100 border-gray-300 text-gray-400 cursor-not-allowed': !selectedDocument || loadingRedaction
          }
        ]"
        title="Redact Document"
      >
        <PencilIcon class="h-3 w-3" />
      </button>

      <!-- Split -->
      <button
        @click="splitDocument"
        :disabled="!selectedDocument"
        class="w-7 h-7 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm hover:bg-gray-50 text-gray-700 disabled:bg-gray-100 disabled:text-gray-400 disabled:cursor-not-allowed"
        title="Split Document"
      >
        <ScissorsIcon class="h-3 w-3" />
      </button>

      <!-- Stack -->
      <button
        @click="stackDocument"
        :disabled="!selectedDocument"
        class="w-7 h-7 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm hover:bg-gray-50 text-gray-700 disabled:bg-gray-100 disabled:text-gray-400 disabled:cursor-not-allowed"
        title="Stack Documents"
      >
        <DocumentDuplicateIcon class="h-3 w-3" />
      </button>
    </div>

    <!-- Additional Tools -->
    <div class="flex flex-col items-center space-y-1 py-2">
      <!-- Print -->
      <button
        @click="printDocument"
        :disabled="!selectedDocument"
        class="w-7 h-7 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm hover:bg-gray-50 text-gray-700 disabled:bg-gray-100 disabled:text-gray-400 disabled:cursor-not-allowed"
        title="Print Document"
      >
        <PrinterIcon class="h-3 w-3" />
      </button>

      <!-- Download -->
      <button
        @click="downloadDocument"
        :disabled="!selectedDocument"
        class="w-7 h-7 rounded bg-white border border-gray-300 flex items-center justify-center transition-all shadow-sm hover:bg-gray-50 text-gray-700 disabled:bg-gray-100 disabled:text-gray-400 disabled:cursor-not-allowed"
        title="Download Document"
      >
        <ArrowDownTrayIcon class="h-3 w-3" />
      </button>
    </div>

    <!-- Status Indicator -->
    <div v-if="statusMessage" class="mt-auto pt-2 border-t border-gray-300">
      <div class="text-xs text-center text-gray-600 px-1">
        <div 
          v-if="statusType === 'loading'"
          class="w-3 h-3 border border-gray-400 border-t-transparent border-r-transparent rounded-full animate-spin mx-auto mb-1"
        ></div>
        <CheckCircleIcon 
          v-else-if="statusType === 'success'"
          class="h-3 w-3 text-green-500 mx-auto mb-1"
        />
        <ExclamationTriangleIcon 
          v-else-if="statusType === 'error'"
          class="h-3 w-3 text-red-500 mx-auto mb-1"
        />
        <div class="break-words text-xs leading-tight">{{ statusMessage }}</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useMainStore } from '@/stores/main'
import { useIndexingStore } from '@/stores/indexing'
import type { DocumentSummary } from '@/types/domain'
import {
  PlusIcon,
  XMarkIcon,
  DocumentPlusIcon,
  DocumentMinusIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  MagnifyingGlassPlusIcon,
  MagnifyingGlassMinusIcon,
  RectangleStackIcon,
  ArrowPathIcon,
  PencilIcon,
  ScissorsIcon,
  DocumentDuplicateIcon,
  PrinterIcon,
  ArrowDownTrayIcon,
  CheckCircleIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'

interface Props {
  selectedDocument?: DocumentSummary | null
  currentPage: number
  totalPages: number
}

interface Emits {
  (e: 'generic-break-added', pageIndex: number): void
  (e: 'generic-break-removed', pageIndex: number): void
  (e: 'page-changed', page: number): void
  (e: 'zoom-changed', zoomLevel: number): void
  (e: 'document-rotated', documentId: number, angle: number): void
  (e: 'redaction-started', documentId: number): void
  (e: 'document-split', documentId: number): void
  (e: 'document-stacked', documentId: number): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const mainStore = useMainStore()
const indexingStore = useIndexingStore()

// Component state
const loadingRotation = ref(false)
const loadingRedaction = ref(false)

// Status messaging
const statusMessage = ref('')
const statusType = ref<'loading' | 'success' | 'error' | null>(null)

// Computed properties
const canAddGenericBreak = computed(() => {
  return props.selectedDocument && props.currentPage > 0
})

const canRemoveGenericBreak = computed(() => {
  // Check if current page has a generic break that can be removed
  const currentPageIndex = props.currentPage - 1
  return indexingStore.bookmarksByPage.has(currentPageIndex)
})

const canGoPrevious = computed(() => {
  return props.currentPage > 1
})

const canGoNext = computed(() => {
  return props.currentPage < props.totalPages
})

// Status management
const setStatus = (message: string, type: 'loading' | 'success' | 'error') => {
  statusMessage.value = message
  statusType.value = type
  
  if (type !== 'loading') {
    setTimeout(() => {
      statusMessage.value = ''
      statusType.value = null
    }, 3000)
  }
}

// Generic break functions
const addGenericBreak = async () => {
  if (!props.selectedDocument || !canAddGenericBreak.value) return
  
  try {
    const pageIndex = props.currentPage - 1 // Convert to 0-based
    await indexingStore.createGenericBreak(props.selectedDocument.id, pageIndex)
    emit('generic-break-added', pageIndex)
    setStatus('Generic break added', 'success')
  } catch (error) {
    setStatus('Failed to add generic break', 'error')
    console.error('Failed to add generic break:', error)
  }
}

const removeGenericBreak = async () => {
  if (!canRemoveGenericBreak.value) return
  
  try {
    const pageIndex = props.currentPage - 1
    const bookmark = indexingStore.bookmarksByPage.get(pageIndex)
    if (bookmark) {
      await indexingStore.deleteBookmark(bookmark.id)
      emit('generic-break-removed', pageIndex)
      setStatus('Generic break removed', 'success')
    }
  } catch (error) {
    setStatus('Failed to remove generic break', 'error')
    console.error('Failed to remove generic break:', error)
  }
}

// Navigation functions
const previousPage = () => {
  if (canGoPrevious.value) {
    emit('page-changed', props.currentPage - 1)
  }
}

const nextPage = () => {
  if (canGoNext.value) {
    emit('page-changed', props.currentPage + 1)
  }
}

// Zoom functions
const zoomIn = () => {
  emit('zoom-changed', mainStore.zoomLevel + 25)
}

const zoomOut = () => {
  emit('zoom-changed', mainStore.zoomLevel - 25)
}

const fitToWidth = () => {
  emit('zoom-changed', 100)
}

// Document manipulation functions
const rotateDocument = async () => {
  if (!props.selectedDocument) return
  
  loadingRotation.value = true
  setStatus('Rotating document...', 'loading')
  
  try {
    emit('document-rotated', props.selectedDocument.id, 90)
    setStatus('Document rotated', 'success')
  } catch (error) {
    setStatus('Failed to rotate document', 'error')
  } finally {
    loadingRotation.value = false
  }
}

const startRedaction = () => {
  if (!props.selectedDocument) return
  
  loadingRedaction.value = true
  setStatus('Starting redaction...', 'loading')
  
  emit('redaction-started', props.selectedDocument.id)
  
  setTimeout(() => {
    loadingRedaction.value = false
    setStatus('Redaction mode active', 'success')
  }, 500)
}

const splitDocument = () => {
  if (!props.selectedDocument) return
  emit('document-split', props.selectedDocument.id)
}

const stackDocument = () => {
  if (!props.selectedDocument) return
  emit('document-stacked', props.selectedDocument.id)
}

const printDocument = () => {
  if (!props.selectedDocument) return
  setStatus('Print not implemented', 'error')
}

const downloadDocument = () => {
  if (!props.selectedDocument) return
  setStatus('Download not implemented', 'error')
}
</script>

<style scoped>
.vertical-pdf-toolbar {
  width: 50px;
  min-width: 50px;
  max-width: 50px;
  min-height: 100%;
}

/* Ensure buttons maintain aspect ratio */
.vertical-pdf-toolbar button {
  flex-shrink: 0;
}

/* Custom scrollbar for overflow */
.vertical-pdf-toolbar::-webkit-scrollbar {
  width: 4px;
}

.vertical-pdf-toolbar::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.vertical-pdf-toolbar::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 2px;
}

.vertical-pdf-toolbar::-webkit-scrollbar-thumb:hover {
  background: #a1a1a1;
}
</style>