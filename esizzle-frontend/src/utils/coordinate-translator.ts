/**
 * Coordinate Translation System
 * 
 * Handles conversion between PDF page coordinates and canvas screen coordinates
 * with support for zoom levels, rotations, and viewport transformations.
 * Maintains pixel-perfect accuracy for manipulation overlays.
 */

import type { Point, Rectangle, ViewportInfo, CoordinateTranslationOptions } from '@/types/manipulation'

export class CoordinateTranslator {
  private viewport: ViewportInfo
  private canvasDimensions: { width: number; height: number }
  private zoomLevel: number
  private pageRotation: number

  constructor(options: CoordinateTranslationOptions) {
    this.viewport = options.viewport
    this.canvasDimensions = options.canvasDimensions
    this.zoomLevel = options.zoomLevel
    this.pageRotation = options.pageRotation || 0
  }

  /**
   * Convert PDF page coordinates to canvas screen coordinates
   * Takes into account zoom level, rotation, and viewport scaling
   */
  pageToCanvas(pageCoord: Point): Point {
    // First apply page rotation if needed
    let coord = pageCoord
    if (this.pageRotation !== 0) {
      coord = this.applyPageRotation(pageCoord, this.pageRotation)
    }

    // Calculate scale factors
    const scaleX = this.canvasDimensions.width / this.viewport.width
    const scaleY = this.canvasDimensions.height / this.viewport.height

    // Apply scaling and zoom
    const zoomFactor = this.zoomLevel / 100
    
    return {
      x: coord.x * scaleX * zoomFactor,
      y: coord.y * scaleY * zoomFactor
    }
  }

  /**
   * Convert canvas screen coordinates to PDF page coordinates
   * Reverse transformation of pageToCanvas
   */
  canvasToPage(canvasCoord: Point): Point {
    // Reverse scaling and zoom
    const scaleX = this.viewport.width / this.canvasDimensions.width
    const scaleY = this.viewport.height / this.canvasDimensions.height
    const zoomFactor = this.zoomLevel / 100

    const coord: Point = {
      x: canvasCoord.x * scaleX / zoomFactor,
      y: canvasCoord.y * scaleY / zoomFactor
    }

    // Reverse page rotation if needed
    if (this.pageRotation !== 0) {
      return this.applyPageRotation(coord, -this.pageRotation)
    }

    return coord
  }

  /**
   * Convert a rectangle from page coordinates to canvas coordinates
   */
  rectanglePageToCanvas(pageRect: Rectangle): Rectangle {
    const topLeft = this.pageToCanvas({ x: pageRect.x, y: pageRect.y })
    const bottomRight = this.pageToCanvas({ 
      x: pageRect.x + pageRect.width, 
      y: pageRect.y + pageRect.height 
    })

    return {
      x: Math.min(topLeft.x, bottomRight.x),
      y: Math.min(topLeft.y, bottomRight.y),
      width: Math.abs(bottomRight.x - topLeft.x),
      height: Math.abs(bottomRight.y - topLeft.y)
    }
  }

  /**
   * Convert a rectangle from canvas coordinates to page coordinates
   */
  rectangleCanvasToPage(canvasRect: Rectangle): Rectangle {
    const topLeft = this.canvasToPage({ x: canvasRect.x, y: canvasRect.y })
    const bottomRight = this.canvasToPage({ 
      x: canvasRect.x + canvasRect.width, 
      y: canvasRect.y + canvasRect.height 
    })

    return {
      x: Math.min(topLeft.x, bottomRight.x),
      y: Math.min(topLeft.y, bottomRight.y),
      width: Math.abs(bottomRight.x - topLeft.x),
      height: Math.abs(bottomRight.y - topLeft.y)
    }
  }

  /**
   * Apply rotation transformation to a point around the page center
   */
  private applyPageRotation(point: Point, rotation: number): Point {
    if (rotation === 0) return point

    const centerX = this.viewport.width / 2
    const centerY = this.viewport.height / 2
    
    // Convert to radians
    const rad = (rotation * Math.PI) / 180
    const cos = Math.cos(rad)
    const sin = Math.sin(rad)
    
    // Translate to origin
    const translatedX = point.x - centerX
    const translatedY = point.y - centerY
    
    // Apply rotation matrix
    const rotatedX = translatedX * cos - translatedY * sin
    const rotatedY = translatedX * sin + translatedY * cos
    
    // Translate back
    return {
      x: rotatedX + centerX,
      y: rotatedY + centerY
    }
  }

  /**
   * Update translator settings (useful when zoom/rotation changes)
   */
  updateSettings(options: Partial<CoordinateTranslationOptions>): void {
    if (options.viewport) {
      this.viewport = options.viewport
    }
    if (options.canvasDimensions) {
      this.canvasDimensions = options.canvasDimensions
    }
    if (options.zoomLevel !== undefined) {
      this.zoomLevel = options.zoomLevel
    }
    if (options.pageRotation !== undefined) {
      this.pageRotation = options.pageRotation
    }
  }

  /**
   * Get current scale factor for visual adjustments (borders, text size, etc.)
   */
  getScaleFactor(): number {
    const scaleX = this.canvasDimensions.width / this.viewport.width
    const scaleY = this.canvasDimensions.height / this.viewport.height
    const zoomFactor = this.zoomLevel / 100
    
    // Return the average scale for consistent visual scaling
    return ((scaleX + scaleY) / 2) * zoomFactor
  }

  /**
   * Check if coordinates are within the page bounds
   */
  isWithinPageBounds(pageCoord: Point): boolean {
    return pageCoord.x >= 0 && 
           pageCoord.x <= this.viewport.width &&
           pageCoord.y >= 0 && 
           pageCoord.y <= this.viewport.height
  }

  /**
   * Clamp coordinates to page bounds
   */
  clampToPageBounds(pageCoord: Point): Point {
    return {
      x: Math.max(0, Math.min(this.viewport.width, pageCoord.x)),
      y: Math.max(0, Math.min(this.viewport.height, pageCoord.y))
    }
  }

  /**
   * Calculate minimum size for manipulation areas in canvas coordinates
   * Ensures manipulations are at least 5x5 pixels regardless of zoom
   */
  getMinimumSize(): { width: number; height: number } {
    const minPixels = 5
    const scaleFactor = this.getScaleFactor()
    
    return {
      width: minPixels / scaleFactor,
      height: minPixels / scaleFactor
    }
  }

  /**
   * Snap coordinates to pixel boundaries for crisp rendering
   */
  snapToPixel(canvasCoord: Point): Point {
    return {
      x: Math.round(canvasCoord.x),
      y: Math.round(canvasCoord.y)
    }
  }

  /**
   * Get viewport information
   */
  getViewportInfo(): ViewportInfo {
    return { ...this.viewport }
  }

  /**
   * Get canvas dimensions
   */
  getCanvasDimensions(): { width: number; height: number } {
    return { ...this.canvasDimensions }
  }
}

/**
 * Factory function to create coordinate translator from PDF.js viewport
 */
export function createCoordinateTranslator(
  pdfViewport: any, // PDF.js PageViewport
  canvasElement: HTMLCanvasElement,
  zoomLevel: number,
  pageRotation: number = 0
): CoordinateTranslator {
  const canvasRect = canvasElement.getBoundingClientRect()
  
  return new CoordinateTranslator({
    viewport: {
      width: pdfViewport.width,
      height: pdfViewport.height,
      scale: pdfViewport.scale,
      rotation: pdfViewport.rotation
    },
    canvasDimensions: {
      width: canvasRect.width,
      height: canvasRect.height
    },
    zoomLevel,
    pageRotation
  })
}

/**
 * Utility functions for coordinate validation and manipulation
 */
export const CoordinateUtils = {
  /**
   * Check if a rectangle has minimum size requirements
   */
  isValidRectangle(rect: Rectangle, minSize: number = 5): boolean {
    return rect.width >= minSize && rect.height >= minSize
  },

  /**
   * Normalize rectangle to ensure positive width/height
   */
  normalizeRectangle(rect: Rectangle): Rectangle {
    return {
      x: rect.width < 0 ? rect.x + rect.width : rect.x,
      y: rect.height < 0 ? rect.y + rect.height : rect.y,
      width: Math.abs(rect.width),
      height: Math.abs(rect.height)
    }
  },

  /**
   * Calculate rectangle from two points (useful for drag operations)
   */
  rectangleFromPoints(start: Point, end: Point): Rectangle {
    return this.normalizeRectangle({
      x: start.x,
      y: start.y,
      width: end.x - start.x,
      height: end.y - start.y
    })
  },

  /**
   * Check if two rectangles intersect
   */
  rectanglesIntersect(rect1: Rectangle, rect2: Rectangle): boolean {
    return !(rect1.x + rect1.width < rect2.x ||
             rect2.x + rect2.width < rect1.x ||
             rect1.y + rect1.height < rect2.y ||
             rect2.y + rect2.height < rect1.y)
  },

  /**
   * Calculate distance between two points
   */
  pointDistance(p1: Point, p2: Point): number {
    const dx = p2.x - p1.x
    const dy = p2.y - p1.y
    return Math.sqrt(dx * dx + dy * dy)
  },

  /**
   * Check if a point is inside a rectangle
   */
  pointInRectangle(point: Point, rect: Rectangle): boolean {
    return point.x >= rect.x &&
           point.x <= rect.x + rect.width &&
           point.y >= rect.y &&
           point.y <= rect.y + rect.height
  }
}
