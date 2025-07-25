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
              :document-url="mainStore.documentUrl"
              :page-number="mainStore.currentPage"
              :zoom-level="mainStore.zoomLevel"
              @loaded="handleDocumentLoaded"
              @error="handleDocumentError"
              @page-rendered="handlePageRendered"
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

          <!-- Page Navigation -->
          <div class="bg-gray-900 px-4 py-2 flex items-center justify-between text-white">
            <!-- Navigation Controls -->
            <div class="flex items-center space-x-2">
              <button
                :disabled="mainStore.currentPage <= 1"
                :class="[
                  'p-1 rounded text-xs',
                  {
                    'bg-gray-700 hover:bg-gray-600': mainStore.currentPage > 1,
                    'bg-gray-800 text-gray-500 cursor-not-allowed': mainStore.currentPage <= 1
                  }
                ]"
                @click="mainStore.previousPage()"
              >
                <ChevronLeftIcon class="h-4 w-4" />
              </button>
              
              <span class="text-sm px-2">
                Page {{ mainStore.currentPage }} of {{ mainStore.totalPages }}
              </span>
              
              <button
                :disabled="mainStore.currentPage >= mainStore.totalPages"
                :class="[
                  'p-1 rounded text-xs',
                  {
                    'bg-gray-700 hover:bg-gray-600': mainStore.currentPage < mainStore.totalPages,
                    'bg-gray-800 text-gray-500 cursor-not-allowed': mainStore.currentPage >= mainStore.totalPages
                  }
                ]"
                @click="mainStore.nextPage()"
              >
                <ChevronRightIcon class="h-4 w-4" />
              </button>
            </div>

            <!-- Zoom Controls -->
            <div class="flex items-center space-x-2 text-sm">
              <button
                class="p-1 bg-gray-700 hover:bg-gray-600 rounded text-xs"
                @click="adjustZoom(-25)"
              >
                <MinusIcon class="h-4 w-4" />
              </button>
              
              <span>{{ mainStore.zoomLevel }}%</span>
              
              <button
                class="p-1 bg-gray-700 hover:bg-gray-600 rounded text-xs"
                @click="adjustZoom(25)"
              >
                <PlusIcon class="h-4 w-4" />
              </button>
              
              <button
                class="px-2 py-1 bg-gray-700 hover:bg-gray-600 rounded text-xs"
                @click="mainStore.setZoomLevel(100)"
              >
                Fit
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Document Thumbnail Strip (when in thumbnail mode) -->
      <div 
        v-if="mainStore.documentViewMode === 'thumbnail' && mainStore.selectedDocument"
        class="w-48 bg-gray-100 border-l border-gray-300 overflow-y-auto thumbnail-strip"
      >
        <div class="p-2 space-y-2">
          <div
            v-for="pageNum in mainStore.totalPages"
            :key="pageNum"
            :class="[
              'thumbnail bg-white border-2 cursor-pointer transition-all',
              {
                'active border-hydra-600': pageNum === mainStore.currentPage,
                'border-transparent hover:border-gray-300': pageNum !== mainStore.currentPage
              }
            ]"
            @click="mainStore.goToPage(pageNum)"
          >
            <!-- Actual thumbnail -->
            <div class="aspect-[8.5/11] relative overflow-hidden">
              <img
                v-if="thumbnails[pageNum - 1]"
                :src="thumbnails[pageNum - 1]"
                :alt="`Page ${pageNum}`"
                class="w-full h-full object-contain"
              />
              <div 
                v-else-if="loadingThumbnails"
                class="w-full h-full flex items-center justify-center text-gray-400"
              >
                <div class="text-center">
                  <div class="spinner-sm mb-1 border-gray-400 border-t-transparent"></div>
                  <span class="text-xs">{{ pageNum }}</span>
                </div>
              </div>
              <div 
                v-else
                class="w-full h-full flex items-center justify-center text-gray-400"
              >
                <div class="text-center">
                  <DocumentIcon class="h-6 w-6 mx-auto mb-1" />
                  <span class="text-xs">{{ pageNum }}</span>
                </div>
              </div>
              
              <!-- Page number overlay -->
              <div class="absolute bottom-1 right-1 bg-black bg-opacity-60 text-white text-xs px-1 rounded">
                {{ pageNum }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Document Tools Bar -->
    <DocumentToolbar
      :selected-document="mainStore.selectedDocument"
      @document-rotated="handleDocumentRotated"
      @redaction-started="handleRedactionStarted"
      @document-split="handleDocumentSplit"
      @document-stacked="handleDocumentStacked"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { useMainStore } from '@/stores/main'
import PDFViewer from '@/components/viewer/PDFViewer.vue'
import DocumentToolbar from '@/components/tools/DocumentToolbar.vue'
import RedactionOverlay from '@/components/tools/RedactionOverlay.vue'
import { pdfService } from '@/services/pdf.service'
import {
  DocumentIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  MinusIcon,
  PlusIcon
} from '@heroicons/vue/24/outline'

const mainStore = useMainStore()

// Thumbnail state
const thumbnails = ref<string[]>([])
const loadingThumbnails = ref(false)
const pdfViewerRef = ref<InstanceType<typeof PDFViewer>>()

// Redaction state
const redactionMode = ref(false)

// Generate thumbnails for all pages
const generateThumbnails = async () => {
  if (!pdfViewerRef.value || !mainStore.documentUrl) {
    return
  }

  const currentPdf = pdfViewerRef.value.getCurrentPdf()
  if (!currentPdf) {
    return
  }

  loadingThumbnails.value = true
  thumbnails.value = []

  try {
    const generatedThumbnails = await pdfService.generateAllThumbnails(currentPdf, {
      scale: 0.15,
      maxWidth: 150,
      maxHeight: 200,
      quality: 0.7
    })
    
    thumbnails.value = generatedThumbnails
  } catch (error) {
    console.error('Failed to generate thumbnails:', error)
    thumbnails.value = []
  } finally {
    loadingThumbnails.value = false
  }
}

// PDF viewer event handlers
const handleDocumentLoaded = async (pageCount: number) => {
  // Update total pages in store
  mainStore.totalPages = pageCount
  console.log(`Document loaded with ${pageCount} pages`)

  // Generate thumbnails when document is loaded and in thumbnail mode
  await nextTick()
  if (mainStore.documentViewMode === 'thumbnail') {
    generateThumbnails()
  }
}

const handleDocumentError = (error: string) => {
  mainStore.setError(`Failed to load document: ${error}`)
}

const handlePageRendered = (pageNumber: number) => {
  console.log(`Page ${pageNumber} rendered successfully`)
}

// Zoom adjustment
const adjustZoom = (delta: number) => {
  const newZoom = mainStore.zoomLevel + delta
  mainStore.setZoomLevel(newZoom)
}

// Document toolbar event handlers
const handleDocumentRotated = (documentId: number, angle: number) => {
  console.log(`Document ${documentId} rotated ${angle} degrees`)
  
  // Regenerate thumbnails after rotation
  if (mainStore.documentViewMode === 'thumbnail') {
    // Small delay to allow document to reload
    setTimeout(() => {
      generateThumbnails()
    }, 500)
  }
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

// Watch for view mode changes to generate thumbnails
watch(() => mainStore.documentViewMode, (newMode) => {
  if (newMode === 'thumbnail' && mainStore.selectedDocument && thumbnails.value.length === 0) {
    generateThumbnails()
  }
})

// Watch for document changes to clear thumbnails
watch(() => mainStore.selectedDocument, () => {
  thumbnails.value = []
  loadingThumbnails.value = false
})
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

.thumbnail-strip {
  scrollbar-width: thin;
  scrollbar-color: #cbd5e1 #f1f5f9;
}

.thumbnail-strip::-webkit-scrollbar {
  width: 6px;
}

.thumbnail-strip::-webkit-scrollbar-track {
  background: #f1f5f9;
}

.thumbnail-strip::-webkit-scrollbar-thumb {
  background: #cbd5e1;
  border-radius: 3px;
}

.thumbnail-strip::-webkit-scrollbar-thumb:hover {
  background: #94a3b8;
}

.spinner-sm {
  width: 16px;
  height: 16px;
  border: 2px solid transparent;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
</style>