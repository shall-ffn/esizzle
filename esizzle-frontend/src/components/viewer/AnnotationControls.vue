<template>
  <div class="annotation-controls fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200 shadow-lg z-50">
    <div class="container mx-auto px-4 py-3">
      <div class="flex items-center justify-between">
        <!-- Left side - Mode and Status -->
        <div class="flex items-center space-x-4">
          <!-- Current Mode Display -->
          <div class="flex items-center space-x-2">
            <div class="mode-indicator">
              <component 
                :is="getModeIcon()" 
                class="h-4 w-4"
                :class="getModeIconColor()"
              />
            </div>
            <span class="text-sm font-medium text-gray-700">
              {{ getModeDisplayName() }}
            </span>
          </div>
          
          <!-- Status Indicator -->
          <div class="flex items-center space-x-2">
            <StatusDot :status="processingStatus" />
            <span class="text-xs text-gray-500">
              {{ getStatusMessage() }}
            </span>
          </div>
        </div>

        <!-- Center - Change Summary -->
        <div v-if="hasChanges" class="flex items-center space-x-4">
          <div class="change-summary bg-blue-50 border border-blue-200 rounded-lg px-3 py-2">
            <div class="flex items-center space-x-3 text-xs">
              <div v-if="changeSummary.pendingRedactions > 0" class="flex items-center space-x-1">
                <div class="w-2 h-2 bg-yellow-500 rounded"></div>
                <span>{{ changeSummary.pendingRedactions }} redactions</span>
              </div>
              
              <div v-if="changeSummary.pendingRotations > 0" class="flex items-center space-x-1">
                <div class="w-2 h-2 bg-blue-500 rounded"></div>
                <span>{{ changeSummary.pendingRotations }} rotations</span>
              </div>
              
              <div v-if="changeSummary.pendingPageBreaks > 0" class="flex items-center space-x-1">
                <div class="w-2 h-2 bg-green-500 rounded"></div>
                <span>{{ changeSummary.pendingPageBreaks }} splits</span>
              </div>
              
              <div v-if="changeSummary.pendingDeletions > 0" class="flex items-center space-x-1">
                <div class="w-2 h-2 bg-red-500 rounded"></div>
                <span>{{ changeSummary.pendingDeletions }} deletions</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Right side - Actions -->
        <div class="flex items-center space-x-3">
          <!-- Mode Selection Shortcuts -->
          <div class="mode-shortcuts hidden md:flex items-center space-x-1 bg-gray-100 rounded-lg p-1">
            <button
              v-for="mode in availableModes"
              :key="mode.value"
              @click="changeMode(mode.value)"
              :class="getModeButtonClass(mode.value)"
              :title="mode.tooltip"
            >
              <component :is="mode.icon" class="h-4 w-4" />
            </button>
          </div>
          
          <!-- Action Buttons -->
          <div class="action-buttons flex items-center space-x-2">
            <button
              v-if="hasChanges"
              @click="discardChanges"
              :disabled="processingStatus === 'processing'"
              class="btn-secondary"
            >
              <XMarkIcon class="h-4 w-4 mr-1" />
              Discard
            </button>
            
            <button
              v-if="hasChanges"
              @click="saveChanges"
              :disabled="processingStatus === 'processing' || !canSave"
              class="btn-primary"
            >
              <span v-if="processingStatus === 'processing'" class="flex items-center">
                <Spinner class="h-4 w-4 mr-2" />
                Processing...
              </span>
              <span v-else class="flex items-center">
                <CheckIcon class="h-4 w-4 mr-1" />
                Save & Apply
              </span>
            </button>
          </div>
        </div>
      </div>

      <!-- Processing Progress Bar -->
      <div v-if="processingStatus === 'processing'" class="mt-3">
        <div class="w-full bg-gray-200 rounded-full h-2">
          <div 
            class="bg-blue-600 h-2 rounded-full transition-all duration-300"
            :style="{ width: `${processingProgress}%` }"
          ></div>
        </div>
        <div class="flex justify-between text-xs text-gray-500 mt-1">
          <span>{{ processingMessage }}</span>
          <span>{{ processingProgress }}%</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import {
  CursorArrowRaysIcon,
  PencilIcon,
  ScissorsIcon,
  TrashIcon,
  ArrowPathIcon,
  XMarkIcon,
  CheckIcon
} from '@heroicons/vue/24/outline'
import type { 
  EditMode, 
  ProcessingStatus, 
  ChangeSummary 
} from '@/types/manipulation'
import StatusDot from './StatusDot.vue'
import Spinner from '@/components/ui/Spinner.vue'

interface Props {
  editMode: EditMode
  hasChanges: boolean
  processingStatus: ProcessingStatus
  changeSummary: ChangeSummary
  processingProgress?: number
  processingMessage?: string
  canSave?: boolean
}

interface Emits {
  (e: 'mode-changed', mode: EditMode): void
  (e: 'save-changes'): void
  (e: 'discard-changes'): void
}

const props = withDefaults(defineProps<Props>(), {
  processingProgress: 0,
  processingMessage: 'Processing...',
  canSave: true
})

const emit = defineEmits<Emits>()

// Available modes configuration
const availableModes = [
  {
    value: 'view' as EditMode,
    icon: CursorArrowRaysIcon,
    tooltip: 'View Mode (V)'
  },
  {
    value: 'redaction' as EditMode,
    icon: PencilIcon,
    tooltip: 'Redaction Mode (R)'
  },
  {
    value: 'pagebreak' as EditMode,
    icon: ScissorsIcon,
    tooltip: 'Page Break Mode (B)'
  },
  {
    value: 'deletion' as EditMode,
    icon: TrashIcon,
    tooltip: 'Page Deletion Mode (D)'
  },
  {
    value: 'rotation' as EditMode,
    icon: ArrowPathIcon,
    tooltip: 'Rotation Mode (T)'
  }
]

// Computed properties
const hasChanges = computed(() => props.changeSummary.totalChanges > 0)

// Methods
const getModeIcon = () => {
  const mode = availableModes.find(m => m.value === props.editMode)
  return mode?.icon || CursorArrowRaysIcon
}

const getModeIconColor = () => {
  const colorMap: Record<EditMode, string> = {
    view: 'text-gray-500',
    redaction: 'text-yellow-600',
    pagebreak: 'text-green-600',
    deletion: 'text-red-600',
    rotation: 'text-blue-600'
  }
  return colorMap[props.editMode] || 'text-gray-500'
}

const getModeDisplayName = () => {
  const nameMap: Record<EditMode, string> = {
    view: 'View Mode',
    redaction: 'Redaction Mode',
    pagebreak: 'Page Break Mode',
    deletion: 'Page Deletion Mode',
    rotation: 'Rotation Mode'
  }
  return nameMap[props.editMode] || 'View Mode'
}

const getStatusMessage = () => {
  switch (props.processingStatus) {
    case 'processing':
      return 'Processing changes...'
    case 'completed':
      return 'Changes applied successfully'
    case 'error':
      return 'Processing failed'
    default:
      return hasChanges.value ? 'Unsaved changes' : 'Ready'
  }
}

const getModeButtonClass = (mode: EditMode) => {
  const baseClasses = 'p-2 rounded transition-colors'
  if (mode === props.editMode) {
    return `${baseClasses} bg-white shadow-sm ${getModeIconColor()}`
  } else {
    return `${baseClasses} text-gray-400 hover:text-gray-600 hover:bg-gray-50`
  }
}

// Event handlers
const changeMode = (mode: EditMode) => {
  emit('mode-changed', mode)
}

const saveChanges = () => {
  emit('save-changes')
}

const discardChanges = () => {
  emit('discard-changes')
}
</script>

<style scoped>
/* Base button styles */
.btn-primary {
  @apply px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors;
}

.btn-secondary {
  @apply px-4 py-2 bg-white text-gray-700 text-sm font-medium border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors;
}

/* Mode indicator */
.mode-indicator {
  @apply flex items-center justify-center w-8 h-8 bg-gray-100 rounded-full;
}

/* Change summary */
.change-summary {
  animation: slideInUp 0.3s ease-out;
}

@keyframes slideInUp {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Progress bar animation */
.bg-blue-600 {
  transition: width 0.3s ease-in-out;
}

/* Mobile responsiveness */
@media (max-width: 768px) {
  .mode-shortcuts {
    display: none !important;
  }
  
  .action-buttons {
    flex-direction: column;
    align-items: stretch;
  }
  
  .action-buttons > * {
    margin: 2px 0;
  }
  
  .change-summary {
    display: none;
  }
}

/* High contrast mode */
@media (prefers-contrast: high) {
  .annotation-controls {
    border-top: 3px solid #000;
  }
  
  .mode-indicator {
    border: 2px solid #666;
  }
  
  .change-summary {
    border: 2px solid #2563eb;
  }
}

/* Reduced motion */
@media (prefers-reduced-motion: reduce) {
  * {
    animation: none !important;
    transition: none !important;
  }
}

/* Focus styles for accessibility */
button:focus {
  @apply outline-none ring-2 ring-blue-500 ring-offset-2;
}

/* Container adjustments */
.annotation-controls {
  backdrop-filter: blur(8px);
  background-color: rgba(255, 255, 255, 0.95);
}
</style>
