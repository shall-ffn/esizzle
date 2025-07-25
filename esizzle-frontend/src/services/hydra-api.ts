// Hydra Due Diligence API service methods
import { apiClient } from './api'
import { API_ENDPOINTS } from '@/types/api'
import type {
  Offering,
  OfferingDetails,
  Sale,
  SaleDetails,
  Loan,
  LoanDetails,
  DocumentSummary,
  DocumentDetails,
  DocumentUrlResponse,
  RotateDocumentRequest,
  RedactDocumentRequest
} from '@/types/domain'

export class HydraApiService {
  // Offering methods
  async getUserOfferings(): Promise<Offering[]> {
    return await apiClient.get<Offering[]>(API_ENDPOINTS.OFFERINGS.USER_OFFERINGS)
  }

  async getOffering(offeringId: number): Promise<OfferingDetails> {
    return await apiClient.get<OfferingDetails>(API_ENDPOINTS.OFFERINGS.BY_ID(offeringId))
  }

  async getAllOfferings(): Promise<Offering[]> {
    return await apiClient.get<Offering[]>(API_ENDPOINTS.OFFERINGS.ALL)
  }

  // Sale methods
  async getSalesByOffering(offeringId: number): Promise<Sale[]> {
    return await apiClient.get<Sale[]>(API_ENDPOINTS.SALES.BY_OFFERING(offeringId))
  }

  async getSale(saleId: number): Promise<SaleDetails> {
    return await apiClient.get<SaleDetails>(API_ENDPOINTS.SALES.BY_ID(saleId))
  }

  // Loan methods
  async getLoansBySale(saleId: number): Promise<Loan[]> {
    return await apiClient.get<Loan[]>(API_ENDPOINTS.LOANS.BY_SALE(saleId))
  }

  async searchLoans(saleId: number, searchTerm: string): Promise<Loan[]> {
    return await apiClient.get<Loan[]>(API_ENDPOINTS.LOANS.SEARCH(saleId), { searchTerm })
  }

  async getLoan(loanId: number): Promise<LoanDetails> {
    return await apiClient.get<LoanDetails>(API_ENDPOINTS.LOANS.BY_ID(loanId))
  }

  // Document methods
  async getDocumentsByLoan(loanId: number): Promise<DocumentSummary[]> {
    return await apiClient.get<DocumentSummary[]>(API_ENDPOINTS.DOCUMENTS.BY_LOAN(loanId))
  }

  async getDocument(documentId: number): Promise<DocumentDetails> {
    return await apiClient.get<DocumentDetails>(API_ENDPOINTS.DOCUMENTS.BY_ID(documentId))
  }

  async getDocumentUrl(documentId: number): Promise<DocumentUrlResponse> {
    return await apiClient.get<DocumentUrlResponse>(API_ENDPOINTS.DOCUMENTS.URL(documentId))
  }

  async updateDocumentType(documentId: number, documentType: string): Promise<{ success: boolean; message: string }> {
    return await apiClient.put<{ success: boolean; message: string }>(
      API_ENDPOINTS.DOCUMENTS.CLASSIFICATION(documentId),
      documentType
    )
  }

  async rotateDocument(documentId: number, request: RotateDocumentRequest): Promise<{ success: boolean; message: string; rotationAngle: number }> {
    return await apiClient.post<{ success: boolean; message: string; rotationAngle: number }>(
      API_ENDPOINTS.DOCUMENTS.ROTATE(documentId),
      request
    )
  }

  async redactDocument(documentId: number, request: RedactDocumentRequest): Promise<{ success: boolean; message: string; redactionCount: number; permanentRedaction: boolean }> {
    return await apiClient.post<{ success: boolean; message: string; redactionCount: number; permanentRedaction: boolean }>(
      API_ENDPOINTS.DOCUMENTS.REDACT(documentId),
      request
    )
  }

  async getDocumentTypes(): Promise<string[]> {
    return await apiClient.get<string[]>(API_ENDPOINTS.DOCUMENTS.TYPES)
  }
}

// Create singleton instance
export const hydraApi = new HydraApiService()