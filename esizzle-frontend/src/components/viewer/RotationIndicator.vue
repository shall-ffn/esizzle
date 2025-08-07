<template>
  <div 
    v-if="editable || rotation !== 0"
    class="absolute inset-0 rotation-overlay"
    :class="{ 'editing-mode': editable }"
  >
    <!-- Rotation indicator badge -->
    <div 
      v-if="rotation !== 0 || editable"
      class="rotation-indicator absolute top-2 right-2 bg-blue-600 text-white px-2 py-1 rounded-full text-xs font-bold shadow-lg z-20"
      :class="{ 'clickable': editable }"
      @click="editable && handleIndicatorClick()"
    >
      {{ rotation }}°
      <ArrowPathIcon v-if="editable" class="inline h-3 w-3 ml-1" />
    </div>

    <!-- Rotation controls for editing mode -->
    <div 
      v-if="editable"
      class="rotation-controls absolute top-12 right-2 bg-white border border-gray-300 rounded-lg p-3 shadow-lg z-20"
      :class="{ 'visible': showControls }"
    >
      <div class="text-xs font-medium text-gray-700 mb-2">
        Page {{ pageNumber + 1 }} Rotation
      </div>
      
      <div class="grid grid-cols-2 gap-2">
        <button
          @click="setRotation(0)"
          :class="getRotationButtonClass(0)"
          title="No rotation (0°)"
        >
          <div class="rotation-preview rotation-0">
            📄
          </div>
          0°
        </button>
        
        <button
          @click="setRotation(90)"
          :class="getRotationButtonClass(90)"
          title="Rotate 90° clockwise"
        >
          <div class="rotation-preview rotation-90">
            📄
          </div>
          90°
        </button>
        
        <button
          @click="setRotation(180)"
          :class="getRotationButtonClass(180)"
          title="Rotate 180° (upside down)"
        >
          <div class="rotation-preview rotation-180">
            📄
          </div>
          180°
        </button>
        
        <button
          @click="setRotation(270)"
          :class="getRotationButtonClass(270)"
          title="Rotate 270° (90° counter-clockwise)"
        >
          <div class="rotation-preview rotation-270">
            📄
          </div>
          270°
        </button>
      </div>
      
      <div class="mt-3 pt-2 border-t border-gray-200">
        <button
          @click="rotateClockwise"
          class="w-full text-xs bg-blue-600 text-white px-2 py-1 rounded hover:bg-blue-700 transition-colors flex items-center justify-center space-x-1"
        >
          <ArrowPathIcon class="h-3 w-3" />
          <span>Rotate +90°</span>
        </button>
      </div>
    </div>

    <!-- Click overlay for editing mode -->
    <div 
      v-if="editable"
      class="click-overlay absolute inset-0 cursor-pointer"
      @click="handlePageClick"
      @mouseenter="handleMouseEnter"
      @mouseleave="handleMouseLeave"
    >
      <!-- Rotation preview overlay -->
      <div 
        v-if="showPreview && rotation !== previewRotation"
        class="rotation-preview-overlay absolute inset-0 bg-blue-100 bg-opacity-30 flex items-center justify-center"
      >
        <div class="bg-blue-600 text-white px-4 py-2 rounded-lg shadow-lg">
          <div class="text-sm font-bold">Preview: {{ previewRotation }}°</div>
          <div class="text-xs mt-1">Click to apply rotation</div>
        </div>
      </div>
    </div>

    <!-- Instructions overlay for edit mode -->
    <div 
      v-if="editable && showInstructions"
      class="instructions-overlay absolute top-4 left-4 bg-black bg-opacity-75 text-white p-3 rounded-lg max-w-sm z-30"
    >
      <div class="text-sm">
        <p class="font-semibold mb-2">Rotation Mode Active</p>
        <ul class="text-xs space-y-1">
          <li>• Click the rotation indicator to open controls</li>
          <li>• Select from 0°, 90°, 180°, or 270°</li>
          <li>• Blue badge shows current rotation</li>
          <li>• Changes apply to individual pages</li>
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
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { ArrowPathIcon } from '@heroicons/vue/24/outline'

interface Props {
  rotation: number  // 0, 90, 180, 270
  pageNumber: number
  editable: boolean
}

interface Emits {
  (e: 'rotation-changed', pageNumber: number, rotation: number): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const showInstructions = ref(true)
const showControls = ref(false)
const showPreview = ref(false)
const previewRotation = ref(0)

// Computed
const normalizedRotation = computed(() => {
  // Ensure rotation is always 0, 90, 180, or 270
  return ((props.rotation % 360) + 360) % 360
})

// Event handlers
const handleIndicatorClick = () => {
  if (!props.editable) return
  showControls.value = !showControls.value
}

const handlePageClick = (event: MouseEvent) => {
  if (!props.editable) return
  
  // Don't handle if clicking on controls
  if ((event.target as HTMLElement).closest('.rotation-controls')) {
    return
  }
  
  // Close controls if open
  if (showControls.value) {
    showControls.value = false
    return
  }
  
  // Cycle through rotations
  rotateClockwise()
}

const handleMouseEnter = () => {
  if (props.editable && !showControls.value) {
    showPreview.value = true
    previewRotation.value = getNextRotation()
  }
}

const handleMouseLeave = () => {
  showPreview.value = false
}

const setRotation = (newRotation: number) => {
  if (newRotation === normalizedRotation.value) return
  
  emit('rotation-changed', props.pageNumber, newRotation)
  showControls.value = false
}

const rotateClockwise = () => {
  const nextRotation = getNextRotation()
  emit('rotation-changed', props.pageNumber, nextRotation)
}

const getNextRotation = (): number => {
  const current = normalizedRotation.value
  return current === 270 ? 0 : current + 90
}

// Style helpers
const getRotationButtonClass = (buttonRotation: number): string => {
  const baseClasses = [
    'flex', 'flex-col', 'items-center', 'space-y-1', 'p-2', 'rounded', 
    'text-xs', 'font-medium', 'transition-all', 'cursor-pointer'
  ]
  
  if (buttonRotation === normalizedRotation.value) {
    return [...baseClasses, 'bg-blue-100', 'text-blue-800', 'border-2', 'border-blue-600'].join(' ')
  } else {
    return [...baseClasses, 'bg-gray-50', 'text-gray-700', 'border', 'border-gray-300', 'hover:bg-gray-100'].join(' ')
  }
}

// Click outside to close controls
const handleClickOutside = (event: MouseEvent) => {
  const target = event.target as HTMLElement
  if (!target.closest('.rotation-controls') && !target.closest('.rotation-indicator')) {
    showControls.value = false
  }
}

// Lifecycle
onMounted(() => {
  // Auto-hide instructions after 7 seconds
  setTimeout(() => {
    showInstructions.value = false
  }, 7000)
  
  // Add click outside listener
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})

// Watch for rotation changes to close controls
const handleRotationChange = () => {
  nextTick(() => {
    showControls.value = false
  })
}
</script>

<style scoped>
.rotation-overlay {
  pointer-events: none;
  z-index: 15;
}

.rotation-overlay > * {
  pointer-events: auto;
}

.rotation-overlay.editing-mode .click-overlay {
  pointer-events: auto;
  cursor: pointer;
}

/* Rotation indicator */
.rotation-indicator {
  font-size: 10px;
  line-height: 1;
  backdrop-filter: blur(4px);
  transition: all 0.2s ease-in-out;
}

.rotation-indicator.clickable {
  cursor: pointer;
}

.rotation-indicator:hover {
  background-color: rgb(37, 99, 235);
  transform: scale(1.05);
}

/* Rotation controls */
.rotation-controls {
  opacity: 0;
  visibility: hidden;
  transform: translateY(-8px);
  transition: all 0.2s ease-in-out;
  min-width: 160px;
}

.rotation-controls.visible {
  opacity: 1;
  visibility: visible;
  transform: translateY(0);
}

/* Rotation preview icons */
.rotation-preview {
  font-size: 16px;
  transition: transform 0.2s ease-in-out;
  display: inline-block;
}

.rotation-preview.rotation-0 {
  transform: rotate(0deg);
}

.rotation-preview.rotation-90 {
  transform: rotate(90deg);
}

.rotation-preview.rotation-180 {
  transform: rotate(180deg);
}

.rotation-preview.rotation-270 {
  transform: rotate(270deg);
}

/* Rotation preview overlay */
.rotation-preview-overlay {
  transition: all 0.3s ease-in-out;
  animation: fadeIn 0.3s ease-out;
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
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

/* Button hover effects */
.rotation-controls button:hover .rotation-preview {
  transform: scale(1.1) rotate(var(--rotation-angle));
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .rotation-controls {
    right: 8px;
    min-width: 140px;
  }
  
  .rotation-controls .grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 6px;
  }
  
  .rotation-controls button {
    padding: 6px;
    font-size: 10px;
  }
  
  .rotation-preview {
    font-size: 14px;
  }
  
  .rotation-indicator {
    font-size: 9px;
    padding: 3px 6px;
  }
}

/* High contrast mode support */
@media (prefers-contrast: high) {
  .rotation-indicator {
    border: 2px solid white;
    background-color: #1d4ed8;
  }
  
  .rotation-controls {
    border: 2px solid #000;
    background-color: #fff;
  }
  
  .rotation-preview-overlay {
    background-color: rgba(29, 78, 216, 0.4);
  }
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  .rotation-indicator,
  .rotation-controls,
  .rotation-preview,
  .rotation-preview-overlay {
    transition: none;
    animation: none;
  }
  
  .instructions-overlay {
    animation: none;
  }
}

/* Focus styles for accessibility */
.rotation-controls button:focus {
  outline: 2px solid #2563eb;
  outline-offset: 2px;
}

.rotation-indicator:focus {
  outline: 2px solid #2563eb;
  outline-offset: 2px;
}
</style>
