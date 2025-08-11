<template>
  <div class="h-full flex flex-col">
    <!-- Project Info Section -->
    <div class="p-3 border-b border-gray-200 bg-gray-50">
      <div class="space-y-3">
        <!-- Project Info Dropdown -->
        <div>
          <label class="block text-xs font-medium text-gray-700 mb-1">
            Project Info ({{ mainStore.userOfferings.length }} offerings)
          </label>
          <select
            class="selection-dropdown"
            :value="mainStore.selectedOffering?.offeringId || ''"
            @change="handleOfferingChange($event)"
            :disabled="mainStore.loading.offerings"
          >
            <option value="">Select Offering...</option>
            <option
              v-for="offering in mainStore.userOfferings"
              :key="offering.offeringId"
              :value="offering.offeringId"
            >
              {{ offering.offeringName }} - {{ offering.offeringDescription }}
            </option>
          </select>
        </div>

        <!-- Sales Dropdown -->
        <div v-if="mainStore.selectedOffering">
          <label class="block text-xs font-medium text-gray-700 mb-1">
            Sales ({{ mainStore.availableSales.length }} sales)
          </label>
          <select
            class="selection-dropdown"
            :value="mainStore.selectedSale?.saleId || ''"
            @change="handleSaleChange($event)"
            :disabled="mainStore.loading.sales"
          >
            <option value="">Select Sale...</option>
            <option
              v-for="sale in mainStore.availableSales"
              :key="sale.saleId"
              :value="sale.saleId"
            >
              {{ sale.saleDesc }} ({{ sale.loansCount }} loans)
            </option>
          </select>
        </div>

        <!-- Loading indicator for selections -->
        <div v-if="mainStore.loading.offerings || mainStore.loading.sales" class="flex items-center space-x-2 text-xs text-gray-500">
          <div class="spinner !h-3 !w-3"></div>
          <span>Loading...</span>
        </div>
      </div>
    </div>

    <!-- Loan List Section -->
    <div class="flex-1 flex flex-col overflow-hidden">
      <!-- Navigation and Search Header -->
      <div class="p-3 border-b border-gray-200 bg-gray-50">
        <!-- Back to Loans Button (shown when document is selected) -->
        <div v-if="mainStore.selectedDocument" class="flex items-center space-x-2 mb-3">
          <button
            @click="backToLoanSelection"
            class="flex items-center space-x-2 px-3 py-2 text-sm text-hydra-600 hover:text-hydra-700 hover:bg-hydra-50 rounded-md transition-colors"
          >
            <ArrowLeftIcon class="h-4 w-4" />
            <span>Back to Loans</span>
          </button>
          <div class="flex-1 text-xs text-gray-500 truncate">
            Viewing: {{ mainStore.selectedDocument.originalName }}
          </div>
        </div>
        
        <!-- Workflow Status -->
        <div v-if="!mainStore.selectedDocument && mainStore.selectedLoan" class="flex items-center justify-between mb-2">
          <div class="text-xs font-medium text-gray-700">
            Loan: {{ mainStore.selectedLoan.assetName }}
          </div>
          <div class="text-xs text-gray-500">
            {{ mainStore.availableDocuments.length }} docs
          </div>
        </div>
        
        <!-- Search Loans -->
        <input
          type="text"
          placeholder="Search Loans"
          class="w-full px-3 py-1 text-sm border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
          v-model="loanSearchInput"
          @input="handleLoanSearch"
          :disabled="!mainStore.selectedSale || mainStore.loading.loans"
        />
      </div>

      <!-- Loan List -->
      <div class="flex-1 overflow-y-auto">
        <div v-if="mainStore.loading.loans" class="p-4 text-center">
          <div class="spinner mx-auto mb-2"></div>
          <p class="text-sm text-gray-600">Loading loans...</p>
        </div>

        <div v-else-if="!mainStore.selectedSale" class="p-4 text-center text-sm text-gray-500">
          Select a sale to view loans
        </div>

        <div v-else-if="mainStore.visibleLoans.length === 0" class="p-4 text-center text-sm text-gray-500">
          No loans found
          <div v-if="loanSearchInput.trim()" class="mt-1">
            <button 
              @click="clearLoanSearch"
              class="text-hydra-600 hover:text-hydra-700 text-xs"
            >
              Clear search
            </button>
          </div>
        </div>

        <div v-else class="loan-list">
          <div
            v-for="loan in mainStore.visibleLoans"
            :key="loan.loanId"
            :class="[
              'loan-list-item cursor-pointer',
              {
                'selected': mainStore.selectedLoan?.loanId === loan.loanId
              }
            ]"
            @click="selectLoan(loan)"
          >
            <div class="flex justify-between items-start">
              <div class="flex-1 min-w-0">
                <div class="text-sm font-medium text-gray-900 truncate">
                  {{ loan.assetName }}
                </div>
                <div class="text-xs text-gray-500 truncate">
                  {{ loan.assetNo }} 
                  <span v-if="loan.assetName2"> • {{ loan.assetName2 }}</span>
                </div>
                <div class="text-xs text-gray-400 mt-1">
                  {{ loan.documentCount }} docs
                  <span v-if="loan.bookBalance"> • ${{ formatCurrency(loan.bookBalance) }}</span>
                </div>
              </div>
              <div class="text-xs text-gray-400 ml-2">
                {{ formatDate(loan.loadedOn) }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Document Data Section -->
    <div class="border-t border-gray-200 bg-gray-50 p-3">
      <div class="space-y-3">
        <div class="flex items-center justify-between">
          <h3 class="text-sm font-medium text-gray-700">Document Data</h3>
          <div class="flex items-center space-x-2">
            <ChevronUpIcon class="h-4 w-4 text-gray-400" />
            <ChevronDownIcon class="h-4 w-4 text-gray-400" />
          </div>
        </div>

        <!-- Date input -->
        <div class="flex items-center space-x-2">
          <input
            type="date"
            class="flex-1 px-3 py-1 text-sm border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500"
            :value="documentDate"
            @change="handleDocumentDateChange($event)"
          />
          <select class="px-2 py-1 text-sm border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-hydra-500 focus:border-hydra-500">
            <option>Year</option>
          </select>
        </div>

        <!-- Save Image Data Button -->
        <button
          class="w-full px-3 py-2 bg-hydra-600 text-white text-sm font-medium rounded hover:bg-hydra-700 focus:outline-none focus:ring-2 focus:ring-hydra-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
          :disabled="!mainStore.selectedDocument"
          @click="saveImageData"
        >
          Save Image Data
        </button>
      </div>
    </div>

    <!-- Document Indexing Section (when document is selected) -->
    <div v-if="mainStore.selectedDocument" class="border-t border-gray-200 flex-1 flex flex-col">
      <!-- Indexing Toolbar -->
      <div class="border-b border-gray-200 bg-gray-50 max-h-80 min-h-0 flex flex-col">
        <IndexingToolbar
          :selected-document="mainStore.selectedDocument"
          :available-document-types="indexingStore.availableDocumentTypes"
          :selected-document-type="indexingStore.selectedDocumentType"
          :current-offering="mainStore.selectedOffering"
          :current-page="mainStore.currentPage"
          :loading="indexingStore.documentTypesLoading"
          @document-type-selected="handleDocumentTypeSelected"
          @document-type-cleared="handleDocumentTypeCleared"
          @set-break-clicked="handleSetBreakClicked"
          @save-image-data-clicked="handleSaveImageData"
        />
      </div>
      
      <!-- Document Indexing - Lower Section -->
      <div class="flex-1 flex flex-col min-h-0">
        <!-- Document Indexing Information -->
        <div class="flex-1 bg-white border-t border-gray-200 min-h-0">
          <div class="p-3 border-b border-gray-200 bg-gray-50">
            <h3 class="text-sm font-medium text-gray-700">Indexes</h3>
          </div>
          
          <!-- Document Type Information -->
          <div class="p-3 space-y-3">
            <div v-if="indexingStore.selectedDocumentType">
              <div class="text-xs font-medium text-gray-700 mb-1">Selected Document Type:</div>
              <div class="text-sm text-gray-900 bg-blue-50 border border-blue-200 rounded px-2 py-1">
                {{ indexingStore.selectedDocumentType.name }}
              </div>
            </div>
            
            <!-- Bookmarks/Indexes List -->
            <div>
              <div class="text-xs font-medium text-gray-700 mb-2">Indexes:</div>
              <div class="space-y-1 max-h-32 overflow-y-auto">
                <div 
                  v-for="(bookmark, index) in indexingStore.pendingBookmarks" 
                  :key="bookmark.id || index"
                  class="flex items-center justify-between text-xs p-2 bg-gray-50 border border-gray-200 rounded"
                >
                  <div class="flex-1 min-w-0">
                    <div class="font-medium text-gray-900 truncate">
                      {{ bookmark.documentTypeName || 'Unclassified' }}
                    </div>
                    <div class="text-gray-500">Page {{ bookmark.pageIndex + 1 }}</div>
                  </div>
                  <button 
                    @click="handleNavigateToBookmark(bookmark)"
                    class="text-blue-600 hover:text-blue-700 ml-2"
                    title="Navigate to page"
                  >
                    →
                  </button>
                </div>
                <div 
                  v-if="indexingStore.pendingBookmarks.length === 0"
                  class="text-xs text-gray-500 text-center py-4"
                >
                  No document indexes found
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useMainStore } from '@/stores/main'
import IndexingToolbar from '@/components/indexing/IndexingToolbar.vue'
import { useIndexingStore } from '@/stores/indexing'
import type { Offering, Sale, Loan } from '@/types/domain'
import {
  ChevronUpIcon,
  ChevronDownIcon,
  ArrowLeftIcon
} from '@heroicons/vue/24/outline'

const mainStore = useMainStore()
const indexingStore = useIndexingStore()

// Local state
const loanSearchInput = ref('')
const documentDate = ref(new Date().toISOString().split('T')[0])

// Handle offering selection
const handleOfferingChange = (event: Event) => {
  const target = event.target as HTMLSelectElement
  const offeringId = parseInt(target.value)
  
  if (offeringId) {
    const offering = mainStore.userOfferings.find(o => o.offeringId === offeringId)
    if (offering) {
      mainStore.selectOffering(offering)
    }
  }
}

// Handle sale selection
const handleSaleChange = (event: Event) => {
  const target = event.target as HTMLSelectElement
  const saleId = parseInt(target.value)
  
  if (saleId) {
    const sale = mainStore.availableSales.find(s => s.saleId === saleId)
    if (sale) {
      mainStore.selectSale(sale)
    }
  }
}

// Handle loan search
let searchTimeout: number | null = null

const handleLoanSearch = () => {
  // Clear existing timeout
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
  
  // Debounce search
  searchTimeout = setTimeout(() => {
    mainStore.updateLoanSearch(loanSearchInput.value)
  }, 300)
}

const clearLoanSearch = () => {
  loanSearchInput.value = ''
  mainStore.updateLoanSearch('')
}

// Handle loan selection
const selectLoan = (loan: Loan) => {
  mainStore.selectLoan(loan)
}

// Navigation functions
const backToLoanSelection = () => {
  // Clear selected document to return to loan list view
  mainStore.selectedDocument = null
  mainStore.selectedDocumentDetails = null
  mainStore.documentUrl = null
  mainStore.currentPage = 1
  mainStore.totalPages = 0
}

// Handle document date change
const handleDocumentDateChange = (event: Event) => {
  const target = event.target as HTMLInputElement
  documentDate.value = target.value
}


// Save image data
const saveImageData = () => {
  if (!mainStore.selectedDocument) return
  
  // TODO: Implement save image data functionality
  console.log('Saving image data for document:', mainStore.selectedDocument.id)
}


// Utility functions
const formatDate = (date: Date | string) => {
  const dateObj = typeof date === 'string' ? new Date(date) : date
  return dateObj.toLocaleDateString('en-US', {
    month: 'numeric',
    day: 'numeric',
    year: '2-digit'
  })
}

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0
  }).format(amount)
}

const getStatusColor = (statusId: number) => {
  switch (statusId) {
    case 1:
      return 'text-yellow-600'
    case 2:
      return 'text-green-600'
    case 3:
      return 'text-red-600'
    default:
      return 'text-gray-600'
  }
}

const getStatusText = (statusId: number) => {
  switch (statusId) {
    case 1:
      return 'Processing'
    case 2:
      return 'Ready'
    case 3:
      return 'Error'
    default:
      return 'Unknown'
  }
}

// Indexing event handlers
const handleDocumentTypeSelected = (documentType: any) => {
  indexingStore.selectDocumentType(documentType)
}

const handleDocumentTypeCleared = () => {
  indexingStore.clearDocumentTypeSelection()
}

const handleSetBreakClicked = async (pageIndex: number) => {
  if (!mainStore.selectedDocument) return
  try {
    await indexingStore.createBookmark(mainStore.selectedDocument.id, pageIndex)
  } catch (error) {
    console.error('Failed to create bookmark:', error)
  }
}

const handleSaveImageData = async (data: any) => {
  if (!mainStore.selectedDocument) return
  try {
    await indexingStore.executeSaveProcess(mainStore.selectedDocument.id)
  } catch (error) {
    console.error('Failed to save image data:', error)
  }
}

const handlePageSelected = (pageNumber: number) => {
  mainStore.currentPage = pageNumber
}

const handleThumbnailLoaded = (pageNumber: number) => {
  // Handle thumbnail loading completion
  console.log('Thumbnail loaded for page:', pageNumber)
}

const handleNavigateToBookmark = (bookmark: any) => {
  // Navigate to the bookmark's page
  mainStore.currentPage = bookmark.pageIndex + 1 // Convert from 0-based to 1-based
}

// Load document types and bookmarks on mount and when selection changes
onMounted(() => {
  if (mainStore.selectedOffering) {
    indexingStore.loadDocumentTypesForOffering(mainStore.selectedOffering.offeringId)
  }
  if (mainStore.selectedDocument) {
    indexingStore.loadBookmarksForDocument(mainStore.selectedDocument.id)
  }
})

watch(() => mainStore.selectedOffering?.offeringId, (offeringId) => {
  if (offeringId) {
    indexingStore.loadDocumentTypesForOffering(offeringId)
  }
})

watch(() => mainStore.selectedDocument?.id, (docId) => {
  if (docId) {
    indexingStore.loadBookmarksForDocument(docId)
  }
})
</script>

<style scoped>
/* Additional component-specific styles */
.loan-list {
  /* Ensure loan list gets adequate space */
  min-height: 300px;
  max-height: 60vh;
}

.loan-list-item {
  @apply px-3 py-2 border-b border-gray-100 hover:bg-gray-50 transition-colors;
}

.loan-list-item.selected {
  @apply bg-orange-100 border-l-4 border-l-orange-400;
}

/* Ensure back button is prominent */
.loan-list-item:hover {
  @apply shadow-sm;
}

/* Improve scrollbar styling for loan list */
.loan-list::-webkit-scrollbar {
  width: 6px;
}

.loan-list::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.loan-list::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.loan-list::-webkit-scrollbar-thumb:hover {
  background: #a1a1a1;
}
</style>
