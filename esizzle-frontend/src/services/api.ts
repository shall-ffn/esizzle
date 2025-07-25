import axios from 'axios'
import type { AxiosInstance, AxiosResponse, AxiosError } from 'axios'
import type { ApiResponse, ErrorResponse } from '@/types/api'

class ApiClient {
  private client: AxiosInstance

  constructor(baseURL: string = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api').replace('/api', '')) {
    this.client = axios.create({
      baseURL,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json'
      }
    })

    this.setupInterceptors()
  }

  private setupInterceptors(): void {
    // Request interceptor to add auth token
    this.client.interceptors.request.use(
      (config) => {
        const token = localStorage.getItem('auth_token')
        if (token) {
          config.headers.Authorization = `Bearer ${token}`
        }

        // In mock auth mode, add user info headers
        const isMockAuth = import.meta.env.VITE_ENABLE_MOCK_AUTH === 'true'
        if (isMockAuth && token?.startsWith('mock-jwt-token')) {
          // Get user info from localStorage (set during mock login)
          const userInfo = localStorage.getItem('mock_user_info')
          if (userInfo) {
            try {
              const user = JSON.parse(userInfo)
              config.headers['X-Mock-User-Id'] = user.id?.toString() || '21496'
              config.headers['X-Mock-User-Email'] = user.email || 'shall@ffncorp.com'
              config.headers['X-Mock-User-Name'] = user.name || 'Stephen Hall'
            } catch (e) {
              // Fallback to hardcoded values if parsing fails
              config.headers['X-Mock-User-Id'] = '21496'
              config.headers['X-Mock-User-Email'] = 'shall@ffncorp.com'
              config.headers['X-Mock-User-Name'] = 'Stephen Hall'
            }
          } else {
            // Fallback to hardcoded values
            config.headers['X-Mock-User-Id'] = '21496'
            config.headers['X-Mock-User-Email'] = 'shall@ffncorp.com'
            config.headers['X-Mock-User-Name'] = 'Stephen Hall'
          }
        }
        
        return config
      },
      (error) => {
        return Promise.reject(error)
      }
    )

    // Response interceptor to handle errors
    this.client.interceptors.response.use(
      (response: AxiosResponse) => {
        return response
      },
      (error: AxiosError<ErrorResponse>) => {
        // Check if we're in mock auth mode
        const isMockAuth = import.meta.env.VITE_ENABLE_MOCK_AUTH === 'true'
        
        if (error.response?.status === 401 && !isMockAuth) {
          // Only redirect to login if NOT in mock auth mode
          localStorage.removeItem('auth_token')
          window.location.href = '/login'
        } else if (error.response?.status === 401 && isMockAuth) {
          // In mock auth mode, just log the error but don't redirect
          console.warn('🔄 Mock auth mode: API returned 401, but not redirecting to login')
        }
        return Promise.reject(error)
      }
    )
  }

  // Generic HTTP methods
  async get<T>(url: string, params?: Record<string, any>): Promise<T> {
    const response = await this.client.get<T>(url, { params })
    return response.data
  }

  async post<T, D = any>(url: string, data?: D): Promise<T> {
    const response = await this.client.post<T>(url, data)
    return response.data
  }

  async put<T, D = any>(url: string, data?: D): Promise<T> {
    const response = await this.client.put<T>(url, data)
    return response.data
  }

  async delete<T>(url: string): Promise<T> {
    const response = await this.client.delete<T>(url)
    return response.data
  }

  // Set auth token
  setAuthToken(token: string): void {
    localStorage.setItem('auth_token', token)
    this.client.defaults.headers.common['Authorization'] = `Bearer ${token}`
  }

  // Clear auth token
  clearAuthToken(): void {
    localStorage.removeItem('auth_token')
    delete this.client.defaults.headers.common['Authorization']
  }

  // Get current token
  getAuthToken(): string | null {
    return localStorage.getItem('auth_token')
  }
}

// Create singleton instance
export const apiClient = new ApiClient()

// Error handling utility
export const handleApiError = (error: unknown): string => {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<ErrorResponse>
    
    if (axiosError.response?.data?.message) {
      return axiosError.response.data.message
    }
    
    if (axiosError.response?.data?.error) {
      return axiosError.response.data.error
    }
    
    switch (axiosError.response?.status) {
      case 400:
        return 'Bad request. Please check your input.'
      case 401:
        return 'Authentication required. Please log in.'
      case 403:
        return 'Access denied. You do not have permission to perform this action.'
      case 404:
        return 'The requested resource was not found.'
      case 500:
        return 'Internal server error. Please try again later.'
      default:
        return axiosError.message || 'An unexpected error occurred.'
    }
  }
  
  if (error instanceof Error) {
    return error.message
  }
  
  return 'An unknown error occurred.'
}