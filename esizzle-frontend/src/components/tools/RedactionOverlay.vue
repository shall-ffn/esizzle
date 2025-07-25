<template>
  <div 
    v-if="active"
    class="absolute inset-0 z-30 cursor-crosshair"
    @mousedown="startDrawing"
    @mousemove="updateDrawing"
    @mouseup="finishDrawing"
    @mouseleave="cancelDrawing"
  >
    <!-- Redaction Areas -->
    <div
      v-for="(area, index) in redactionAreas"
      :key="index"
      :class="[
        'absolute border-2 transition-all',
        {
          'bg-black': area.permanent,
          'bg-red-500 bg-opacity-50 border-red-600': !area.permanent,
          'animate-pulse': area.temporary
        }
      ]"
      :style="{
        left: `${area.x}px`,
        top: `${area.y}px`,
        width: `${area.width}px`,
        height: `${area.height}px`
      }"
    >
      <!-- Redaction controls -->
      <div 
        v-if="!area.permanent"
        class="absolute -top-8 left-0 flex items-center space-x-1 bg-white border border-gray-300 rounded px-2 py-1 shadow-lg"
      >
        <button
          @click="makeAreaPermanent(index)"
          class="text-xs bg-red-600 text-white px-2 py-1 rounded hover:bg-red-700"
          title="Make permanent"
        >
          Apply
        </button>
        <button
          @click="removeArea(index)"
          class="text-xs bg-gray-500 text-white px-2 py-1 rounded hover:bg-gray-600"
          title="Remove"
        >
          Remove
        </button>
      </div>
    </div>

    <!-- Current drawing area -->
    <div
      v-if="currentArea"
      class="absolute bg-red-500 bg-opacity-30 border-2 border-red-600 border-dashed"
      :style="{
        left: `${currentArea.x}px`,
        top: `${currentArea.y}px`,
        width: `${currentArea.width}px`,
        height: `${currentArea.height}px`
      }"
    />

    <!-- Instructions overlay -->
    <div 
      v-if="showInstructions"
      class="absolute top-4 left-4 bg-black bg-opacity-75 text-white p-3 rounded-lg max-w-xs"
    >
      <div class="text-sm">
        <p class="font-semibold mb-2">Redaction Mode Active</p>
        <ul class="text-xs space-y-1">
          <li>• Click and drag to mark areas for redaction</li>
          <li>• Click "Apply" to make redaction permanent</li>
          <li>• Click "Remove" to delete redaction area</li>
          <li>• Press ESC to exit redaction mode</li>
        </ul>
      </div>
      <button
        @click="showInstructions = false"
        class="mt-3 px-2 py-1 bg-gray-600 text-white text-xs rounded hover:bg-gray-700"
      >
        Got it
      </button>
    </div>

    <!-- Exit button -->
    <div class="absolute top-4 right-4">
      <button
        @click="exitRedactionMode"
        class="bg-red-600 text-white px-4 py-2 rounded-lg shadow-lg hover:bg-red-700 transition-colors"
      >
        Exit Redaction Mode
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

interface RedactionArea {
  x: number
  y: number
  width: number
  height: number
  pageNumber: number
  permanent: boolean
  temporary?: boolean
}

interface Props {
  active: boolean
  documentId: number
  currentPage: number
}

interface Emits {
  (e: 'exit'): void
  (e: 'areas-updated', areas: RedactionArea[]): void
  (e: 'apply-redactions', areas: RedactionArea[]): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Component state
const redactionAreas = ref<RedactionArea[]>([])
const currentArea = ref<RedactionArea | null>(null)
const isDrawing = ref(false)
const startPoint = ref<{ x: number; y: number } | null>(null)
const showInstructions = ref(true)

// Drawing functions
const startDrawing = (event: MouseEvent) => {
  if (!props.active) return
  
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  const x = event.clientX - rect.left
  const y = event.clientY - rect.top
  
  startPoint.value = { x, y }
  isDrawing.value = true
  
  currentArea.value = {
    x,
    y,
    width: 0,
    height: 0,
    pageNumber: props.currentPage,
    permanent: false
  }
}

const updateDrawing = (event: MouseEvent) => {
  if (!isDrawing.value || !startPoint.value || !currentArea.value) return
  
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  const currentX = event.clientX - rect.left
  const currentY = event.clientY - rect.top
  
  const width = Math.abs(currentX - startPoint.value.x)
  const height = Math.abs(currentY - startPoint.value.y)
  const x = Math.min(currentX, startPoint.value.x)
  const y = Math.min(currentY, startPoint.value.y)
  
  currentArea.value = {
    ...currentArea.value,
    x,
    y,
    width,
    height
  }
}

const finishDrawing = () => {
  if (!isDrawing.value || !currentArea.value) return
  
  // Only add areas that have meaningful size
  if (currentArea.value.width > 5 && currentArea.value.height > 5) {
    redactionAreas.value.push({ ...currentArea.value })
    emit('areas-updated', [...redactionAreas.value])
  }
  
  currentArea.value = null
  isDrawing.value = false
  startPoint.value = null
}

const cancelDrawing = () => {
  currentArea.value = null
  isDrawing.value = false
  startPoint.value = null
}

// Area management
const makeAreaPermanent = async (index: number) => {
  const area = redactionAreas.value[index]
  if (!area) return
  
  try {
    // Mark as temporary while processing
    area.temporary = true
    
    // Apply redaction via parent component
    emit('apply-redactions', [area])
    
    // Mark as permanent
    area.permanent = true
    area.temporary = false
    
    emit('areas-updated', [...redactionAreas.value])
  } catch (error) {
    area.temporary = false
    console.error('Failed to apply redaction:', error)
  }
}

const removeArea = (index: number) => {
  redactionAreas.value.splice(index, 1)
  emit('areas-updated', [...redactionAreas.value])
}

const exitRedactionMode = () => {
  // Clear any pending areas
  currentArea.value = null
  isDrawing.value = false
  startPoint.value = null
  
  emit('exit')
}

// Keyboard shortcuts
const handleKeyDown = (event: KeyboardEvent) => {
  if (event.key === 'Escape') {
    exitRedactionMode()
  }
}

// Lifecycle
onMounted(() => {
  document.addEventListener('keydown', handleKeyDown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeyDown)
})

// Reset areas when document changes
const resetAreas = () => {
  redactionAreas.value = []
  currentArea.value = null
  isDrawing.value = false
  startPoint.value = null
  showInstructions.value = true
}

// Expose methods
defineExpose({
  resetAreas,
  getAreas: () => redactionAreas.value
})
</script>

<style scoped>
/* Ensure redaction overlay is above everything else */
.z-30 {
  z-index: 30;
}

/* Smooth transitions for redaction areas */
.transition-all {
  transition: all 0.2s ease;
}

/* Pulsing animation for temporary redactions */
@keyframes pulse {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.7;
  }
}

.animate-pulse {
  animation: pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}
</style>