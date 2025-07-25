// API response and request types

export interface ApiResponse<T> {
  data: T
  success: boolean
  message?: string
  errors?: string[]
}

export interface PaginatedResponse<T> {
  data: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export interface AuthRequest {
  username: string
  password: string
}

export interface AuthResponse {
  token: string
  user: {
    id: number
    name: string
    email: string
    userName: string
    accessLevel: number
    clientId?: number
  }
  expiresAt: Date
}

export interface ErrorResponse {
  error: string
  message: string
  statusCode: number
  timestamp: Date
}

// HTTP status codes
export enum HttpStatusCode {
  OK = 200,
  Created = 201,
  BadRequest = 400,
  Unauthorized = 401,
  Forbidden = 403,
  NotFound = 404,
  InternalServerError = 500
}

// API endpoints - Updated to match the .NET API structure
export const API_ENDPOINTS = {
  // Authentication
  AUTH: {
    LOGIN: '/api/auth/login',
    LOGOUT: '/api/auth/logout',
    ME: '/api/auth/me',
    REFRESH: '/api/auth/refresh'
  },
  
  // Health Check
  HEALTH: {
    CHECK: '/api/health',
    DETAILED: '/api/health/detailed',
    DATABASE: '/api/health/database'
  },
  
  // Offerings
  OFFERINGS: {
    BASE: '/api/v1/hydra/offering',
    USER_OFFERINGS: '/api/v1/hydra/offering/user-offerings',
    ALL: '/api/v1/hydra/offering/all',
    BY_ID: (id: number) => `/api/v1/hydra/offering/${id}`
  },
  
  // Sales
  SALES: {
    BASE: '/api/v1/hydra/sale',
    BY_OFFERING: (offeringId: number) => `/api/v1/hydra/sale/by-offering/${offeringId}`,
    BY_ID: (id: number) => `/api/v1/hydra/sale/${id}`
  },
  
  // Loans
  LOANS: {
    BASE: '/api/v1/hydra/loan',
    BY_SALE: (saleId: number) => `/api/v1/hydra/loan/by-sale/${saleId}`,
    SEARCH: (saleId: number) => `/api/v1/hydra/loan/by-sale/${saleId}/search`,
    BY_ID: (id: number) => `/api/v1/hydra/loan/${id}`
  },
  
  // Documents
  DOCUMENTS: {
    BASE: '/api/v1/hydra/document',
    BY_LOAN: (loanId: number) => `/api/v1/hydra/document/by-loan/${loanId}`,
    BY_ID: (id: number) => `/api/v1/hydra/document/${id}`,
    URL: (id: number) => `/api/v1/hydra/document/${id}/url`,
    CLASSIFICATION: (id: number) => `/api/v1/hydra/document/${id}/classification`,
    ROTATE: (id: number) => `/api/v1/hydra/document/${id}/rotate`,
    REDACT: (id: number) => `/api/v1/hydra/document/${id}/redact`,
    TYPES: '/api/v1/hydra/document/types'
  }
} as const