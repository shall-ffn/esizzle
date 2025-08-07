<template>
  <div class="enhanced-document-toolbar bg-white border-t border-gray-200 p-3 shadow-lg">
    <div class="flex items-center justify-between">
      <!-- Mode Selection -->
      <div class="flex items-center space-x-2">
        <span class="text-sm font-medium text-gray-700">Mode:</span>
        <div class="flex rounded-lg border border-gray-300 overflow-hidden">
          <ModeButton 
            mode="view" 
            :active="editMode === 'view'"
            :disabled="processingStatus === 'processing'"
            @click="setEditMode('view')"
          >
            <CursorArrowRaysIcon class="h-4 w-4" />
            View
          </ModeButton>
          <ModeButton 
            mode="redaction" 
            :active="editMode === 'redaction'"
            :disabled="processingStatus === 'processing'"
            @click="setEditMode('redaction')"
          >
            <PencilIcon class="h-4 w-4" />
            Redact
          </ModeButton>
          <ModeButton 
            mode="pagebreak" 
            :active="editMode === 'pagebreak'"
            :disabled="processingStatus === 'processing'"
            @click="setEditMode('pagebreak')"
          >
            <ScissorsIcon class="h-4 w-4" />
            Split
          </ModeButton>
          <ModeButton 
            mode="deletion" 
            :active="editMode === 'deletion'"
            :disabled="processingStatus === 'processing'"
            @click="setEditMode('deletion')"
          >
            <TrashIcon class="h-4 w-4" />
            Delete
          </ModeButton>
          <ModeButton 
            mode="rotation" 
            :active="editMode === 'rotation'"
            :disabled="processingStatus === 'processing'"
            @click="setEditMode('rotation')"
          >
            <ArrowPathIcon class="h-4 w-4" />
            Rotate
          </ModeButton>
        </div>
      </div>

      <!-- Status and Actions -->
      <div class="flex items-center space-x-3">
        <!-- Processing Status -->
        <StatusIndicator 
          :status="processingStatus"
          :progress="processingProgress"
        />

        <!-- Change Summary -->
        <div v-if="hasUnsavedChanges && changeSummary" class="text-sm text-gray-600">
          <ChangeSummaryDisplay :summary="changeSummary" />
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center space-x-2">
          <button
            v-if="hasUnsavedChanges"
            @click="discardChanges"
            :disabled="processingStatus === 'processing'"
            class="px-3 py-1.5 text-sm border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            Discard Changes
          </button>
          <button
            v-if="hasUnsavedChanges"
            @click="saveAndProcess"
            :disabled="processingStatus === 'processing' || !canSave"
            class="px-4 py-1.5 text-sm bg-hydra-600 text-white rounded-md hover:bg-hydra-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center space-x-2"
          >
            <span v-if="processingStatus === 'processing'" class="flex items-center space-x-2">
              <Spinner class="h-4 w-4" />
              <span>Processing...</span>
            </span>
            <span v-else>Save & Apply</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Mode-specific instructions -->
    <div v-if="editMode !== 'view'" class="mt-3 p-2 bg-blue-50 border border-blue-200 rounded-md">
      <InstructionBar :mode="editMode" />
    </div>

    <!-- Processing Progress Bar -->
    <div v-if="processingStatus === 'processing'" class="mt-3">
      <ProcessingProgressBar 
        :progress="processingProgress" 
        @cancel="handleCancelProcessing"
      />
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
  ArrowPathIcon 
} from '@heroicons/vue/24/outline'
import type { 
  EditMode, 
  ProcessingStatus, 
  ProcessingProgress, 
  ChangeSummary 
} from '@/types/manipulation'
import ModeButton from './ModeButton.vue'
import StatusIndicator from './StatusIndicator.vue'
import ChangeSummaryDisplay from './ChangeSummaryDisplay.vue'
import InstructionBar from './InstructionBar.vue'
import ProcessingProgressBar from './ProcessingProgressBar.vue'
import Spinner from '@/components/ui/Spinner.vue'

interface Props {
  editMode: EditMode
  hasUnsavedChanges: boolean
  processingStatus: ProcessingStatus
  processingProgress?: ProcessingProgress
  changeSummary?: ChangeSummary
  canSave?: boolean
}

interface Emits {
  (e: 'mode-changed', mode: EditMode): void
  (e: 'save-requested'): void
  (e: 'discard-requested'): void
  (e: 'processing-cancelled'): void
}

const props = withDefaults(defineProps<Props>(), {
  canSave: true
})

const emit = defineEmits<Emits>()

// Computed properties
const isProcessing = computed(() => props.processingStatus === 'processing')

// Event handlers
const setEditMode = (mode: EditMode) => {
  if (isProcessing.value) return
  emit('mode-changed', mode)
}

const saveAndProcess = () => {
  if (isProcessing.value || !props.canSave) return
  emit('save-requested')
}

const discardChanges = () => {
  if (isProcessing.value) return
  emit('discard-requested')
}

const handleCancelProcessing = () => {
  emit('processing-cancelled')
}
</script>

<style scoped>
.enhanced-document-toolbar {
  background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
}

/* Ensure toolbar stays above other content */
.shadow-lg {
  box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05);
}
</style>
