<template>
  <div 
    v-if="editable || isDeleted"
    class="absolute inset-0 page-deletion-overlay"
    :class="{ 
      'editing-mode': editable,
      'deleted': isDeleted 
    }"
    @click="handlePageClick"
  >
    <!-- Page deletion X pattern (matches Hydra original) -->
    <div 
      v-if="isDeleted"
      class="page-deletion absolute inset-0 bg-red-200 bg-opacity-20 cursor-pointer"
    >
      <!-- X pattern using pseudo-elements -->
      <div class="deletion-x"></div>
      
      <!-- Deletion indicator -->
      <div class="deletion-indicator absolute top-2 right-2 bg-red-600 text-white text-xs px-2 py-1 rounded-full font-bold shadow-lg">
        DELETE
      </div>
    </div>

    <!-- Hover preview for editable mode -->
    <div 
      v-if="editable && !isDeleted && showPreview"
      class="deletion-preview absolute inset-0 bg-red-200 bg-opacity-10 cursor-pointer"
    >
      <div class="preview-x"></div>
      <div class="preview-text absolute inset-0 flex items-center justify-center">
        <span class="bg-red-600 text-white px-3 py-1 rounded-lg font-bold text-sm shadow-lg">
          Click to mark for deletion
        </span>
      </div>
    </div>

    <!-- Instructions overlay for edit mode -->
    <div 
      v-if="editable && showInstructions"
      class="instructions-overlay absolute top-4 left-4 bg-black bg-opacity-75 text-white p-3 rounded-lg max-w-sm z-30"
    >
      <div class="text-sm">
        <p class="font-semibold mb-2">Page Deletion Mode Active</p>
        <ul class="text-xs space-y-1">
          <li>• Click on pages to toggle deletion</li>
          <li>• Red X pattern = marked for deletion</li>
          <li>• Deleted pages will be removed from final PDF</li>
          <li>• Click again to unmark a page</li>
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
import { ref, onMounted, onUnmounted } from 'vue'

interface Props {
  pageNumber: number
  isDeleted: boolean
  editable: boolean
}

interface Emits {
  (e: 'deletion-toggled', pageNumber: number): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const showInstructions = ref(true)
const showPreview = ref(false)

// Event handlers
const handlePageClick = () => {
  if (!props.editable) return
  
  emit('deletion-toggled', props.pageNumber)
}

const handleMouseEnter = () => {
  if (props.editable && !props.isDeleted) {
    showPreview.value = true
  }
}

const handleMouseLeave = () => {
  showPreview.value = false
}

// Lifecycle
onMounted(() => {
  // Auto-hide instructions after 6 seconds
  setTimeout(() => {
    showInstructions.value = false
  }, 6000)
  
  // Add mouse tracking for preview
  if (props.editable) {
    const overlay = document.querySelector('.page-deletion-overlay') as HTMLElement
    if (overlay) {
      overlay.addEventListener('mouseenter', handleMouseEnter)
      overlay.addEventListener('mouseleave', handleMouseLeave)
    }
  }
})

onUnmounted(() => {
  // Cleanup event listeners
  const overlay = document.querySelector('.page-deletion-overlay') as HTMLElement
  if (overlay) {
    overlay.removeEventListener('mouseenter', handleMouseEnter)
    overlay.removeEventListener('mouseleave', handleMouseLeave)
  }
})
</script>

<style scoped>
.page-deletion-overlay {
  pointer-events: auto;
  z-index: 30;
}

.page-deletion-overlay.editing-mode {
  cursor: pointer;
}

/* Page deletion X pattern (matches Hydra original) */
.page-deletion {
  position: absolute;
  inset: 0;
  background-color: rgba(255, 0, 0, 0.2);
  z-index: 30;
  pointer-events: auto;
}

.deletion-x::before,
.deletion-x::after {
  content: '';
  position: absolute;
  top: 0;
  left: 50%;
  width: 5px;
  height: 100%;
  background-color: rgba(220, 38, 38, 0.75);
  transform-origin: center;
}

.deletion-x::before {
  transform: translateX(-50%) rotate(45deg);
}

.deletion-x::after {
  transform: translateX(-50%) rotate(-45deg);
}

/* Preview X pattern for hover */
.preview-x::before,
.preview-x::after {
  content: '';
  position: absolute;
  top: 0;
  left: 50%;
  width: 3px;
  height: 100%;
  background-color: rgba(220, 38, 38, 0.4);
  transform-origin: center;
  transition: all 0.2s ease-in-out;
}

.preview-x::before {
  transform: translateX(-50%) rotate(45deg);
}

.preview-x::after {
  transform: translateX(-50%) rotate(-45deg);
}

/* Hover effects */
.deletion-preview:hover .preview-x::before,
.deletion-preview:hover .preview-x::after {
  background-color: rgba(220, 38, 38, 0.6);
  width: 4px;
}

/* Deletion indicator */
.deletion-indicator {
  animation: pulse 2s infinite;
  font-size: 10px;
  line-height: 1;
}

@keyframes pulse {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.7;
  }
}

/* Preview text */
.preview-text {
  opacity: 0;
  transition: opacity 0.3s ease-in-out;
}

.deletion-preview:hover .preview-text {
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

/* Enhanced visual feedback */
.page-deletion:hover {
  background-color: rgba(255, 0, 0, 0.3);
}

.page-deletion:hover .deletion-x::before,
.page-deletion:hover .deletion-x::after {
  background-color: rgba(220, 38, 38, 0.9);
  width: 6px;
}

.page-deletion:hover .deletion-indicator {
  animation: none;
  opacity: 1;
  transform: scale(1.05);
}

/* Transitions */
.page-deletion,
.deletion-preview {
  transition: all 0.2s ease-in-out;
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .deletion-x::before,
  .deletion-x::after {
    width: 4px;
  }
  
  .preview-x::before,
  .preview-x::after {
    width: 2px;
  }
  
  .deletion-indicator {
    font-size: 9px;
    padding: 2px 6px;
  }
  
  .preview-text span {
    font-size: 12px;
    padding: 4px 8px;
  }
}

/* High contrast mode support */
@media (prefers-contrast: high) {
  .deletion-x::before,
  .deletion-x::after {
    background-color: #dc2626;
  }
  
  .page-deletion {
    background-color: rgba(255, 0, 0, 0.3);
  }
  
  .deletion-indicator {
    background-color: #dc2626;
    border: 2px solid white;
  }
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  .deletion-indicator {
    animation: none;
  }
  
  .instructions-overlay {
    animation: none;
  }
  
  * {
    transition: none !important;
  }
}
</style>
