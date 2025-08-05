// PDF.js service for document rendering and manipulation
// Following the "one-copy rule" to prevent "Cannot read from private field" errors
import * as pdfjsLib from 'pdfjs-dist'
import type { PDFDocumentProxy, PDFPageProxy } from 'pdfjs-dist'

// Use CDN worker that matches our exact version to prevent dual-copy issues
// Since we're using the same PDF.js import source, this should work without private field errors
pdfjsLib.GlobalWorkerOptions.workerSrc = `//unpkg.com/pdfjs-dist@3.11.174/build/pdf.worker.min.js`

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
    console.log('PDF.js worker configured with local worker:', pdfjsLib.GlobalWorkerOptions.workerSrc)
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
      console.log('PDF.js: Using cached loading task for', url)
      return await this.loadingTasks.get(url)!
    }

    console.log('PDF.js: Loading document from', url)

    // Create loading task with simplified options for reliability
    const loadingTask = pdfjsLib.getDocument({
      url,
      verbosity: import.meta.env.DEV ? 1 : 0,
    })

    // Store the promise to prevent duplicate loading
    this.loadingTasks.set(url, loadingTask.promise)

    try {
      const pdf = await loadingTask.promise
      console.log('PDF.js: Successfully loaded document with', pdf.numPages, 'pages')
      return pdf
    } catch (error) {
      // Remove failed loading task
      this.loadingTasks.delete(url)
      console.error('PDF.js: Failed to load document:', error)
      
      // Provide more helpful error messages
      if (error instanceof Error) {
        if (error.message.includes('Invalid PDF structure')) {
          throw new Error('The document appears to be corrupted or is not a valid PDF')
        } else if (error.message.includes('Network error') || error.message.includes('fetch')) {
          throw new Error('Failed to download the document. Please check your internet connection.')
        } else if (error.message.includes('AbortError')) {
          throw new Error('Document loading was cancelled')
        }
      }
      
      throw error
    }
  }

  /**
   * Render a PDF page using the centralized PDF.js import
   */
  async renderPage(
    pdf: PDFDocumentProxy,
    pageNumber: number,
    canvas: HTMLCanvasElement,
    options: PDFRenderOptions = {}
  ): Promise<void> {
    console.log(`PDF.js: Rendering page ${pageNumber}`)
    
    // Ensure canvas is visible before rendering
    if (!canvas.offsetParent) {
      console.warn(`PDF.js: Canvas for page ${pageNumber} is not visible, delaying render`)
      await new Promise(resolve => setTimeout(resolve, 100))
      if (!canvas.offsetParent) {
        throw new Error(`Canvas for page ${pageNumber} is not visible in DOM`)
      }
    }
    
    try {
      console.log(`PDF.js: Getting page ${pageNumber} from document with ${pdf.numPages} pages`)
      const page = await pdf.getPage(pageNumber)
      console.log(`PDF.js: Successfully got page ${pageNumber}`)
      
      const scale = options.scale || 1
      const rotation = options.rotation || 0

      // Get viewport with scale and rotation
      const viewport = page.getViewport({ scale, rotation })
      console.log(`PDF.js: Got viewport for page ${pageNumber}:`, {
        width: viewport.width,
        height: viewport.height,
        scale: viewport.scale,
        rotation: viewport.rotation
      })
      
      const context = canvas.getContext('2d')
      if (!context) {
        throw new Error('Failed to get 2D rendering context from canvas')
      }

      // Set canvas dimensions to match viewport
      canvas.width = viewport.width
      canvas.height = viewport.height
      canvas.style.width = viewport.width + 'px'
      canvas.style.height = viewport.height + 'px'

      console.log(`PDF.js: Canvas dimensions set:`, {
        canvasWidth: canvas.width,
        canvasHeight: canvas.height,
        styleWidth: canvas.style.width,
        styleHeight: canvas.style.height
      })

      // Clear the canvas before rendering
      context.clearRect(0, 0, canvas.width, canvas.height)

      // Create render context
      const renderContext = {
        canvasContext: context,
        viewport: viewport
      }

      console.log(`PDF.js: Starting render`)

      // Render the page
      const renderTask = page.render(renderContext)
      await renderTask.promise
      console.log(`PDF.js: Successfully rendered page ${pageNumber}`)
      
    } catch (error) {
      console.error(`PDF.js: Failed to render page ${pageNumber}:`, error)
      throw new Error(`Failed to render page ${pageNumber}: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }

  /**
   * Generate thumbnail for a PDF page with proper scaling
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
    
    let finalWidth = viewport.width
    let finalHeight = viewport.height

    // Apply max width/height constraints if specified
    if (options.maxWidth && finalWidth > options.maxWidth) {
      const ratio = options.maxWidth / finalWidth
      finalWidth = options.maxWidth
      finalHeight = finalHeight * ratio
    }
    
    if (options.maxHeight && finalHeight > options.maxHeight) {
      const ratio = options.maxHeight / finalHeight
      finalHeight = options.maxHeight
      finalWidth = finalWidth * ratio
    }

    // Set canvas dimensions
    canvas.width = Math.floor(finalWidth)
    canvas.height = Math.floor(finalHeight)

    // Calculate the scale for the final thumbnail size
    const thumbScale = Math.min(finalWidth / viewport.width, finalHeight / viewport.height) * scale
    const thumbViewport = page.getViewport({ scale: thumbScale })

    // Render the page
    const renderContext = {
      canvasContext: context,
      viewport: thumbViewport
    }

    try {
      await page.render(renderContext).promise
      // Convert to data URL
      return canvas.toDataURL('image/jpeg', quality)
    } catch (error) {
      console.error(`Failed to generate thumbnail for page ${pageNumber}:`, error)
      // Return a placeholder data URL for failed thumbnails
      context.fillStyle = '#f3f4f6'
      context.fillRect(0, 0, canvas.width, canvas.height)
      context.fillStyle = '#6b7280'
      context.font = '12px Arial'
      context.textAlign = 'center'
      context.fillText(`Page ${pageNumber}`, canvas.width / 2, canvas.height / 2)
      return canvas.toDataURL('image/jpeg', quality)
    }
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
   * Check if canvas is ready for rendering
   */
  isCanvasReady(canvas: HTMLCanvasElement): boolean {
    // Check if canvas is attached to DOM and visible
    return canvas.offsetParent !== null && canvas.offsetWidth > 0 && canvas.offsetHeight > 0
  }

  /**
   * Wait for canvas to become ready
   */
  async waitForCanvasReady(canvas: HTMLCanvasElement, timeout: number = 5000): Promise<boolean> {
    const startTime = Date.now()
    
    while (!this.isCanvasReady(canvas)) {
      if (Date.now() - startTime > timeout) {
        return false
      }
      await new Promise(resolve => setTimeout(resolve, 50))
    }
    
    return true
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

// Export singleton instance and centralized imports
export const pdfService = PDFService.getInstance()
export const { getDocument, version } = pdfjsLib