<template>
  <div class="document-classifier">
    <!-- Classification Header -->
    <div class="bg-gray-50 px-3 py-2 border-b border-gray-200">
      <div class="flex items-center justify-between">
        <button 
          @click="toggleExpanded"
          class="flex items-center space-x-2 text-sm font-medium text-gray-700 hover:text-gray-900 transition-colors"
        >
          <ChevronRightIcon 
            :class="[
              'h-4 w-4 transform transition-transform',
              { 'rotate-90': isExpanded }
            ]" 
          />
          <span>Document Classification</span>
          <span v-if="selectedDocument && !isExpanded" class="text-xs text-gray-500">
            ({{ selectedDocument.documentType || 'Unclassified' }})
          </span>
        </button>
        <div class="flex items-center space-x-2">
          <span :class="['text-xs px-2 py-1 rounded', getConfidenceColor()]">
            {{ confidenceText }}
          </span>
          <button
            @click="toggleExpanded"
            class="text-xs text-gray-500 hover:text-gray-700"
            :title="isExpanded ? 'Collapse' : 'Expand'"
          >
            {{ isExpanded ? 'Collapse' : 'Expand' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Collapsible Content -->
    <div v-show="isExpanded" class="transition-all duration-200">
      <!-- Document Info -->
    <div v-if="selectedDocument" class="p-3 bg-white border-b border-gray-200">
      <div class="space-y-2 text-xs">
        <div class="flex justify-between">
          <span class="text-gray-600">Document:</span>
          <span class="font-medium text-gray-900 truncate ml-2">{{ selectedDocument.originalName }}</span>
        </div>
        <div class="flex justify-between">
          <span class="text-gray-600">Pages:</span>
          <span class="text-gray-900">{{ selectedDocument.pageCount || 0 }}</span>
        </div>
        <div class="flex justify-between">
          <span class="text-gray-600">Current Type:</span>
          <span :class="['font-medium', selectedDocument.documentType ? 'text-green-700' : 'text-red-600']">
            {{ selectedDocument.documentType || 'Unclassified' }}
          </span>
        </div>
      </div>
    </div>

    <!-- Quick Classification Buttons -->
    <div class="p-3 bg-gray-50 border-b border-gray-200">
      <div class="mb-2">
        <label class="text-xs font-medium text-gray-700">Quick Classify:</label>
      </div>
      <div class="grid grid-cols-2 gap-2">
        <button
          v-for="quickType in quickClassificationTypes"
          :key="quickType.value"
          :class="[
            'p-2 text-xs rounded border transition-colors',
            {
              'bg-hydra-600 text-white border-hydra-600': selectedDocumentType === quickType.value,
              'bg-white text-gray-700 border-gray-300 hover:bg-gray-50': selectedDocumentType !== quickType.value
            }
          ]"
          @click="setDocumentType(quickType.value)"
          :disabled="!selectedDocument || updating"
        >
          <div class="font-medium">{{ quickType.label }}</div>
          <div class="text-xs opacity-75">{{ quickType.description }}</div>
        </button>
      </div>
    </div>

    <!-- Advanced Classification -->
    <div class="p-3">
      <div class="space-y-3">
        <!-- Category Selection -->
        <div>
          <label class="block text-xs font-medium text-gray-700 mb-1">
            Document Category
          </label>
          <select
            v-model="selectedCategory"
            class="w-full px-3 py-2 text-sm border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
            @change="handleCategoryChange"
            :disabled="!selectedDocument || updating"
          >
            <option value="">Select category...</option>
            <option v-for="category in documentCategories" :key="category.value" :value="category.value">
              {{ category.label }}
            </option>
          </select>
        </div>

        <!-- Sub-type Selection -->
        <div v-if="selectedCategory">
          <label class="block text-xs font-medium text-gray-700 mb-1">
            Document Type
          </label>
          <select
            v-model="selectedDocumentType"
            class="w-full px-3 py-2 text-sm border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
            @change="handleDocumentTypeChange"
            :disabled="!selectedDocument || updating"
          >
            <option value="">Select specific type...</option>
            <option 
              v-for="type in filteredDocumentTypes" 
              :key="type.value" 
              :value="type.value"
            >
              {{ type.label }}
            </option>
          </select>
        </div>

        <!-- Classification Confidence -->
        <div v-if="selectedDocumentType">
          <label class="block text-xs font-medium text-gray-700 mb-1">
            Classification Confidence
          </label>
          <div class="space-y-2">
            <div class="flex items-center space-x-2">
              <input
                type="range"
                min="0"
                max="100"
                v-model="classificationConfidence"
                class="flex-1 h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer"
                :disabled="!selectedDocument || updating"
              />
              <span class="text-xs text-gray-600 w-12">{{ classificationConfidence }}%</span>
            </div>
            <div class="text-xs text-gray-500">
              How confident are you in this classification?
            </div>
          </div>
        </div>

        <!-- Additional Attributes -->
        <div v-if="selectedDocumentType" class="border-t border-gray-200 pt-3">
          <label class="block text-xs font-medium text-gray-700 mb-2">
            Additional Attributes
          </label>
          <div class="space-y-2">
            <label class="flex items-center">
              <input
                type="checkbox"
                v-model="documentAttributes.requiresReview"
                class="h-3 w-3 text-hydra-600 focus:ring-hydra-500 border-gray-300 rounded"
              />
              <span class="ml-2 text-xs text-gray-700">Requires Review</span>
            </label>
            <label class="flex items-center">
              <input
                type="checkbox"
                v-model="documentAttributes.isComplete"
                class="h-3 w-3 text-hydra-600 focus:ring-hydra-500 border-gray-300 rounded"
              />
              <span class="ml-2 text-xs text-gray-700">Document Complete</span>
            </label>
            <label class="flex items-center">
              <input
                type="checkbox"
                v-model="documentAttributes.hasExceptions"
                class="h-3 w-3 text-hydra-600 focus:ring-hydra-500 border-gray-300 rounded"
              />
              <span class="ml-2 text-xs text-gray-700">Has Exceptions</span>
            </label>
          </div>
        </div>

        <!-- Notes -->
        <div v-if="selectedDocumentType">
          <label class="block text-xs font-medium text-gray-700 mb-1">
            Classification Notes
          </label>
          <textarea
            v-model="classificationNotes"
            rows="3"
            class="w-full px-3 py-2 text-xs border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
            placeholder="Add any notes about this classification..."
            :disabled="!selectedDocument || updating"
          ></textarea>
        </div>

        <!-- Action Buttons -->
        <div class="flex space-x-2 pt-2">
          <button
            :disabled="!canSave || updating"
            @click="saveClassification"
            :class="[
              'flex-1 px-3 py-2 text-sm font-medium rounded transition-colors',
              {
                'bg-hydra-600 text-white hover:bg-hydra-700': canSave && !updating,
                'bg-gray-400 text-gray-700 cursor-not-allowed': !canSave || updating
              }
            ]"
          >
            <span v-if="updating" class="flex items-center justify-center">
              <div class="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin mr-2"></div>
              Saving...
            </span>
            <span v-else>Save Classification</span>
          </button>
          
          <button
            @click="clearClassification"
            :disabled="!selectedDocument || updating"
            class="px-3 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Clear
          </button>
        </div>
      </div>
    </div>

    <!-- Classification History -->
    <div v-if="selectedDocument && classificationHistory.length > 0" class="border-t border-gray-200">
      <div class="bg-gray-50 px-3 py-2">
        <h4 class="text-xs font-medium text-gray-700">Classification History</h4>
      </div>
      <div class="p-3 max-h-32 overflow-y-auto">
        <div v-for="entry in classificationHistory" :key="entry.id" class="text-xs text-gray-600 mb-2 last:mb-0">
          <div class="flex justify-between items-start">
            <span class="font-medium">{{ entry.documentType }}</span>
            <span class="text-gray-400">{{ formatDate(entry.timestamp) }}</span>
          </div>
          <div class="text-gray-500">by {{ entry.userName }} ({{ entry.confidence }}% confidence)</div>
        </div>
      </div>
    </div>

    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useMainStore } from '@/stores/main'
import type { DocumentSummary } from '@/types/domain'
import { ChevronRightIcon } from '@heroicons/vue/24/outline'

interface Props {
  selectedDocument?: DocumentSummary | null
}

interface ClassificationEntry {
  id: number
  documentType: string
  confidence: number
  userName: string
  timestamp: Date
  notes?: string
}

const props = defineProps<Props>()

const mainStore = useMainStore()

// State
const selectedCategory = ref('')
const selectedDocumentType = ref('')
const classificationConfidence = ref(85)
const updating = ref(false)
const classificationNotes = ref('')
const isExpanded = ref(false)

// Toggle expanded state
const toggleExpanded = () => {
  isExpanded.value = !isExpanded.value
}

const documentAttributes = ref({
  requiresReview: false,
  isComplete: true,
  hasExceptions: false
})

// Mock classification history
const classificationHistory = ref<ClassificationEntry[]>([])

// Quick classification types
const quickClassificationTypes = [
  { value: 'deed_of_trust', label: 'Deed of Trust', description: 'Security instrument' },
  { value: 'promissory_note', label: 'Promissory Note', description: 'Payment promise' },
  { value: 'appraisal', label: 'Appraisal', description: 'Property valuation' },
  { value: 'title_report', label: 'Title Report', description: 'Title examination' }
]

// Document categories
const documentCategories = [
  { value: 'loan_documents', label: 'Loan Documents' },
  { value: 'property_documents', label: 'Property Documents' },
  { value: 'financial_documents', label: 'Financial Documents' },
  { value: 'legal_documents', label: 'Legal Documents' },
  { value: 'regulatory_documents', label: 'Regulatory Documents' },
  { value: 'other', label: 'Other Documents' }
]

// All document types by category
const documentTypesByCategory = {
  loan_documents: [
    { value: 'promissory_note', label: 'Promissory Note' },
    { value: 'loan_agreement', label: 'Loan Agreement' },
    { value: 'loan_application', label: 'Loan Application' },
    { value: 'credit_report', label: 'Credit Report' },
    { value: 'income_verification', label: 'Income Verification' }
  ],
  property_documents: [
    { value: 'deed_of_trust', label: 'Deed of Trust' },
    { value: 'appraisal', label: 'Appraisal' },
    { value: 'title_report', label: 'Title Report' },
    { value: 'property_inspection', label: 'Property Inspection' },
    { value: 'survey', label: 'Property Survey' }
  ],
  financial_documents: [
    { value: 'closing_disclosure', label: 'Closing Disclosure' },
    { value: 'settlement_statement', label: 'Settlement Statement' },
    { value: 'bank_statements', label: 'Bank Statements' },
    { value: 'tax_returns', label: 'Tax Returns' }
  ],
  legal_documents: [
    { value: 'purchase_agreement', label: 'Purchase Agreement' },
    { value: 'power_of_attorney', label: 'Power of Attorney' },
    { value: 'affidavit', label: 'Affidavit' },
    { value: 'legal_opinion', label: 'Legal Opinion' }
  ],
  regulatory_documents: [
    { value: 'disclosure_statement', label: 'Disclosure Statement' },
    { value: 'compliance_certificate', label: 'Compliance Certificate' },
    { value: 'regulatory_filing', label: 'Regulatory Filing' }
  ],
  other: [
    { value: 'correspondence', label: 'Correspondence' },
    { value: 'photos', label: 'Photos' },
    { value: 'miscellaneous', label: 'Miscellaneous' }
  ]
}

// Computed properties
const filteredDocumentTypes = computed(() => {
  if (!selectedCategory.value) return []
  return documentTypesByCategory[selectedCategory.value as keyof typeof documentTypesByCategory] || []
})

const canSave = computed(() => {
  return props.selectedDocument && selectedDocumentType.value && !updating.value
})

const confidenceText = computed(() => {
  if (!selectedDocumentType.value) return 'Not Classified'
  if (classificationConfidence.value >= 90) return 'High Confidence'
  if (classificationConfidence.value >= 70) return 'Medium Confidence'
  return 'Low Confidence'
})

// Methods
const getConfidenceColor = () => {
  if (!selectedDocumentType.value) return 'bg-gray-100 text-gray-600'
  if (classificationConfidence.value >= 90) return 'bg-green-100 text-green-800'
  if (classificationConfidence.value >= 70) return 'bg-yellow-100 text-yellow-800'
  return 'bg-red-100 text-red-800'
}

const setDocumentType = (type: string) => {
  selectedDocumentType.value = type
  
  // Auto-set category based on type
  for (const [category, types] of Object.entries(documentTypesByCategory)) {
    if (types.some(t => t.value === type)) {
      selectedCategory.value = category
      break
    }
  }
  
  // Auto-save quick classification
  saveClassification()
}

const handleCategoryChange = () => {
  selectedDocumentType.value = ''
}

const handleDocumentTypeChange = () => {
  // Could trigger auto-save here if desired
}

const saveClassification = async () => {
  if (!props.selectedDocument || !selectedDocumentType.value) return
  
  updating.value = true
  
  try {
    await mainStore.updateDocumentType(props.selectedDocument.id, selectedDocumentType.value)
    
    // Add to classification history
    classificationHistory.value.unshift({
      id: Date.now(),
      documentType: selectedDocumentType.value,
      confidence: classificationConfidence.value,
      userName: 'Current User', // TODO: Get from auth
      timestamp: new Date(),
      notes: classificationNotes.value
    })
    
    console.log('Document classification saved successfully')
  } catch (error) {
    console.error('Failed to save document classification:', error)
  } finally {
    updating.value = false
  }
}

const clearClassification = () => {
  selectedCategory.value = ''
  selectedDocumentType.value = ''
  classificationConfidence.value = 85
  classificationNotes.value = ''
  documentAttributes.value = {
    requiresReview: false,
    isComplete: true,
    hasExceptions: false
  }
}

const formatDate = (date: Date) => {
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  })
}

// Watch for document changes
watch(() => props.selectedDocument, (newDoc) => {
  if (newDoc) {
    selectedDocumentType.value = newDoc.documentType || ''
    
    // Set category based on existing document type
    if (newDoc.documentType) {
      for (const [category, types] of Object.entries(documentTypesByCategory)) {
        if (types.some(t => t.value === newDoc.documentType)) {
          selectedCategory.value = category
          break
        }
      }
    }
    
    // Auto-expand if document is unclassified
    if (!newDoc.documentType) {
      isExpanded.value = true
    }
  } else {
    clearClassification()
    isExpanded.value = false
  }
})
</script>

<style scoped>
.document-classifier {
  border-top: 1px solid #e5e7eb;
}

/* Custom range slider styling */
input[type="range"] {
  -webkit-appearance: none;
  appearance: none;
}

input[type="range"]::-webkit-slider-thumb {
  -webkit-appearance: none;
  appearance: none;
  height: 20px;
  width: 20px;
  border-radius: 50%;
  background: #dc2626;
  cursor: pointer;
}

input[type="range"]::-moz-range-thumb {
  height: 20px;
  width: 20px;
  border-radius: 50%;
  background: #dc2626;
  cursor: pointer;
  border: none;
}
</style>
