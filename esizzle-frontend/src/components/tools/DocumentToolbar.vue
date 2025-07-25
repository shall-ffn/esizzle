<template>
  <div class="document-toolbar bg-white border-t border-gray-200 p-3">
    <div class="flex items-center justify-between">
      <!-- Left side - Document info and actions -->
      <div class="flex items-center space-x-4">
        <!-- Drag Mode Toggle -->
        <div class="flex items-center space-x-2">
          <span class="text-sm font-medium text-gray-700">Drag Mode</span>
          <button
            :class="[
              'px-3 py-1 text-xs rounded transition-colors',
              {
                'bg-hydra-600 text-white': dragMode,
                'bg-gray-200 text-gray-700 hover:bg-gray-300': !dragMode
              }
            ]"
            @click="toggleDragMode"
          >
            {{ dragMode ? 'ON' : 'OFF' }}
          </button>
        </div>

        <!-- Favorite Button -->
        <button 
          :class="[
            'flex items-center space-x-1 px-3 py-1 text-xs rounded transition-colors',
            {
              'bg-yellow-100 text-yellow-800 border border-yellow-300': isFavorite,
              'bg-gray-100 text-gray-700 hover:bg-gray-200': !isFavorite
            }
          ]"
          @click="toggleFavorite"
          :disabled="!selectedDocument"
        >
          <StarIcon :class="['h-3 w-3', { 'fill-current': isFavorite }]" />
          <span>{{ isFavorite ? 'Favorited' : 'Favorite' }}</span>
        </button>
      </div>

      <!-- Right side - Document manipulation tools -->
      <div class="flex items-center space-x-2">
        <!-- Rotation Tools -->
        <div class="flex items-center space-x-1 border-r border-gray-200 pr-3">
          <span class="text-xs text-gray-600 mr-2">Rotate:</span>
          <button
            v-for="angle in [90, 180, 270]"
            :key="angle"
            :class="[
              'p-2 text-xs rounded transition-colors border',
              {
                'bg-gray-50 hover:bg-gray-100 border-gray-300': !loadingRotation,
                'bg-gray-200 cursor-not-allowed border-gray-300': loadingRotation
              }
            ]"
            :disabled="!selectedDocument || loadingRotation"
            @click="rotateDocument(angle)"
            :title="`Rotate ${angle}°`"
          >
            <ArrowPathIcon 
              :class="[
                'h-4 w-4 transition-transform',
                {
                  'rotate-0': angle === 90,
                  'rotate-180': angle === 180,
                  'rotate-270': angle === 270
                }
              ]" 
            />
            <span class="sr-only">{{ angle }}°</span>
          </button>
        </div>

        <!-- Document Processing Tools -->
        <button 
          :class="[
            'flex items-center space-x-1 px-3 py-1 text-xs rounded transition-colors border',
            {
              'bg-red-50 hover:bg-red-100 border-red-300 text-red-700': !loadingRedaction,
              'bg-gray-200 cursor-not-allowed border-gray-300 text-gray-500': loadingRedaction
            }
          ]"
          :disabled="!selectedDocument || loadingRedaction"
          @click="startRedaction"
        >
          <PencilIcon class="h-4 w-4" />
          <span>{{ loadingRedaction ? 'Processing...' : 'Redact' }}</span>
        </button>
        
        <button 
          class="flex items-center space-x-1 px-3 py-1 text-xs rounded transition-colors border bg-gray-50 hover:bg-gray-100 border-gray-300 text-gray-700"
          :disabled="!selectedDocument"
          @click="splitDocument"
        >
          <ScissorsIcon class="h-4 w-4" />
          <span>Split</span>
        </button>
        
        <button 
          class="flex items-center space-x-1 px-3 py-1 text-xs rounded transition-colors border bg-gray-50 hover:bg-gray-100 border-gray-300 text-gray-700"
          :disabled="!selectedDocument"
          @click="stackDocument"
        >
          <DocumentDuplicateIcon class="h-4 w-4" />
          <span>Stack</span>
        </button>

        <!-- Document Classification -->
        <div class="border-l border-gray-200 pl-3">
          <select
            v-model="selectedDocumentType"
            :disabled="!selectedDocument || loadingClassification"
            class="text-xs border border-gray-300 rounded px-2 py-1 bg-white"
            @change="updateDocumentType"
          >
            <option value="">Select Type...</option>
            <option v-for="type in documentTypes" :key="type.value" :value="type.value">
              {{ type.label }}
            </option>
          </select>
        </div>
      </div>
    </div>

    <!-- Status Bar -->
    <div v-if="showStatus" class="mt-2 text-xs text-gray-600">
      <div v-if="statusMessage" class="flex items-center space-x-2">
        <div 
          v-if="statusType === 'loading'"
          class="w-3 h-3 border border-gray-400 border-t-transparent border-r-transparent rounded-full animate-spin"
        ></div>
        <CheckCircleIcon 
          v-else-if="statusType === 'success'"
          class="h-4 w-4 text-green-500"
        />
        <ExclamationTriangleIcon 
          v-else-if="statusType === 'error'"
          class="h-4 w-4 text-red-500"
        />
        <span>{{ statusMessage }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useMainStore } from '@/stores/main'
import type { DocumentSummary } from '@/types/domain'
import {
  StarIcon,
  ArrowPathIcon,
  PencilIcon,
  ScissorsIcon,
  DocumentDuplicateIcon,
  CheckCircleIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'

interface Props {
  selectedDocument?: DocumentSummary | null
}

interface Emits {
  (e: 'document-rotated', documentId: number, angle: number): void
  (e: 'redaction-started', documentId: number): void
  (e: 'document-split', documentId: number): void
  (e: 'document-stacked', documentId: number): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const mainStore = useMainStore()

// Component state
const dragMode = ref(false)
const isFavorite = ref(false)
const selectedDocumentType = ref('')
const loadingRotation = ref(false)
const loadingRedaction = ref(false)
const loadingClassification = ref(false)

// Status messaging
const statusMessage = ref('')
const statusType = ref<'loading' | 'success' | 'error' | null>(null)
const showStatus = computed(() => !!statusMessage.value)

// Document types for classification
const documentTypes = [
  { value: 'deed_of_trust', label: 'Deed of Trust' },
  { value: 'promissory_note', label: 'Promissory Note' },
  { value: 'appraisal', label: 'Appraisal' },
  { value: 'title_report', label: 'Title Report' },
  { value: 'insurance', label: 'Insurance' },
  { value: 'income_verification', label: 'Income Verification' },
  { value: 'credit_report', label: 'Credit Report' },
  { value: 'property_inspection', label: 'Property Inspection' },
  { value: 'closing_disclosure', label: 'Closing Disclosure' },
  { value: 'loan_application', label: 'Loan Application' },
  { value: 'other', label: 'Other' }
]

// Watch for document changes
watch(() => props.selectedDocument, (newDoc) => {
  if (newDoc) {
    selectedDocumentType.value = newDoc.documentType || ''
    isFavorite.value = newDoc.isFavorite || false
    clearStatus()
  } else {
    selectedDocumentType.value = ''
    isFavorite.value = false
    clearStatus()
  }
})

// Status management
const setStatus = (message: string, type: 'loading' | 'success' | 'error') => {
  statusMessage.value = message
  statusType.value = type
  
  if (type !== 'loading') {
    setTimeout(clearStatus, 3000)
  }
}

const clearStatus = () => {
  statusMessage.value = ''
  statusType.value = null
}

// Tool functions
const toggleDragMode = () => {
  dragMode.value = !dragMode.value
}

const toggleFavorite = async () => {
  if (!props.selectedDocument) return
  
  try {
    isFavorite.value = !isFavorite.value
    // TODO: Implement favorite functionality in API
    setStatus(isFavorite.value ? 'Added to favorites' : 'Removed from favorites', 'success')
  } catch (error) {
    isFavorite.value = !isFavorite.value
    setStatus('Failed to update favorite status', 'error')
  }
}

const rotateDocument = async (angle: number) => {
  if (!props.selectedDocument) return
  
  loadingRotation.value = true
  setStatus(`Rotating document ${angle}°...`, 'loading')
  
  try {
    await mainStore.rotateDocument(props.selectedDocument.id, angle)
    emit('document-rotated', props.selectedDocument.id, angle)
    setStatus(`Document rotated ${angle}° successfully`, 'success')
  } catch (error) {
    setStatus('Failed to rotate document', 'error')
    console.error('Failed to rotate document:', error)
  } finally {
    loadingRotation.value = false
  }
}

const startRedaction = () => {
  if (!props.selectedDocument) return
  
  loadingRedaction.value = true
  setStatus('Starting redaction mode...', 'loading')
  
  // Emit event to parent component to handle redaction UI
  emit('redaction-started', props.selectedDocument.id)
  
  setTimeout(() => {
    loadingRedaction.value = false
    setStatus('Redaction mode activated - click and drag to mark areas', 'success')
  }, 500)
}

const splitDocument = () => {
  if (!props.selectedDocument) return
  
  setStatus('Document splitting not yet implemented', 'error')
  emit('document-split', props.selectedDocument.id)
}

const stackDocument = () => {
  if (!props.selectedDocument) return
  
  setStatus('Document stacking not yet implemented', 'error')
  emit('document-stacked', props.selectedDocument.id)
}

const updateDocumentType = async () => {
  if (!props.selectedDocument || !selectedDocumentType.value) return
  
  loadingClassification.value = true
  setStatus('Updating document classification...', 'loading')
  
  try {
    await mainStore.updateDocumentType(props.selectedDocument.id, selectedDocumentType.value)
    setStatus('Document type updated successfully', 'success')
  } catch (error) {
    setStatus('Failed to update document type', 'error')
    console.error('Failed to update document type:', error)
  } finally {
    loadingClassification.value = false
  }
}
</script>

<style scoped>
.document-toolbar {
  min-height: 60px;
}

.rotate-90 {
  transform: rotate(90deg);
}

.rotate-180 {
  transform: rotate(180deg);
}

.rotate-270 {
  transform: rotate(270deg);
}

/* Custom rotation classes for buttons */
button .rotate-270 {
  transform: rotate(-90deg);
}
</style>