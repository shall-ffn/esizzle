// Health check service for development and monitoring
import axios, { type AxiosResponse } from 'axios'

export interface HealthStatus {
  status: 'healthy' | 'unhealthy'
  timestamp: string
  version: string
  environment: string
}

export interface DetailedHealthStatus extends HealthStatus {
  checks: {
    database: {
      status: 'healthy' | 'unhealthy'
      responseTime?: string
      connectionState?: string
      tablesAccessible?: boolean
      message: string
      error?: string
    }
    memory: {
      totalMemoryMB: number
      workingSetMB: number
      gcCollections: {
        gen0: number
        gen1: number
        gen2: number
      }
    }
    uptime: {
      totalSeconds: number
      formatted: string
    }
  }
}

export class HealthService {
  private readonly baseUrl: string

  constructor(baseUrl: string = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api') {
    this.baseUrl = baseUrl
  }

  /**
   * Check basic API health
   */
  async checkHealth(): Promise<HealthStatus> {
    try {
      const response: AxiosResponse<HealthStatus> = await axios.get(
        `${this.baseUrl}/health`,
        { timeout: 5000 }
      )
      return response.data
    } catch (error) {
      console.error('Health check failed:', error)
      throw new Error('API health check failed')
    }
  }

  /**
   * Check detailed health including database
   */
  async checkDetailedHealth(): Promise<DetailedHealthStatus> {
    try {
      const response: AxiosResponse<DetailedHealthStatus> = await axios.get(
        `${this.baseUrl}/health/detailed`,
        { timeout: 10000 }
      )
      return response.data
    } catch (error) {
      console.error('Detailed health check failed:', error)
      throw new Error('Detailed API health check failed')
    }
  }

  /**
   * Check database connectivity only
   */
  async checkDatabase(): Promise<DetailedHealthStatus['checks']['database']> {
    try {
      const response: AxiosResponse<DetailedHealthStatus['checks']['database']> = await axios.get(
        `${this.baseUrl}/health/database`,
        { timeout: 10000 }
      )
      return response.data
    } catch (error) {
      console.error('Database health check failed:', error)
      throw new Error('Database health check failed')
    }
  }

  /**
   * Validate frontend environment configuration
   */
  validateFrontendConfig(): {
    status: 'healthy' | 'warning' | 'error'
    checks: Array<{
      name: string
      status: 'pass' | 'warn' | 'fail'
      message: string
      value?: string
    }>
  } {
    const checks = []

    // Check API base URL
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL
    if (apiBaseUrl) {
      checks.push({
        name: 'API Base URL',
        status: 'pass' as const,
        message: 'API base URL is configured',
        value: apiBaseUrl
      })
    } else {
      checks.push({
        name: 'API Base URL',
        status: 'warn' as const,
        message: 'API base URL not configured, using default'
      })
    }

    // Check debug logging
    const debugLogging = import.meta.env.VITE_ENABLE_DEBUG_LOGGING
    checks.push({
      name: 'Debug Logging',
      status: debugLogging === 'true' ? 'pass' : 'warn',
      message: debugLogging === 'true' ? 'Debug logging enabled' : 'Debug logging disabled',
      value: debugLogging
    })

    // Check PDF.js configuration
    const pdfWorkerUrl = import.meta.env.VITE_PDF_WORKER_URL
    if (pdfWorkerUrl) {
      checks.push({
        name: 'PDF.js Worker',
        status: 'pass' as const,
        message: 'PDF.js worker URL configured',
        value: pdfWorkerUrl
      })
    } else {
      checks.push({
        name: 'PDF.js Worker',
        status: 'fail' as const,
        message: 'PDF.js worker URL not configured'
      })
    }

    // Check browser compatibility
    const userAgent = navigator.userAgent
    const isModernBrowser = 'fetch' in window && 'Promise' in window && 'URLSearchParams' in window
    
    checks.push({
      name: 'Browser Compatibility',
      status: isModernBrowser ? 'pass' : 'fail',
      message: isModernBrowser ? 'Browser supports modern features' : 'Browser may not be fully compatible',
      value: userAgent
    })

    // Check local storage
    try {
      localStorage.setItem('test', 'test')
      localStorage.removeItem('test')
      checks.push({
        name: 'Local Storage',
        status: 'pass' as const,
        message: 'Local storage is available'
      })
    } catch {
      checks.push({
        name: 'Local Storage',
        status: 'warn' as const,
        message: 'Local storage is not available'
      })
    }

    // Determine overall status
    const hasFailures = checks.some(check => check.status === 'fail')
    const hasWarnings = checks.some(check => check.status === 'warn')
    
    let status: 'healthy' | 'warning' | 'error'
    if (hasFailures) {
      status = 'error'
    } else if (hasWarnings) {
      status = 'warning'
    } else {
      status = 'healthy'
    }

    return { status, checks }
  }

  /**
   * Test full system connectivity
   */
  async runSystemCheck(): Promise<{
    frontend: ReturnType<HealthService['validateFrontendConfig']>
    backend: HealthStatus | null
    database: DetailedHealthStatus['checks']['database'] | null
    overall: 'healthy' | 'warning' | 'error'
  }> {
    const frontend = this.validateFrontendConfig()
    let backend: HealthStatus | null = null
    let database: DetailedHealthStatus['checks']['database'] | null = null

    // Test backend connectivity
    try {
      backend = await this.checkHealth()
    } catch (error) {
      console.warn('Backend health check failed:', error)
    }

    // Test database connectivity
    try {
      database = await this.checkDatabase()
    } catch (error) {
      console.warn('Database health check failed:', error)
    }

    // Determine overall status
    let overall: 'healthy' | 'warning' | 'error'
    if (!backend || !database || frontend.status === 'error' || database.status === 'unhealthy') {
      overall = 'error'
    } else if (frontend.status === 'warning') {
      overall = 'warning'
    } else {
      overall = 'healthy'
    }

    return {
      frontend,
      backend,
      database,
      overall
    }
  }
}

// Export singleton instance
export const healthService = new HealthService()