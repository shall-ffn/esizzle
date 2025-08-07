<template>
  <div class="error-overlay fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
    <div class="error-modal bg-white rounded-lg shadow-xl p-6 max-w-md w-full mx-4">
      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <div class="flex items-center space-x-3">
          <div class="error-icon">
            <ExclamationTriangleIcon class="h-6 w-6 text-red-500" />
          </div>
          <h3 class="text-lg font-semibold text-gray-900">
            Processing Error
          </h3>
        </div>
        
        <button
          @click="handleDismiss"
          class="text-gray-400 hover:text-gray-600 transition-colors"
          title="Dismiss error"
        >
          <XMarkIcon class="h-5 w-5" />
        </button>
      </div>

      <!-- Error Message -->
      <div class="mb-6">
        <div class="bg-red-50 border border-red-200 rounded-lg p-4">
          <div class="text-sm font-medium text-red-800 mb-2">
            {{ getErrorTitle() }}
          </div>
          <div class="text-sm text-red-700">
            {{ getErrorMessage() }}
          </div>
        </div>
      </div>

      <!-- Error Details (collapsible) -->
      <div v-if="error?.details && showDetails" class="mb-6">
        <div class="bg-gray-50 border border-gray-200 rounded-lg p-4">
          <div class="text-xs font-medium text-gray-700 mb-2">Error Details:</div>
          <pre class="text-xs text-gray-600 whitespace-pre-wrap">{{ formatErrorDetails() }}</pre>
        </div>
      </div>

      <!-- Toggle Details Button -->
      <div v-if="error?.details" class="mb-6">
        <button
          @click="showDetails = !showDetails"
          class="text-sm text-gray-600 hover:text-gray-800 underline transition-colors"
        >
          {{ showDetails ? 'Hide Details' : 'Show Details' }}
        </button>
      </div>

      <!-- Suggested Actions -->
      <div class="mb-6">
        <div class="text-sm font-medium text-gray-700 mb-3">Suggested Actions:</div>
        <ul class="text-sm text-gray-600 space-y-2">
          <li v-for="suggestion in getSuggestions()" :key="suggestion" class="flex items-start space-x-2">
            <span class="text-blue-500 mt-0.5">•</span>
            <span>{{ suggestion }}</span>
          </li>
        </ul>
      </div>

      <!-- Action Buttons -->
      <div class="flex items-center justify-end space-x-3">
        <button
          @click="handleDismiss"
          class="px-4 py-2 text-sm text-gray-700 bg-gray-100 border border-gray-300 rounded-md hover:bg-gray-200 transition-colors"
        >
          Dismiss
        </button>
        
        <button
          @click="handleRetry"
          class="px-4 py-2 text-sm text-white bg-blue-600 rounded-md hover:bg-blue-700 transition-colors flex items-center space-x-2"
        >
          <ArrowPathIcon class="h-4 w-4" />
          <span>Try Again</span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ExclamationTriangleIcon, XMarkIcon, ArrowPathIcon } from '@heroicons/vue/24/outline'
import type { ManipulationError } from '@/types/manipulation'

interface Props {
  error: ManipulationError | null
}

interface Emits {
  (e: 'retry'): void
  (e: 'dismiss'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const showDetails = ref(false)

// Methods
const getErrorTitle = () => {
  if (!props.error) return 'Unknown Error'
  
  switch (props.error.type) {
    case 'validation':
      return 'Validation Error'
    case 'processing':
      return 'Processing Failed'
    case 'network':
      return 'Network Error'
    case 'permission':
      return 'Permission Denied'
    default:
      return 'Error Occurred'
  }
}

const getErrorMessage = () => {
  if (!props.error) return 'An unknown error occurred.'
  
  return props.error.message || 'An unexpected error occurred while processing your request.'
}

const formatErrorDetails = () => {
  if (!props.error?.details) return ''
  
  if (typeof props.error.details === 'string') {
    return props.error.details
  } else {
    try {
      return JSON.stringify(props.error.details, null, 2)
    } catch {
      return String(props.error.details)
    }
  }
}

const getSuggestions = () => {
  if (!props.error) return ['Please try again later.']
  
  const suggestions: Record<string, string[]> = {
    validation: [
      'Check that all required fields are filled out correctly',
      'Ensure redaction areas are large enough to be valid',
      'Verify that document types are selected for page breaks'
    ],
    processing: [
      'Try processing with fewer manipulations at once',
      'Check that the document is not corrupted',
      'Ensure you have sufficient permissions for this document',
      'Contact support if the problem persists'
    ],
    network: [
      'Check your internet connection',
      'Try refreshing the page and attempting again',
      'Wait a few minutes and retry the operation'
    ],
    permission: [
      'Verify you have edit permissions for this document',
      'Contact your administrator for access',
      'Try logging out and back in'
    ]
  }
  
  return suggestions[props.error.type] || [
    'Try refreshing the page',
    'Check your internet connection',
    'Contact support if the issue persists'
  ]
}

// Event handlers
const handleRetry = () => {
  emit('retry')
}

const handleDismiss = () => {
  emit('dismiss')
}
</script>

<style scoped>
/* Error overlay */
.error-overlay {
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

/* Error modal */
.error-modal {
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

/* Error icon */
.error-icon {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Error details pre formatting */
pre {
  max-height: 150px;
  overflow-y: auto;
  font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
}

/* Custom scrollbar for error details */
pre::-webkit-scrollbar {
  width: 4px;
}

pre::-webkit-scrollbar-track {
  background: #f1f5f9;
}

pre::-webkit-scrollbar-thumb {
  background: #cbd5e1;
  border-radius: 2px;
}

pre::-webkit-scrollbar-thumb:hover {
  background: #94a3b8;
}

/* Suggestions list styling */
ul li {
  line-height: 1.5;
}

/* Button focus styles */
button:focus {
  outline: 2px solid #3b82f6;
  outline-offset: 2px;
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  .error-overlay,
  .error-modal {
    animation: none;
  }
}

/* High contrast mode */
@media (prefers-contrast: high) {
  .error-modal {
    border: 2px solid #000;
  }
  
  .bg-red-50 {
    background-color: #fee2e2;
    border-color: #dc2626;
  }
}

/* Mobile responsiveness */
@media (max-width: 640px) {
  .error-modal {
    margin: 16px;
    max-width: none;
  }
  
  .flex.space-x-3 {
    flex-direction: column;
    space-x: 0;
  }
  
  .flex.space-x-3 > * {
    margin: 4px 0;
  }
}
</style>
