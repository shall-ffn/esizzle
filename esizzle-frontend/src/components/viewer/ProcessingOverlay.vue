<template>
  <div class="processing-overlay fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
    <div class="processing-modal bg-white rounded-lg shadow-xl p-6 max-w-md w-full mx-4">
      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <div class="flex items-center space-x-3">
          <div class="processing-spinner">
            <div class="spinner"></div>
          </div>
          <h3 class="text-lg font-semibold text-gray-900">
            Processing Document
          </h3>
        </div>
        
        <button
          v-if="progress && progress.status !== 'completed'"
          @click="handleCancel"
          class="text-gray-400 hover:text-gray-600 transition-colors"
          title="Cancel processing"
        >
          <XMarkIcon class="h-5 w-5" />
        </button>
      </div>

      <!-- Progress Information -->
      <div class="mb-6">
        <div class="flex justify-between items-center mb-2">
          <span class="text-sm font-medium text-gray-700">
            {{ getCurrentOperation() }}
          </span>
          <span class="text-sm text-gray-500">
            {{ getProgressPercentage() }}%
          </span>
        </div>
        
        <!-- Progress Bar -->
        <div class="w-full bg-gray-200 rounded-full h-2">
          <div 
            class="bg-blue-600 h-2 rounded-full transition-all duration-500 ease-out"
            :style="{ width: `${getProgressPercentage()}%` }"
          ></div>
        </div>
        
        <!-- Status Message -->
        <p class="text-sm text-gray-600 mt-3">
          {{ getStatusMessage() }}
        </p>
      </div>

      <!-- Operation Details -->
      <div v-if="progress && progress.currentOperation" class="mb-4">
        <div class="bg-gray-50 rounded-lg p-3">
          <div class="text-xs font-medium text-gray-700 mb-1">Current Step:</div>
          <div class="text-sm text-gray-900">{{ progress.currentOperation }}</div>
        </div>
      </div>

      <!-- Estimated Time -->
      <div class="flex items-center justify-between text-xs text-gray-500">
        <span>{{ getElapsedTime() }}</span>
        <span v-if="getEstimatedTime()">{{ getEstimatedTime() }} remaining</span>
      </div>

      <!-- Cancel Button -->
      <div v-if="progress && progress.status !== 'completed'" class="mt-6 flex justify-end">
        <button
          @click="handleCancel"
          class="px-4 py-2 text-sm text-gray-700 bg-gray-100 border border-gray-300 rounded-md hover:bg-gray-200 transition-colors"
        >
          Cancel Processing
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { XMarkIcon } from '@heroicons/vue/24/outline'
import type { ProcessingProgress } from '@/types/manipulation'

interface Props {
  progress?: ProcessingProgress
}

interface Emits {
  (e: 'cancel'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const startTime = ref<Date>(new Date())
const elapsedSeconds = ref(0)

// Timer for elapsed time
let elapsedTimer: number | null = null

// Computed properties
const getProgressPercentage = () => {
  return props.progress?.progress || 0
}

const getCurrentOperation = () => {
  if (!props.progress) return 'Initializing...'
  
  switch (props.progress.status) {
    case 'starting':
      return 'Starting processing...'
    case 'processing':
      return props.progress.currentOperation || 'Processing document...'
    case 'completed':
      return 'Processing completed'
    case 'error':
      return 'Processing failed'
    default:
      return 'Processing...'
  }
}

const getStatusMessage = () => {
  if (!props.progress) return 'Please wait while we process your document changes.'
  
  const operationMessages: Record<string, string> = {
    'Initializing...': 'Setting up processing environment',
    'Loading document data...': 'Retrieving document and manipulation data',
    'Downloading PDF...': 'Downloading document from storage',
    'Creating backup...': 'Creating backup copy before modifications',
    'Applying redactions...': 'Permanently removing sensitive content',
    'Applying rotations...': 'Rotating pages as specified',
    'Deleting pages...': 'Removing selected pages from document',
    'Splitting document...': 'Creating separate documents from page breaks',
    'Saving processed document...': 'Uploading modified document to storage'
  }
  
  const currentOp = props.progress.currentOperation || props.progress.message
  return operationMessages[currentOp] || 'Processing your document modifications'
}

const getElapsedTime = () => {
  const minutes = Math.floor(elapsedSeconds.value / 60)
  const seconds = elapsedSeconds.value % 60
  
  if (minutes > 0) {
    return `${minutes}m ${seconds}s elapsed`
  } else {
    return `${seconds}s elapsed`
  }
}

const getEstimatedTime = () => {
  const progress = getProgressPercentage()
  if (progress === 0 || progress >= 95) return ''
  
  // Estimate based on current progress and elapsed time
  const elapsedMs = elapsedSeconds.value * 1000
  const totalEstimatedMs = (elapsedMs / progress) * 100
  const remainingMs = totalEstimatedMs - elapsedMs
  const remainingSeconds = Math.ceil(remainingMs / 1000)
  
  if (remainingSeconds < 60) {
    return `~${remainingSeconds}s`
  } else {
    const minutes = Math.floor(remainingSeconds / 60)
    return `~${minutes}m`
  }
}

// Event handlers
const handleCancel = () => {
  emit('cancel')
}

// Lifecycle
onMounted(() => {
  startTime.value = new Date()
  
  // Start elapsed time counter
  elapsedTimer = setInterval(() => {
    elapsedSeconds.value = Math.floor((Date.now() - startTime.value.getTime()) / 1000)
  }, 1000)
})

onUnmounted(() => {
  if (elapsedTimer) {
    clearInterval(elapsedTimer)
  }
})
</script>

<style scoped>
/* Processing overlay */
.processing-overlay {
  backdrop-filter: blur(4px);
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

/* Processing modal */
.processing-modal {
  animation: slideInUp 0.3s ease-out;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
}

@keyframes slideInUp {
  from {
    opacity: 0;
    transform: translateY(16px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Spinner */
.processing-spinner {
  width: 24px;
  height: 24px;
  position: relative;
}

.spinner {
  width: 100%;
  height: 100%;
  border: 2px solid #e5e7eb;
  border-top: 2px solid #3b82f6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% {
    transform: rotate(0deg);
  }
  100% {
    transform: rotate(360deg);
  }
}

/* Progress bar animations */
.bg-blue-600 {
  transition: width 0.5s ease-out;
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  .processing-overlay,
  .processing-modal {
    animation: none;
  }
  
  .spinner {
    animation: none;
  }
  
  .bg-blue-600 {
    transition: none;
  }
}

/* High contrast mode */
@media (prefers-contrast: high) {
  .processing-modal {
    border: 2px solid #000;
  }
  
  .spinner {
    border-top-color: #000;
  }
}

/* Mobile responsiveness */
@media (max-width: 640px) {
  .processing-modal {
    margin: 16px;
    max-width: none;
  }
}
</style>
