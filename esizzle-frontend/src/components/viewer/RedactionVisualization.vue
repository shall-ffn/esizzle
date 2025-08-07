<template>
  <div 
    v-if="editable || visibleRedactions.length > 0"
    class="absolute inset-0 redaction-overlay"
    :class="{ 'drawing-mode': editable }"
    @mousedown="handleMouseDown"
    @mousemove="handleMouseMove"
    @mouseup="handleMouseUp"
    @mouseleave="handleMouseLeave"
  >
    <!-- Existing redaction areas -->
    <div
      v-for="redaction in visibleRedactions"
      :key="redaction.id"
      :class="getRedactionClass(redaction)"
      :style="getRedactionStyle(redaction)"
      @click="handleRedactionClick(redaction)"
      @contextmenu="handleContextMenu($event, redaction)"
    >
      <!-- Redaction controls for pending redactions -->
      <div 
        v-if="editable && !redaction.applied && !redaction.deleted"
        class="redaction-controls absolute -top-10 left-0 flex items-center space-x-2 bg-white border border-gray-300 rounded-lg px-2 py-1 shadow-lg z-10"
      >
        <button
          @click.stop="applyRedaction(redaction)"
          class="text-xs bg-red-600 text-white px-2 py-1 rounded hover:bg-red-700 transition-colors"
          title="Apply redaction"
        >
          Apply
        </button>
        <button
          @click.stop="removeRedaction(redaction.id)"
          class="text-xs bg-gray-500 text-white px-2 py-1 rounded hover:bg-gray-600 transition-colors"
          title="Remove redaction"
        >
          Remove
        </button>
        <input
          v-if="redaction.text !== undefined"
          v-model="redaction.text"
          @click.stop
          class="text-xs border border-gray-300 rounded px-1 py-0.5 w-20"
          placeholder="Note..."
          title="Optional note"
        />
      </div>

      <!-- Applied redaction indicator -->
      <div 
        v-if="redaction.applied"
        class="applied-indicator absolute top-1 right-1 bg-black text-white text-xs px-1 rounded"
      >
        ✓
      </div>
    </div>

    <!-- Current drawing area -->
    <div
      v-if="currentDrawing && editable"
      :class="['redaction-drawing', 'absolute', 'bg-yellow-400', 'bg-opacity-30', 'border-2', 'border-dashed', 'border-yellow-600']"
      :style="getCurrentDrawingStyle()"
    />

    <!-- Instructions overlay for edit mode -->
    <div 
      v-if="editable && showInstructions"
      class="instructions-overlay absolute top-4 left-4 bg-black bg-opacity-75 text-white p-3 rounded-lg max-w-xs z-20"
    >
      <div class="text-sm">
        <p class="font-semibold mb-2">Redaction Mode Active</p>
        <ul class="text-xs space-y-1">
          <li>• Click and drag to create redaction areas</li>
          <li>• Yellow = pending, Black = applied</li>
          <li>• Click "Apply" to make permanent</li>
          <li>• Right-click for more options</li>
        </ul>
      </div>
      <button
        @click="showInstructions = false"
        class="mt-3 px-2 py-1 bg-gray-600 text-white text-xs rounded hover:bg-gray-700 transition-colors"
      >
        Got it
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import type { 
  RedactionAnnotation, 
  Point, 
  Rectangle 
} from '@/types/manipulation'
import { CoordinateTranslator, CoordinateUtils } from '@/utils/coordinate-translator'

interface Props {
  redactions: RedactionAnnotation[]
  pageNumber: number
  editable: boolean
  coordinateTranslator: CoordinateTranslator | null
  zoomLevel: number
}

interface Emits {
  (e: 'redaction-added', redaction: Omit<RedactionAnnotation, 'id' | 'dateCreated'>): void
  (e: 'redaction-updated', redactionId: number, updates: Partial<RedactionAnnotation>): void
  (e: 'redaction-removed', redactionId: number): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const isDrawing = ref(false)
const startPoint = ref<Point | null>(null)
const currentDrawing = ref<Rectangle | null>(null)
const showInstructions = ref(true)

// Computed properties
const visibleRedactions = computed(() => 
  props.redactions.filter(r => r.pageNumber === props.pageNumber && !r.deleted)
)

// Drawing handlers
const handleMouseDown = (event: MouseEvent) => {
  if (!props.editable || !props.coordinateTranslator) return
  
  event.preventDefault()
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  const canvasPoint: Point = {
    x: event.clientX - rect.left,
    y: event.clientY - rect.top
  }
  
  // Convert to page coordinates
  const pagePoint = props.coordinateTranslator.canvasToPage(canvasPoint)
  
  startPoint.value = pagePoint
  isDrawing.value = true
  
  currentDrawing.value = {
    x: pagePoint.x,
    y: pagePoint.y,
    width: 0,
    height: 0
  }
}

const handleMouseMove = (event: MouseEvent) => {
  if (!isDrawing.value || !startPoint.value || !props.coordinateTranslator) return
  
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  const canvasPoint: Point = {
    x: event.clientX - rect.left,
    y: event.clientY - rect.top
  }
  
  // Convert to page coordinates
  const pagePoint = props.coordinateTranslator.canvasToPage(canvasPoint)
  
  // Calculate drawing rectangle
  currentDrawing.value = CoordinateUtils.rectangleFromPoints(startPoint.value, pagePoint)
}

const handleMouseUp = () => {
  if (!isDrawing.value || !currentDrawing.value || !props.coordinateTranslator) return
  
  // Check minimum size
  const minSize = props.coordinateTranslator.getMinimumSize()
  if (currentDrawing.value.width < minSize.width || currentDrawing.value.height < minSize.height) {
    resetDrawing()
    return
  }
  
  // Create new redaction
  const newRedaction: Omit<RedactionAnnotation, 'id' | 'dateCreated'> = {
    imageId: 0, // Will be set by parent
    pageNumber: props.pageNumber,
    pageX: currentDrawing.value.x,
    pageY: currentDrawing.value.y,
    pageWidth: currentDrawing.value.width,
    pageHeight: currentDrawing.value.height,
    guid: crypto.randomUUID(),
    text: '',
    applied: false,
    drawOrientation: 0,
    createdBy: 0, // Will be set by parent
    deleted: false
  }
  
  emit('redaction-added', newRedaction)
  resetDrawing()
}

const handleMouseLeave = () => {
  resetDrawing()
}

// Redaction management
const handleRedactionClick = (redaction: RedactionAnnotation) => {
  if (!props.editable) return
  
  console.log('Redaction clicked:', redaction)
  // Could implement selection/editing here
}

const handleContextMenu = (event: MouseEvent, redaction: RedactionAnnotation) => {
  if (!props.editable) return
  
  event.preventDefault()
  // Could implement context menu here
  console.log('Redaction context menu:', redaction)
}

const applyRedaction = (redaction: RedactionAnnotation) => {
  emit('redaction-updated', redaction.id, { applied: true })
}

const removeRedaction = (redactionId: number) => {
  emit('redaction-removed', redactionId)
}

// Style calculations
const getRedactionClass = (redaction: RedactionAnnotation): string => {
  const baseClasses = ['absolute', 'border-2', 'cursor-pointer', 'transition-all']
  
  if (redaction.applied) {
    return [...baseClasses, 'redaction-applied', 'bg-black', 'border-black'].join(' ')
  } else {
    return [...baseClasses, 'redaction-pending', 'bg-yellow-500', 'bg-opacity-50', 'border-black', 'hover:bg-opacity-70'].join(' ')
  }
}

const getRedactionStyle = (redaction: RedactionAnnotation) => {
  if (!props.coordinateTranslator) return {}
  
  const canvasRect = props.coordinateTranslator.rectanglePageToCanvas({
    x: redaction.pageX,
    y: redaction.pageY,
    width: redaction.pageWidth,
    height: redaction.pageHeight
  })
  
  return {
    left: `${canvasRect.x}px`,
    top: `${canvasRect.y}px`,
    width: `${canvasRect.width}px`,
    height: `${canvasRect.height}px`,
    zIndex: redaction.applied ? 15 : 10
  }
}

const getCurrentDrawingStyle = () => {
  if (!currentDrawing.value || !props.coordinateTranslator) return {}
  
  const canvasRect = props.coordinateTranslator.rectanglePageToCanvas(currentDrawing.value)
  
  return {
    left: `${canvasRect.x}px`,
    top: `${canvasRect.y}px`,
    width: `${canvasRect.width}px`,
    height: `${canvasRect.height}px`,
    zIndex: 25
  }
}

// Utility functions
const resetDrawing = () => {
  isDrawing.value = false
  startPoint.value = null
  currentDrawing.value = null
}

// Lifecycle
onMounted(() => {
  // Auto-hide instructions after 5 seconds
  setTimeout(() => {
    showInstructions.value = false
  }, 5000)
})

onUnmounted(() => {
  resetDrawing()
})
</script>

<style scoped>
.redaction-overlay {
  cursor: crosshair;
  pointer-events: auto;
}

.redaction-overlay.drawing-mode {
  cursor: crosshair;
}

/* Applied redactions - black overlay matching Hydra original */
.redaction-applied {
  background-color: #000000;
  opacity: 1.0;
  border: 2px solid #000000;
  pointer-events: none;
}

/* Pending redactions - yellow semi-transparent matching Hydra original */
.redaction-pending {
  background-color: rgba(255, 255, 0, 0.5);
  border: 2px solid #000000;
  pointer-events: auto;
}

/* Drawing redactions - yellow dashed */
.redaction-drawing {
  background-color: rgba(255, 255, 0, 0.3);
  border: 2px dashed #ca8a04;
  pointer-events: none;
}

/* Control styling */
.redaction-controls {
  opacity: 0;
  transition: opacity 0.2s ease-in-out;
}

.redaction-pending:hover .redaction-controls {
  opacity: 1;
}

/* Instructions overlay */
.instructions-overlay {
  animation: fadeInUp 0.3s ease-out;
}

@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Applied indicator */
.applied-indicator {
  font-size: 10px;
  line-height: 1;
}

/* Hover effects */
.redaction-pending:hover {
  box-shadow: 0 0 0 2px rgba(255, 255, 0, 0.8);
}

/* Transitions */
.transition-all {
  transition: all 0.2s ease-in-out;
}
</style>
