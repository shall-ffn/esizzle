// PDF.js service for document rendering and manipulation
import * as pdfjsLib from 'pdfjs-dist'
import type { PDFDocumentProxy, PDFPageProxy } from 'pdfjs-dist'

export interface PDFRenderOptions {
  scale?: number
  rotation?: number
  canvasContext?: CanvasRenderingContext2D
  viewport?: any
}

export interface ThumbnailOptions {
  scale?: number
  maxWidth?: number
  maxHeight?: number
  quality?: number
}

export class PDFService {
  private static instance: PDFService
  private loadingTasks: Map<string, Promise<PDFDocumentProxy>> = new Map()

  constructor() {
    // Configure PDF.js worker - use different paths for dev vs production
    if (import.meta.env.DEV) {
      pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
        'pdfjs-dist/build/pdf.worker.min.js',
        import.meta.url
      ).toString()
    } else {
      pdfjsLib.GlobalWorkerOptions.workerSrc = '/pdf.worker.min.js'
    }
  }

  static getInstance(): PDFService {
    if (!PDFService.instance) {
      PDFService.instance = new PDFService()
    }
    return PDFService.instance
  }

  /**
   * Load a PDF document from URL
   */
  async loadDocument(url: string): Promise<PDFDocumentProxy> {
    // Check if document is already being loaded
    if (this.loadingTasks.has(url)) {
      return await this.loadingTasks.get(url)!
    }

    // Create loading task
    const loadingTask = pdfjsLib.getDocument({
      url,
      verbosity: 0, // Reduce console logging
      cMapUrl: '/cmaps/',
      cMapPacked: true
    })

    // Store the promise to prevent duplicate loading
    this.loadingTasks.set(url, loadingTask.promise)

    try {
      const pdf = await loadingTask.promise
      return pdf
    } catch (error) {
      // Remove failed loading task
      this.loadingTasks.delete(url)
      throw error
    }
  }

  /**
   * Render a PDF page to a canvas
   */
  async renderPage(
    pdf: PDFDocumentProxy,
    pageNumber: number,
    canvas: HTMLCanvasElement,
    options: PDFRenderOptions = {}
  ): Promise<void> {
    const page = await pdf.getPage(pageNumber)
    const scale = options.scale || 1
    const rotation = options.rotation || 0

    const viewport = page.getViewport({ scale, rotation })
    const context = canvas.getContext('2d')!

    // Set canvas dimensions
    canvas.width = viewport.width
    canvas.height = viewport.height
    canvas.style.width = viewport.width + 'px'
    canvas.style.height = viewport.height + 'px'

    // Render the page
    const renderContext = {
      canvasContext: context,
      viewport: viewport
    }

    await page.render(renderContext).promise
  }

  /**
   * Generate thumbnail for a PDF page
   */
  async generateThumbnail(
    pdf: PDFDocumentProxy,
    pageNumber: number,
    options: ThumbnailOptions = {}
  ): Promise<string> {
    const page = await pdf.getPage(pageNumber)
    const scale = options.scale || 0.2
    const quality = options.quality || 0.8

    const viewport = page.getViewport({ scale })
    
    // Create canvas for thumbnail
    const canvas = document.createElement('canvas')
    const context = canvas.getContext('2d')!
    
    canvas.width = viewport.width
    canvas.height = viewport.height

    // Apply max width/height constraints if specified
    if (options.maxWidth && canvas.width > options.maxWidth) {
      const ratio = options.maxWidth / canvas.width
      canvas.width = options.maxWidth
      canvas.height = canvas.height * ratio
    }
    
    if (options.maxHeight && canvas.height > options.maxHeight) {
      const ratio = options.maxHeight / canvas.height
      canvas.height = options.maxHeight
      canvas.width = canvas.width * ratio
    }

    // Render the page
    const renderContext = {
      canvasContext: context,
      viewport: viewport
    }

    await page.render(renderContext).promise

    // Convert to data URL
    return canvas.toDataURL('image/jpeg', quality)
  }

  /**
   * Generate thumbnails for all pages in a PDF
   */
  async generateAllThumbnails(
    pdf: PDFDocumentProxy,
    options: ThumbnailOptions = {}
  ): Promise<string[]> {
    const thumbnails: string[] = []
    const numPages = pdf.numPages

    // Generate thumbnails in batches to avoid overwhelming the browser
    const batchSize = 5
    for (let i = 1; i <= numPages; i += batchSize) {
      const batch = []
      for (let j = i; j < Math.min(i + batchSize, numPages + 1); j++) {
        batch.push(this.generateThumbnail(pdf, j, options))
      }
      
      const batchResults = await Promise.all(batch)
      thumbnails.push(...batchResults)
    }

    return thumbnails
  }

  /**
   * Get page text content for search functionality
   */
  async getPageTextContent(pdf: PDFDocumentProxy, pageNumber: number): Promise<string> {
    const page = await pdf.getPage(pageNumber)
    const textContent = await page.getTextContent()
    
    return textContent.items
      .map((item: any) => item.str)
      .join(' ')
  }

  /**
   * Search for text across all pages
   */
  async searchDocument(pdf: PDFDocumentProxy, searchTerm: string): Promise<Array<{page: number, matches: number}>> {
    const results: Array<{page: number, matches: number}> = []
    const numPages = pdf.numPages
    const searchRegex = new RegExp(searchTerm, 'gi')

    for (let pageNum = 1; pageNum <= numPages; pageNum++) {
      const textContent = await this.getPageTextContent(pdf, pageNum)
      const matches = (textContent.match(searchRegex) || []).length
      
      if (matches > 0) {
        results.push({ page: pageNum, matches })
      }
    }

    return results
  }

  /**
   * Get document metadata
   */
  async getDocumentInfo(pdf: PDFDocumentProxy): Promise<any> {
    return await pdf.getMetadata()
  }

  /**
   * Calculate optimal scale to fit page in container
   */
  calculateFitScale(
    page: PDFPageProxy,
    containerWidth: number,
    containerHeight: number,
    padding: number = 20
  ): number {
    const viewport = page.getViewport({ scale: 1 })
    const availableWidth = containerWidth - (padding * 2)
    const availableHeight = containerHeight - (padding * 2)
    
    const scaleX = availableWidth / viewport.width
    const scaleY = availableHeight / viewport.height
    
    return Math.min(scaleX, scaleY, 2) // Cap at 200% zoom
  }

  /**
   * Cleanup resources
   */
  cleanup(url?: string): void {
    if (url) {
      this.loadingTasks.delete(url)
    } else {
      this.loadingTasks.clear()
    }
  }
}

// Export singleton instance
export const pdfService = PDFService.getInstance()
