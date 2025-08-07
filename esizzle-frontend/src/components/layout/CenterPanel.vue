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
      <div class="flex-1 flex flex-col bg-gray-700 relative" :style="{ minWidth: mainStore.selectedDocument ? '60%' : '100%' }">
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

      <!-- Thumbnail Panel (right side of center area) -->
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
import { ref, computed, watch } from 'vue'
import { useMainStore } from '@/stores/main'
import { useIndexingStore } from '@/stores/indexing'
import PDFViewer from '@/components/viewer/PDFViewer.vue'
import DocumentToolbar from '@/components/tools/DocumentToolbar.vue'
import RedactionOverlay from '@/components/tools/RedactionOverlay.vue'
import ThumbnailView from '@/components/indexing/ThumbnailView.vue'
import { pdfService } from '@/services/pdf.service'
import type { PageThumbnailDto } from '@/types/indexing'
import {
  DocumentIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  MinusIcon,
  PlusIcon
} from '@heroicons/vue/24/outline'

const mainStore = useMainStore()
const indexingStore = useIndexingStore()

// Component refs
const pdfViewerRef = ref<InstanceType<typeof PDFViewer>>()

// Redaction state
const redactionMode = ref(false)

// Thumbnail management
const thumbnails = ref<PageThumbnailDto[]>([])
const thumbnailsLoading = ref(false)

// Generate thumbnails for the loaded PDF document
const generateThumbnails = async () => {
  if (!mainStore.documentUrl || !mainStore.totalPages) return
  
  thumbnailsLoading.value = true
  try {
    // Load the PDF document
    const pdf = await pdfService.loadDocument(mainStore.documentUrl)
    
    // Generate thumbnails with appropriate sizing for 120px width and shorter height to match legacy interface
    const thumbnailUrls = await pdfService.generateAllThumbnails(pdf, {
      scale: 0.4,
      maxWidth: 120,
      maxHeight: 102, // Reduced height to match 85% aspect ratio (120 * 0.85)
      quality: 0.8
    })
    
    // Create PageThumbnailDto objects
    const thumbnailDtos: PageThumbnailDto[] = thumbnailUrls.map((url, index) => ({
      pageNumber: index + 1,
      thumbnailUrl: url,
      width: 120,
      height: 102, // Updated to match the new aspect ratio
      hasBookmark: false, // Will be updated by ThumbnailView component based on bookmarks
      bookmarkType: undefined,
      documentTypeName: undefined
    }))
    
    thumbnails.value = thumbnailDtos
    console.log(`Generated ${thumbnailDtos.length} thumbnails`)
    
  } catch (error) {
    console.error('Failed to generate thumbnails:', error)
    thumbnails.value = []
  } finally {
    thumbnailsLoading.value = false
  }
}

// PDF viewer event handlers
const handleDocumentLoaded = async (pageCount: number) => {
  // Update total pages in store
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

// Zoom adjustment
const adjustZoom = (delta: number) => {
  const newZoom = mainStore.zoomLevel + delta
  mainStore.setZoomLevel(newZoom)
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

// Watch for document changes to regenerate thumbnails
watch(() => mainStore.selectedDocument, async (newDoc) => {
  if (newDoc && mainStore.documentUrl) {
    thumbnails.value = []
    await generateThumbnails()
  } else {
    thumbnails.value = []
  }
})

// Thumbnail event handlers
const handlePageSelected = (pageNumber: number) => {
  mainStore.currentPage = pageNumber
}

const handleThumbnailLoaded = (pageNumber: number) => {
  // Handle thumbnail loading completion
  console.log('Thumbnail loaded for page:', pageNumber)
}

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
