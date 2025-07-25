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
      v-else-if="error" 
      class="absolute inset-0 flex items-center justify-center text-white"
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

    <!-- PDF Canvas -->
    <div 
      v-else-if="documentUrl"
      class="h-full w-full overflow-auto"
      ref="containerRef"
    >
      <div class="flex justify-center p-4">
        <canvas
          ref="canvasRef"
          class="pdf-canvas shadow-lg border border-gray-300 bg-white"
          :style="{ 
            transform: `scale(${zoomLevel / 100})`,
            transformOrigin: 'top center'
          }"
        />
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
import { ref, watch, onMounted, onUnmounted, nextTick } from 'vue'
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
const currentPdf = ref<PDFDocumentProxy | null>(null)
const isRendering = ref(false)

// Load and render PDF document
const loadDocument = async () => {
  if (!props.documentUrl) {
    currentPdf.value = null
    return
  }

  loading.value = true
  loadingMessage.value = 'Loading document...'
  error.value = ''

  try {
    // Load PDF document
    const pdf = await pdfService.loadDocument(props.documentUrl)
    currentPdf.value = pdf

    // Emit loaded event with page count
    emit('loaded', pdf.numPages)

    // Render the current page
    await renderCurrentPage()

  } catch (err) {
    const errorMessage = err instanceof Error ? err.message : 'Unknown error occurred'
    error.value = errorMessage
    emit('error', errorMessage)
    console.error('Failed to load PDF:', err)
  } finally {
    loading.value = false
  }
}

// Render the current page
const renderCurrentPage = async () => {
  if (!currentPdf.value || !canvasRef.value || isRendering.value) {
    return
  }

  isRendering.value = true
  loadingMessage.value = `Rendering page ${props.pageNumber}...`

  try {
    const scale = props.zoomLevel / 100

    await pdfService.renderPage(
      currentPdf.value,
      props.pageNumber,
      canvasRef.value,
      {
        scale,
        rotation: props.rotation
      }
    )

    emit('pageRendered', props.pageNumber)

  } catch (err) {
    const errorMessage = err instanceof Error ? err.message : 'Failed to render page'
    error.value = errorMessage
    emit('error', errorMessage)
    console.error('Failed to render page:', err)
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
  loadDocument()
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

// Cleanup on unmount
onUnmounted(() => {
  if (props.documentUrl) {
    pdfService.cleanup(props.documentUrl)
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

.pdf-canvas {
  transition: transform 0.2s ease-in-out;
  max-width: none;
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