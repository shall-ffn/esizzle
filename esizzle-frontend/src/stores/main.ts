import { defineStore } from 'pinia'
import type {
  Offering,
  OfferingDetails,
  Sale,
  SaleDetails,
  Loan,
  LoanDetails,
  DocumentSummary,
  DocumentDetails,
  User,
  ViewMode,
  PanelSizes,
  LoadingStates
} from '@/types/domain'
import { hydraApi } from '@/services/hydra-api'
import { handleApiError } from '@/services/api'

interface MainState {
  // User context
  currentUser: User | null
  userOfferings: Offering[]
  
  // Selection hierarchy (preserving exact legacy workflow)
  selectedOffering: Offering | null
  selectedOfferingDetails: OfferingDetails | null
  availableSales: Sale[]
  selectedSale: Sale | null
  selectedSaleDetails: SaleDetails | null
  availableLoans: Loan[]
  selectedLoan: Loan | null
  selectedLoanDetails: LoanDetails | null
  loanSearchTerm: string
  
  // Document state
  availableDocuments: DocumentSummary[]
  selectedDocument: DocumentSummary | null
  selectedDocumentDetails: DocumentDetails | null
  currentPage: number
  totalPages: number
  documentUrl: string | null
  
  // UI state matching legacy
  documentViewMode: ViewMode
  zoomLevel: number
  panelSizes: PanelSizes
  
  // Loading states
  loading: LoadingStates
  
  // Error handling
  error: string | null
}

export const useMainStore = defineStore('main', {
  state: (): MainState => ({
    currentUser: null,
    userOfferings: [],
    
    selectedOffering: null,
    selectedOfferingDetails: null,
    availableSales: [],
    selectedSale: null,
    selectedSaleDetails: null,
    availableLoans: [],
    selectedLoan: null,
    selectedLoanDetails: null,
    loanSearchTerm: '',
    
    availableDocuments: [],
    selectedDocument: null,
    selectedDocumentDetails: null,
    currentPage: 1,
    totalPages: 0,
    documentUrl: null,
    
    documentViewMode: 'single',
    zoomLevel: 100,
    panelSizes: {
      left: 300,
      center: 800,
      right: 350
    },
    
    loading: {
      offerings: false,
      sales: false,
      loans: false,
      documents: false,
      documentContent: false
    },
    
    error: null
  }),

  getters: {
    // Filter loans based on search term (matching legacy "Search Loans" functionality)
    visibleLoans: (state) => {
      if (!state.loanSearchTerm.trim()) {
        return state.availableLoans
      }
      
      const searchTerm = state.loanSearchTerm.toLowerCase()
      return state.availableLoans.filter(loan => 
        loan.assetName.toLowerCase().includes(searchTerm) ||
        loan.assetNo?.toLowerCase().includes(searchTerm) ||
        loan.assetName2?.toLowerCase().includes(searchTerm)
      )
    },
    
    // Group documents by type (for indexing functionality)
    documentsByType: (state) => {
      return state.availableDocuments.reduce((acc, doc) => {
        const type = doc.documentType || 'Unclassified'
        if (!acc[type]) {
          acc[type] = []
        }
        acc[type].push(doc)
        return acc
      }, {} as Record<string, DocumentSummary[]>)
    },
    
    // Check if any data is loading
    isLoading: (state) => {
      return Object.values(state.loading).some(Boolean)
    },
    
    // Current selection path for breadcrumbs
    selectionPath: (state) => {
      const path = []
      if (state.selectedOffering) {
        path.push({ type: 'offering', name: state.selectedOffering.offeringName || 'Unknown Offering' })
      }
      if (state.selectedSale) {
        path.push({ type: 'sale', name: state.selectedSale.saleDesc })
      }
      if (state.selectedLoan) {
        path.push({ type: 'loan', name: state.selectedLoan.assetName })
      }
      return path
    }
  },

  actions: {
    // Error handling
    setError(error: string | null) {
      this.error = error
    },

    clearError() {
      this.error = null
    },

    // Load user's offerings
    async loadUserOfferings() {
      this.loading.offerings = true
      this.clearError()
      
      try {
        this.userOfferings = await hydraApi.getUserOfferings()
      } catch (error) {
        // Check if we're in mock auth mode and provide mock data
        const isMockAuth = import.meta.env.VITE_ENABLE_MOCK_AUTH === 'true'
        
        if (isMockAuth) {
          console.warn('🔄 Mock auth mode: Using mock offerings data')
          // Provide mock offerings data
          this.userOfferings = [
            {
              offeringId: 1,
              offeringName: 'Demo Offering 1',
              offeringDescription: 'Sample offering for development',
              clientId: 1,
              salesCount: 5
            },
            {
              offeringId: 2,
              offeringName: 'Demo Offering 2', 
              offeringDescription: 'Another sample offering',
              clientId: 1,
              salesCount: 3
            }
          ]
        } else {
          this.setError(handleApiError(error))
          console.error('Failed to load user offerings:', error)
        }
      } finally {
        this.loading.offerings = false
      }
    },

    // Selection workflow matching legacy cascading dropdowns
    async selectOffering(offering: Offering) {
      this.selectedOffering = offering
      this.selectedOfferingDetails = null
      this.selectedSale = null
      this.selectedSaleDetails = null
      this.selectedLoan = null
      this.selectedLoanDetails = null
      this.selectedDocument = null
      this.selectedDocumentDetails = null
      this.availableSales = []
      this.availableLoans = []
      this.availableDocuments = []
      this.documentUrl = null
      
      // Load offering details and sales
      await Promise.all([
        this.loadOfferingDetails(offering.offeringId),
        this.loadSales(offering.offeringId)
      ])
    },

    async loadOfferingDetails(offeringId: number) {
      this.clearError()
      
      try {
        this.selectedOfferingDetails = await hydraApi.getOffering(offeringId)
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to load offering details:', error)
      }
    },

    async loadSales(offeringId: number) {
      this.loading.sales = true
      this.clearError()
      
      try {
        this.availableSales = await hydraApi.getSalesByOffering(offeringId)
      } catch (error) {
        // Check if we're in mock auth mode and provide mock data
        const isMockAuth = import.meta.env.VITE_ENABLE_MOCK_AUTH === 'true'
        
        if (isMockAuth) {
          console.warn('🔄 Mock auth mode: Using mock sales data for offering', offeringId)
          // Provide mock sales data
          this.availableSales = [
            {
              saleId: offeringId * 10 + 1,
              saleDesc: `Demo Sale ${offeringId}-A`,
              clientId: 1,
              offeringId: offeringId,
              loansCount: 15,
              saleDate: new Date('2024-01-15'),
              totalAmount: 2500000
            },
            {
              saleId: offeringId * 10 + 2,
              saleDesc: `Demo Sale ${offeringId}-B`,
              clientId: 1,
              offeringId: offeringId,
              loansCount: 23,
              saleDate: new Date('2024-02-01'),
              totalAmount: 3200000
            },
            {
              saleId: offeringId * 10 + 3,
              saleDesc: `Demo Sale ${offeringId}-C`,
              clientId: 1,
              offeringId: offeringId,
              loansCount: 18,
              saleDate: new Date('2024-02-15'),
              totalAmount: 1800000
            }
          ]
        } else {
          this.setError(handleApiError(error))
          console.error('Failed to load sales:', error)
        }
      } finally {
        this.loading.sales = false
      }
    },

    async selectSale(sale: Sale) {
      this.selectedSale = sale
      this.selectedSaleDetails = null
      this.selectedLoan = null
      this.selectedLoanDetails = null
      this.selectedDocument = null
      this.selectedDocumentDetails = null
      this.availableLoans = []
      this.availableDocuments = []
      this.documentUrl = null
      this.loanSearchTerm = '' // Clear search when selecting new sale
      
      // Load sale details and loans
      await Promise.all([
        this.loadSaleDetails(sale.saleId),
        this.loadLoans(sale.saleId)
      ])
    },

    async loadSaleDetails(saleId: number) {
      this.clearError()
      
      try {
        this.selectedSaleDetails = await hydraApi.getSale(saleId)
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to load sale details:', error)
      }
    },

    async loadLoans(saleId: number) {
      this.loading.loans = true
      this.clearError()
      
      try {
        this.availableLoans = await hydraApi.getLoansBySale(saleId)
      } catch (error) {
        // Check if we're in mock auth mode and provide mock data
        const isMockAuth = import.meta.env.VITE_ENABLE_MOCK_AUTH === 'true'
        
        if (isMockAuth) {
          console.warn('🔄 Mock auth mode: Using mock loans data for sale', saleId)
          // Provide mock loans data
          this.availableLoans = Array.from({ length: 8 }, (_, index) => ({
            loanId: saleId * 100 + index + 1,
            assetName: `123 Main St ${String.fromCharCode(65 + index)}`,
            assetNo: `${saleId}${String(index + 1).padStart(3, '0')}`,
            assetName2: `Property ${index + 1}`,
            bookBalance: Math.floor(Math.random() * 500000) + 100000,
            documentCount: Math.floor(Math.random() * 20) + 5,
            loadedOn: new Date(Date.now() - Math.random() * 90 * 24 * 60 * 60 * 1000), // Random date within last 90 days
            saleId: saleId
          }))
        } else {
          this.setError(handleApiError(error))
          console.error('Failed to load loans:', error)
        }
      } finally {
        this.loading.loans = false
      }
    },

    // Search loans (matching legacy functionality)
    async searchLoans(searchTerm: string) {
      if (!this.selectedSale || !searchTerm.trim() || searchTerm.length < 2) {
        return
      }

      this.loading.loans = true
      this.clearError()
      
      try {
        const searchResults = await hydraApi.searchLoans(this.selectedSale.saleId, searchTerm.trim())
        // Update local loans list with search results
        this.availableLoans = searchResults
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to search loans:', error)
      } finally {
        this.loading.loans = false
      }
    },

    // Update loan search term and trigger local filtering
    updateLoanSearch(searchTerm: string) {
      this.loanSearchTerm = searchTerm
      
      // If search term is cleared, reload all loans for the current sale
      if (!searchTerm.trim() && this.selectedSale) {
        this.loadLoans(this.selectedSale.saleId)
      }
    },

    async selectLoan(loan: Loan) {
      this.selectedLoan = loan
      this.selectedLoanDetails = null
      this.selectedDocument = null
      this.selectedDocumentDetails = null
      this.availableDocuments = []
      this.documentUrl = null
      this.currentPage = 1
      this.totalPages = 0
      
      // Load loan details and documents
      await Promise.all([
        this.loadLoanDetails(loan.loanId),
        this.loadDocuments(loan.loanId)
      ])
    },

    async loadLoanDetails(loanId: number) {
      this.clearError()
      
      try {
        this.selectedLoanDetails = await hydraApi.getLoan(loanId)
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to load loan details:', error)
      }
    },

    async loadDocuments(loanId: number) {
      this.loading.documents = true
      this.clearError()
      
      try {
        this.availableDocuments = await hydraApi.getDocumentsByLoan(loanId)
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to load documents:', error)
      } finally {
        this.loading.documents = false
      }
    },

    async selectDocument(document: DocumentSummary) {
      this.selectedDocument = document
      this.selectedDocumentDetails = null
      this.documentUrl = null
      this.currentPage = 1
      this.totalPages = document.pageCount || 1
      
      // Load document details and URL
      await Promise.all([
        this.loadDocumentDetails(document.id),
        this.loadDocumentUrl(document.id)
      ])
    },

    async loadDocumentDetails(documentId: number) {
      this.clearError()
      
      try {
        this.selectedDocumentDetails = await hydraApi.getDocument(documentId)
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to load document details:', error)
      }
    },

    async loadDocumentUrl(documentId: number) {
      this.loading.documentContent = true
      this.clearError()
      
      try {
        const response = await hydraApi.getDocumentUrl(documentId)
        this.documentUrl = response.url
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to load document URL:', error)
      } finally {
        this.loading.documentContent = false
      }
    },

    // Document operations
    async updateDocumentType(documentId: number, documentType: string) {
      this.clearError()
      
      try {
        await hydraApi.updateDocumentType(documentId, documentType)
        
        // Update local document list
        const docIndex = this.availableDocuments.findIndex(doc => doc.id === documentId)
        if (docIndex !== -1) {
          this.availableDocuments[docIndex].documentType = documentType
        }
        
        // Update selected document if it's the one being updated
        if (this.selectedDocument?.id === documentId) {
          this.selectedDocument.documentType = documentType
        }
        
        if (this.selectedDocumentDetails?.id === documentId) {
          this.selectedDocumentDetails.documentType = documentType
        }
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to update document type:', error)
        throw error
      }
    },

    async rotateDocument(documentId: number, angle: number, pageNumber?: number) {
      this.clearError()
      
      try {
        await hydraApi.rotateDocument(documentId, { angle, pageNumber })
        
        // Reload document URL to reflect changes
        await this.loadDocumentUrl(documentId)
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to rotate document:', error)
        throw error
      }
    },

    async redactDocument(documentId: number, areas: any[], permanent: boolean = true) {
      this.clearError()
      
      try {
        await hydraApi.redactDocument(documentId, { areas, permanentRedaction: permanent })
        
        // Mark document as redacted in local state
        const docIndex = this.availableDocuments.findIndex(doc => doc.id === documentId)
        if (docIndex !== -1) {
          this.availableDocuments[docIndex].isRedacted = true
        }
        
        if (this.selectedDocument?.id === documentId) {
          this.selectedDocument.isRedacted = true
        }
        
        if (this.selectedDocumentDetails?.id === documentId) {
          this.selectedDocumentDetails.isRedacted = true
        }
        
        // Reload document URL to reflect changes
        await this.loadDocumentUrl(documentId)
      } catch (error) {
        this.setError(handleApiError(error))
        console.error('Failed to redact document:', error)
        throw error
      }
    },

    // UI state management
    setViewMode(mode: ViewMode) {
      this.documentViewMode = mode
    },

    setZoomLevel(level: number) {
      this.zoomLevel = Math.max(25, Math.min(500, level))
    },

    setPanelSizes(sizes: Partial<PanelSizes>) {
      this.panelSizes = { ...this.panelSizes, ...sizes }
    },

    // Page navigation
    goToPage(pageNumber: number) {
      if (pageNumber >= 1 && pageNumber <= this.totalPages) {
        this.currentPage = pageNumber
      }
    },

    nextPage() {
      if (this.currentPage < this.totalPages) {
        this.currentPage++
      }
    },

    previousPage() {
      if (this.currentPage > 1) {
        this.currentPage--
      }
    }
  }
})