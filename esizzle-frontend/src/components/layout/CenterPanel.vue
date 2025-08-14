<template>
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

      <!-- Right side - View controls -->
      <div class="flex items-center space-x-2">
        <!-- View Mode Toggle -->
        <div class="flex bg-gray-100 rounded-md p-1">
          <button
            :class="[
              'px-2 py-1 text-xs rounded transition-colors',
              {
                'bg-white shadow text-gray-900': mainStore.documentViewMode === 'single',
                'text-gray-600 hover:text-gray-900': mainStore.documentViewMode !== 'single'
              }
            ]"
            @click="mainStore.setViewMode('single')"
          >
            Single
          </button>
          <button
            :class="[
              'px-2 py-1 text-xs rounded transition-colors',
              {
                'bg-white shadow text-gray-900': mainStore.documentViewMode === 'thumbnail',
                'text-gray-600 hover:text-gray-900': mainStore.documentViewMode !== 'thumbnail'
              }
            ]"
            @click="mainStore.setViewMode('thumbnail')"
          >
            Thumbnails
          </button>
        </div>

        <!-- Working View and All View tabs -->
        <div class="flex bg-gray-100 rounded-md p-1">
          <button class="px-3 py-1 text-xs bg-white shadow text-gray-900 rounded">
            Working View
          </button>
          <button class="px-3 py-1 text-xs text-gray-600 hover:text-gray-900 rounded">
            All View
          </button>
          <button class="px-3 py-1 text-xs text-gray-600 hover:text-gray-900 rounded">
            History
          </button>
        </div>
      </div>
    </div>

    <!-- Main Viewer Area -->
    <div class="flex-1 flex overflow-hidden">
      <!-- Main PDF Display and Toolbar Area -->
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
            <!-- PDF Viewer Component with Redaction Overlay -->
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
              
              <!-- Redaction Overlay -->
              <RedactionOverlay
                :active="redactionMode"
                :document-id="mainStore.selectedDocument?.id || 0"
                :current-page="mainStore.currentPage"
                @exit="exitRedactionMode"
                @areas-updated="handleRedactionAreasUpdated"
                @apply-redactions="handleApplyRedactions"
              />
            </div>

            <!-- Page Status Bar (minimal) -->
            <div class="bg-gray-900 px-4 py-1 text-white text-center">
              <span class="text-xs">
                Page {{ mainStore.currentPage }} of {{ mainStore.totalPages }} • {{ mainStore.zoomLevel }}%
              </span>
            </div>
          </div>
        </div>

        <!-- Vertical PDF Toolbar (right side) -->
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
      </div>
    </div>

    <!-- Status/Info Bar (replaces bottom toolbar) -->
    <div v-if="mainStore.selectedDocument" class="bg-white border-t border-gray-200 px-4 py-2 text-xs text-gray-600">
      <div class="flex items-center justify-between">
        <span>{{ mainStore.selectedDocument.originalName }}</span>
        <span>{{ mainStore.selectedDocument.pageCount || 0 }} pages • {{ (mainStore.selectedDocument.length / 1024).toFixed(1) }} KB</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useMainStore } from '@/stores/main'
import { useIndexingStore } from '@/stores/indexing'
import PDFViewer from '@/components/viewer/PDFViewer.vue'
import VerticalPdfToolbar from '@/components/tools/VerticalPdfToolbar.vue'
import RedactionOverlay from '@/components/tools/RedactionOverlay.vue'
import type { PageThumbnailDto } from '@/types/indexing'
import {
  DocumentIcon
} from '@heroicons/vue/24/outline'

const mainStore = useMainStore()
const indexingStore = useIndexingStore()

// Component refs
const pdfViewerRef = ref<InstanceType<typeof PDFViewer>>()

interface Props {
  thumbnails: PageThumbnailDto[]
  thumbnailsLoading: boolean
}

interface Emits {
  (e: 'thumbnails-generated', thumbnails: PageThumbnailDto[]): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Redaction state
const redactionMode = ref(false)

// Thumbnail generation is now handled in parent AppShell component

// PDF viewer event handlers
const handleDocumentLoaded = async (pageCount: number) => {
  // Update total pages in store
  mainStore.totalPages = pageCount
  console.log(`Document loaded with ${pageCount} pages`)
}

const handleDocumentError = (error: string) => {
  mainStore.setError(`Failed to load document: ${error}`)
}

const handlePageRendered = (pageNumber: number) => {
  console.log(`Page ${pageNumber} rendered successfully`)
}

// Page navigation handlers
const handlePreviousPage = () => {
  mainStore.previousPage()
}

const handleNextPage = () => {
  mainStore.nextPage()
}

const handleGoToPage = (page: number) => {
  // Handle special case for "go to last page" (-1)
  if (page === -1) {
    mainStore.goToPage(mainStore.totalPages)
  } else {
    mainStore.goToPage(page)
  }
}

// Zoom adjustment (kept for compatibility)
const adjustZoom = (delta: number) => {
  const newZoom = mainStore.zoomLevel + delta
  mainStore.setZoomLevel(newZoom)
}

// New handlers for vertical toolbar
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

// Document toolbar event handlers
const handleDocumentRotated = (documentId: number, angle: number) => {
  console.log(`Document ${documentId} rotated ${angle} degrees`)
}

const handleRedactionStarted = (documentId: number) => {
  console.log(`Redaction started for document ${documentId}`)
  redactionMode.value = true
}

const exitRedactionMode = () => {
  redactionMode.value = false
}

const handleRedactionAreasUpdated = (areas: any[]) => {
  console.log('Redaction areas updated:', areas)
}

const handleApplyRedactions = async (areas: any[]) => {
  if (!mainStore.selectedDocument) return
  
  try {
    await mainStore.redactDocument(mainStore.selectedDocument.id, areas, true)
    console.log('Redactions applied successfully')
  } catch (error) {
    console.error('Failed to apply redactions:', error)
  }
}

const handleDocumentSplit = (documentId: number) => {
  console.log(`Document split requested for document ${documentId}`)
  // TODO: Implement document splitting
}

const handleDocumentStacked = (documentId: number) => {
  console.log(`Document stack requested for document ${documentId}`)
  // TODO: Implement document stacking
}

// Document change handling is now in parent AppShell component

</script>

<style scoped>
.pdf-canvas-container {
  display: flex;
  justify-content: center;
  align-items: flex-start;
  min-height: 100%;
  padding: 20px;
}

.pdf-canvas {
  transform-origin: top center;
  transition: transform 0.2s ease-in-out;
}
</style>
