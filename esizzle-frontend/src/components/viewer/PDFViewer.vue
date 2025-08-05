<template>
  <div class="pdf-viewer-container h-full w-full relative">
    <!-- Loading overlay -->
    <div 
      v-if="loading" 
      class="absolute inset-0 flex items-center justify-center bg-gray-800 bg-opacity-75 z-20"
    >
      <div class="text-center text-white">
        <div class="spinner mb-4 border-white border-t-transparent"></div>
        <p class="text-sm">{{ loadingMessage }}</p>
      </div>
    </div>

    <!-- Error state -->
    <div 
      v-if="error" 
      class="absolute inset-0 flex items-center justify-center text-white z-10"
    >
      <div class="text-center">
        <ExclamationTriangleIcon class="h-16 w-16 mx-auto mb-4 text-red-400" />
        <p class="text-lg font-medium mb-2">Error Loading Document</p>
        <p class="text-sm text-gray-300 mb-4">{{ error }}</p>
        <button 
          @click="reload"
          class="px-4 py-2 bg-hydra-600 text-white rounded hover:bg-hydra-700 transition-colors"
        >
          Try Again
        </button>
      </div>
    </div>

    <!-- PDF Canvas - Always present when documentUrl exists -->
    <div 
      v-if="documentUrl"
      class="h-full w-full overflow-auto"
      ref="containerRef"
      :class="{ 'opacity-0': loading || error }"
    >
      <div class="flex justify-center p-4">
        <div class="pdf-page-container relative" :style="{ 
          transform: `scale(${zoomLevel / 100})`,
          transformOrigin: 'top center'
        }">
          <canvas
            ref="canvasRef"
            class="pdf-canvas shadow-lg border border-gray-300 bg-white block"
            width="800"
            height="1000"
          />
        </div>
      </div>
    </div>

    <!-- No document state -->
    <div 
      v-else 
      class="absolute inset-0 flex items-center justify-center text-gray-400"
    >
      <div class="text-center">
        <DocumentIcon class="h-16 w-16 mx-auto mb-4 opacity-50" />
        <p class="text-lg font-medium">No Document Selected</p>
        <p class="text-sm mt-2">Select a document to view it here</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted, nextTick, markRaw } from 'vue'
import type { PDFDocumentProxy } from 'pdfjs-dist'
import { pdfService } from '@/services/pdf.service'
import { DocumentIcon, ExclamationTriangleIcon } from '@heroicons/vue/24/outline'

interface Props {
  documentUrl?: string
  pageNumber?: number
  zoomLevel?: number
  rotation?: number
}

interface Emits {
  (e: 'loaded', pages: number): void
  (e: 'error', error: string): void
  (e: 'pageRendered', page: number): void
}

const props = withDefaults(defineProps<Props>(), {
  pageNumber: 1,
  zoomLevel: 100,
  rotation: 0
})

const emit = defineEmits<Emits>()

// Refs
const canvasRef = ref<HTMLCanvasElement>()
const containerRef = ref<HTMLDivElement>()

// State
const loading = ref(false)
const loadingMessage = ref('')
const error = ref('')
// Use markRaw to prevent Vue from making PDF.js objects reactive
const currentPdf = ref<PDFDocumentProxy | null>(null)
const isRendering = ref(false)

// Load and render PDF document with detailed debugging
const loadDocument = async () => {
  if (!props.documentUrl) {
    console.log('PDFViewer: No document URL provided')
    currentPdf.value = null
    return
  }

  console.log('PDFViewer: Starting document load:', props.documentUrl)
  loading.value = true
  loadingMessage.value = 'Loading document...'
  error.value = ''

  try {
    // Load PDF document
    console.log('PDFViewer: Calling pdfService.loadDocument')
    const pdf = await pdfService.loadDocument(props.documentUrl)
    console.log('PDFViewer: PDF loaded successfully, pages:', pdf.numPages)
    
    // Debug: Log PDF document info
    console.log('PDFViewer: PDF document info:', {
      numPages: pdf.numPages,
      fingerprint: pdf.fingerprint || 'N/A'
    })
    
    // Use markRaw to prevent Vue reactivity interference with PDF.js objects
    currentPdf.value = markRaw(pdf)

    // Emit loaded event with page count
    emit('loaded', pdf.numPages)

    // Wait for next tick to ensure DOM is updated
    await nextTick()
    console.log('PDFViewer: DOM updated, checking canvas availability')
    
    // Canvas should be available immediately now since it's not conditional
    if (!canvasRef.value) {
      console.error('PDFViewer: Canvas ref still not available after DOM update')
      throw new Error('Canvas ref not available - template rendering issue')
    }

    console.log('PDFViewer: Canvas ref is available, proceeding with render')
    // Render the current page
    await renderCurrentPage()

  } catch (err) {
    const errorMessage = err instanceof Error ? err.message : 'Unknown error occurred'
    console.error('PDFViewer: Failed to load PDF:', err)
    console.error('PDFViewer: Error details:', {
      message: errorMessage,
      stack: err instanceof Error ? err.stack : undefined,
      documentUrl: props.documentUrl
    })
    error.value = errorMessage
    emit('error', errorMessage)
  } finally {
    loading.value = false
  }
}

// Render the current page with comprehensive debugging
const renderCurrentPage = async () => {
  console.log('PDFViewer: renderCurrentPage called', {
    hasPdf: !!currentPdf.value,
    hasCanvas: !!canvasRef.value,
    isRendering: isRendering.value,
    pageNumber: props.pageNumber
  })

  if (!currentPdf.value) {
    console.warn('PDFViewer: No PDF document loaded')
    return
  }

  if (!canvasRef.value) {
    console.warn('PDFViewer: Canvas ref not available')
    return
  }

  if (isRendering.value) {
    console.warn('PDFViewer: Already rendering, skipping')
    return
  }

  isRendering.value = true
  loadingMessage.value = `Rendering page ${props.pageNumber}...`

  try {
    const canvas = canvasRef.value
    console.log('PDFViewer: Canvas element details:', {
      tagName: canvas.tagName,
      offsetWidth: canvas.offsetWidth,
      offsetHeight: canvas.offsetHeight,
      offsetParent: !!canvas.offsetParent,
      clientWidth: canvas.clientWidth,
      clientHeight: canvas.clientHeight,
      style: canvas.style.cssText
    })
    
    // Wait for canvas to be ready before rendering
    console.log('PDFViewer: Waiting for canvas to be ready')
    const isReady = await pdfService.waitForCanvasReady(canvas, 5000)
    if (!isReady) {
      throw new Error('Canvas not ready for rendering after 5 seconds')
    }
    
    console.log(`PDFViewer: Canvas ready for page ${props.pageNumber}, final dimensions: ${canvas.offsetWidth}x${canvas.offsetHeight}`)
    
    const scale = props.zoomLevel / 100
    console.log('PDFViewer: Rendering with scale:', scale)

    // Try primary render method first, with fallback on failure
    try {
      await pdfService.renderPage(
        currentPdf.value,
        props.pageNumber,
        canvas,
        {
          scale,
          rotation: props.rotation
        }
      )
    } catch (primaryError) {
      console.warn('PDFViewer: Primary render failed, trying fallback:', primaryError)
      loadingMessage.value = `Retrying page ${props.pageNumber} with fallback method...`
      
      await pdfService.renderPageWithFallback(
        currentPdf.value,
        props.pageNumber,
        canvas,
        {
          scale,
          rotation: props.rotation
        }
      )
    }

    console.log(`PDFViewer: Successfully rendered page ${props.pageNumber}`)
    emit('pageRendered', props.pageNumber)

  } catch (err) {
    const errorMessage = err instanceof Error ? err.message : 'Failed to render page'
    console.error('PDFViewer: Render error:', {
      error: err,
      message: errorMessage,
      pageNumber: props.pageNumber,
      zoomLevel: props.zoomLevel
    })
    error.value = errorMessage
    emit('error', errorMessage)
  } finally {
    isRendering.value = false
  }
}

// Reload document
const reload = () => {
  error.value = ''
  loadDocument()
}

// Fit page to container width
const fitToWidth = async () => {
  if (!currentPdf.value || !containerRef.value) return

  try {
    const page = await currentPdf.value.getPage(props.pageNumber)
    const containerWidth = containerRef.value.clientWidth
    const containerHeight = containerRef.value.clientHeight
    
    const optimalScale = pdfService.calculateFitScale(
      page,
      containerWidth,
      containerHeight
    )
    
    // Emit zoom change (parent component should handle this)
    console.log('Optimal scale:', optimalScale * 100)
    
  } catch (err) {
    console.error('Failed to calculate fit scale:', err)
  }
}

// Watch for prop changes
watch(() => props.documentUrl, () => {
  if (props.documentUrl) {
    console.log('PDFViewer: Document URL changed, loading new document:', props.documentUrl)
    loadDocument()
  } else {
    console.log('PDFViewer: Document URL cleared')
    currentPdf.value = null
  }
}, { immediate: true })

watch(() => props.pageNumber, () => {
  if (currentPdf.value) {
    renderCurrentPage()
  }
})

watch(() => props.zoomLevel, () => {
  if (currentPdf.value) {
    renderCurrentPage()
  }
})

watch(() => props.rotation, () => {
  if (currentPdf.value) {
    renderCurrentPage()
  }
})

// Watch for canvas ref availability
watch(canvasRef, (newCanvas) => {
  console.log('PDFViewer: Canvas ref changed:', !!newCanvas)
  if (newCanvas && currentPdf.value && !isRendering.value) {
    console.log('PDFViewer: Canvas now available, triggering render')
    renderCurrentPage()
  }
})

// Cleanup on unmount
onUnmounted(() => {
  console.log('PDFViewer: Component unmounting, cleaning up')
  if (props.documentUrl) {
    pdfService.cleanup(props.documentUrl)
  }
  currentPdf.value = null
})

// Handle component mounted
onMounted(() => {
  console.log('PDFViewer: Component mounted')
  if (props.documentUrl && !currentPdf.value) {
    console.log('PDFViewer: Document URL present on mount, loading')
    loadDocument()
  }
})

// Expose methods for parent component
defineExpose({
  reload,
  fitToWidth,
  isLoading: () => loading.value,
  hasError: () => !!error.value,
  getCurrentPdf: () => currentPdf.value
})
</script>

<style scoped>
.pdf-viewer-container {
  background: #374151; /* gray-700 */
}

.pdf-page-container {
  display: inline-block;
  transition: transform 0.2s ease-in-out;
}

.pdf-canvas {
  display: block;
  max-width: none;
  min-width: 200px;
  min-height: 260px;
}

/* Custom scrollbar for PDF container */
.pdf-viewer-container::-webkit-scrollbar {
  width: 12px;
  height: 12px;
}

.pdf-viewer-container::-webkit-scrollbar-track {
  background: #1f2937; /* gray-800 */
}

.pdf-viewer-container::-webkit-scrollbar-thumb {
  background: #4b5563; /* gray-600 */
  border-radius: 6px;
}

.pdf-viewer-container::-webkit-scrollbar-thumb:hover {
  background: #6b7280; /* gray-500 */
}

.pdf-viewer-container::-webkit-scrollbar-corner {
  background: #1f2937; /* gray-800 */
}
</style>