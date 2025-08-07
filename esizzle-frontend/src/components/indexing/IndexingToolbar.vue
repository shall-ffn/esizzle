<template>
  <div class="indexing-toolbar">
    <!-- Toolbar Header -->
    <div class="bg-gray-50 px-3 py-2 border-b border-gray-200">
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-medium text-gray-700">Document Indexing</h3>
        <div class="flex items-center space-x-2">
          <span v-if="selectedDocumentType" class="text-xs text-green-600 font-medium">
            {{ selectedDocumentType.name }}
          </span>
          <button
            v-if="selectedDocumentType"
            @click="clearSelection"
            class="text-xs text-gray-500 hover:text-gray-700"
            title="Clear selection"
          >
            <XMarkIcon class="h-3 w-3" />
          </button>
        </div>
      </div>
    </div>

    <!-- Search Filter -->
    <div class="p-3 bg-white border-b border-gray-200">
      <div class="relative">
        <input
          type="text"
          v-model="searchFilter"
          placeholder="Search document types..."
          class="w-full pl-8 pr-3 py-2 text-sm border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
          :disabled="loading || !availableDocumentTypes.length"
        />
        <MagnifyingGlassIcon class="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
      </div>
    </div>

    <!-- Document Types List -->
    <div class="flex-1 overflow-y-auto">
      <div v-if="loading" class="p-4 text-center">
        <div class="spinner mx-auto mb-2"></div>
        <p class="text-sm text-gray-600">Loading document types...</p>
      </div>

      <div v-else-if="!currentOffering" class="p-4 text-center text-sm text-gray-500">
        Select an offering to view available document types
      </div>

      <div v-else-if="filteredDocumentTypes.length === 0" class="p-4 text-center text-sm text-gray-500">
        No document types found
        <div v-if="searchFilter.trim()" class="mt-1">
          <button 
            @click="clearSearch"
            class="text-hydra-600 hover:text-hydra-700 text-xs"
          >
            Clear search
          </button>
        </div>
      </div>

      <div v-else class="document-types-list">
        <!-- Matched types (with constellation matches) appear first in bold -->
        <div v-for="docType in matchedTypes" :key="`matched-${docType.id}`" class="document-type-item matched">
          <button
            :class="[
              'w-full text-left px-3 py-3 transition-colors border-b border-gray-100 hover:bg-gray-50',
              {
                'bg-hydra-100 border-hydra-200': selectedDocumentType?.id === docType.id
              }
            ]"
            @click="selectDocumentType(docType)"
          >
            <div class="flex items-center justify-between">
              <div class="flex-1 min-w-0">
                <div class="text-sm font-bold text-gray-900 truncate">
                  {{ docType.name }}
                </div>
                <div v-if="docType.isGeneric" class="text-xs text-orange-600 mt-1">
                  Generic Document Type
                </div>
              </div>
              <div class="ml-2 text-xs text-green-600 font-medium">
                Matched
              </div>
            </div>
          </button>
        </div>

        <!-- Unmatched types appear below in regular formatting -->
        <div v-for="docType in unmatchedTypes" :key="`unmatched-${docType.id}`" class="document-type-item">
          <button
            :class="[
              'w-full text-left px-3 py-3 transition-colors border-b border-gray-100 hover:bg-gray-50',
              {
                'bg-hydra-100 border-hydra-200': selectedDocumentType?.id === docType.id
              }
            ]"
            @click="selectDocumentType(docType)"
          >
            <div class="flex items-center justify-between">
              <div class="flex-1 min-w-0">
                <div class="text-sm text-gray-900 truncate">
                  {{ docType.name }}
                </div>
                <div v-if="docType.isGeneric" class="text-xs text-orange-600 mt-1">
                  Generic Document Type
                </div>
              </div>
            </div>
          </button>
        </div>
      </div>
    </div>

    <!-- Action Button -->
    <div class="p-3 bg-gray-50 border-t border-gray-200">
      <button
        :disabled="!canTakeAction || loading"
        @click="handleActionButtonClick"
        :class="[
          'w-full px-4 py-2 text-sm font-medium rounded transition-colors',
          {
            'bg-hydra-600 text-white hover:bg-hydra-700': canTakeAction && !loading,
            'bg-gray-400 text-gray-700 cursor-not-allowed': !canTakeAction || loading
          }
        ]"
      >
        {{ actionButtonText }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useMainStore } from '@/stores/main'
import type { DocumentTypeDto, DocumentMetadata } from '@/types/indexing'
import type { DocumentSummary, Offering } from '@/types/domain'
import {
  MagnifyingGlassIcon,
  XMarkIcon
} from '@heroicons/vue/24/outline'

interface Props {
  selectedDocument: DocumentSummary | null
  availableDocumentTypes: DocumentTypeDto[]
  selectedDocumentType: DocumentTypeDto | null
  currentOffering: Offering | null
  currentPage: number
  loading: boolean
}

interface Emits {
  (e: 'document-type-selected', documentType: DocumentTypeDto): void
  (e: 'document-type-cleared'): void
  (e: 'set-break-clicked', pageIndex: number): void
  (e: 'save-image-data-clicked', data: DocumentMetadata): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const mainStore = useMainStore()

// Local state
const searchFilter = ref('')

// Computed properties
const filteredDocumentTypes = computed(() => {
  if (!searchFilter.value.trim()) {
    return props.availableDocumentTypes
  }
  
  const filter = searchFilter.value.toLowerCase()
  return props.availableDocumentTypes.filter(dt => 
    dt.name.toLowerCase().includes(filter)
  )
})

// Separate matched and unmatched types (for constellation matching)
// For now, we'll simulate this - in real implementation this would come from API
const matchedTypes = computed(() => {
  // Simulate some document types having constellation matches
  return filteredDocumentTypes.value.filter(dt => 
    dt.name.includes('Note') || dt.name.includes('Trust') || dt.name.includes('Agreement')
  )
})

const unmatchedTypes = computed(() => {
  return filteredDocumentTypes.value.filter(dt => 
    !matchedTypes.value.some(matched => matched.id === dt.id)
  )
})

const actionButtonText = computed(() => {
  if (props.selectedDocumentType && props.currentPage > 0) {
    return 'Set Break'
  }
  return 'Save Image Data'
})

const canTakeAction = computed(() => {
  return props.selectedDocument && !props.loading
})

// Methods
const selectDocumentType = (docType: DocumentTypeDto) => {
  emit('document-type-selected', docType)
}

const clearSelection = () => {
  emit('document-type-cleared')
}

const clearSearch = () => {
  searchFilter.value = ''
}

const handleActionButtonClick = () => {
  if (!props.selectedDocument) return
  
  if (props.selectedDocumentType && props.currentPage > 0) {
    // Set break at current page
    emit('set-break-clicked', props.currentPage - 1) // Convert to 0-based
  } else {
    // Save image data with current selections
    const metadata: DocumentMetadata = {
      documentTypeId: props.selectedDocumentType?.id,
      documentDate: new Date(),
      comments: ''
    }
    emit('save-image-data-clicked', metadata)
  }
}

// Watch for document changes to clear search
watch(() => props.selectedDocument, () => {
  searchFilter.value = ''
})
</script>

<style scoped>
.indexing-toolbar {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.document-types-list {
  max-height: 60vh;
  overflow-y: auto;
}

.document-type-item.matched {
  order: -1;
}

/* Spinner styling */
.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid #f3f4f6;
  border-top: 2px solid #dc2626;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* Scrollbar styling */
.document-types-list::-webkit-scrollbar {
  width: 6px;
}

.document-types-list::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.document-types-list::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.document-types-list::-webkit-scrollbar-thumb:hover {
  background: #a1a1a1;
}
</style>
