<template>
  <div 
    v-if="manipulationState && editMode !== 'view'"
    class="absolute inset-0 pointer-events-none z-20"
  >
    <!-- Page-specific overlays for each page -->
    <div
      v-for="pageIndex in pageCount"
      :key="pageIndex"
      class="absolute page-overlay"
      :style="getPageStyle(pageIndex - 1)"
    >
      <!-- Redaction overlays -->
      <RedactionVisualization
        v-if="editMode === 'redaction' || hasRedactionsOnPage(pageIndex - 1)"
        :redactions="getPageRedactions(pageIndex - 1)"
        :page-number="pageIndex - 1"
        :editable="editMode === 'redaction'"
        :coordinate-translator="coordinateTranslator"
        :zoom-level="zoomLevel"
        @redaction-added="handleRedactionAdded"
        @redaction-updated="handleRedactionUpdated"
        @redaction-removed="handleRedactionRemoved"
      />

      <!-- Page break overlays -->
      <PageBreakVisualization
        v-if="editMode === 'pagebreak' || hasPageBreaksOnPage(pageIndex - 1)"
        :page-breaks="getPageBreaks(pageIndex - 1)"
        :page-number="pageIndex - 1"
        :editable="editMode === 'pagebreak'"
        :document-types="documentTypes"
        @break-added="handlePageBreakAdded"
        @break-removed="handlePageBreakRemoved"
        @break-updated="handlePageBreakUpdated"
      />

      <!-- Page deletion overlays -->
      <PageDeletionVisualization
        v-if="editMode === 'deletion' || isPageDeleted(pageIndex - 1)"
        :page-number="pageIndex - 1"
        :is-deleted="isPageDeleted(pageIndex - 1)"
        :editable="editMode === 'deletion'"
        @deletion-toggled="handlePageDeletionToggled"
      />

      <!-- Rotation indicators -->
      <RotationIndicator
        v-if="editMode === 'rotation' || hasPageRotation(pageIndex - 1)"
        :rotation="getPageRotation(pageIndex - 1)"
        :page-number="pageIndex - 1"
        :editable="editMode === 'rotation'"
        @rotation-changed="handleRotationChanged"
      />
    </div>

    <!-- Mode-specific UI elements -->
    <AnnotationControls
      :edit-mode="editMode"
      :has-changes="manipulationState.hasUnsavedChanges"
      :processing-status="manipulationState.processingStatus"
      :change-summary="changeSummary"
      @mode-changed="handleModeChanged"
      @save-changes="handleSaveChanges"
      @discard-changes="handleDiscardChanges"
    />

    <!-- Processing progress overlay -->
    <ProcessingOverlay
      v-if="manipulationState.processingStatus === 'processing'"
      :progress="processingProgress"
      @cancel="handleCancelProcessing"
    />

    <!-- Error overlay -->
    <ErrorOverlay
      v-if="manipulationState.processingStatus === 'error' || processingError"
      :error="processingError"
      @retry="handleRetryProcessing"
      @dismiss="handleDismissError"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import type { 
  DocumentManipulationState, 
  EditMode, 
  ProcessingProgress,
  RedactionAnnotation,
  RotationAnnotation,
  PageBreakAnnotation,
  PageDeletionAnnotation,
  ChangeSummary,
  ManipulationError,
  DocumentType
} from '@/types/manipulation'
import { CoordinateTranslator } from '@/utils/coordinate-translator'
import RedactionVisualization from './RedactionVisualization.vue'
import PageBreakVisualization from './PageBreakVisualization.vue'
import PageDeletionVisualization from './PageDeletionVisualization.vue'
import RotationIndicator from './RotationIndicator.vue'
import AnnotationControls from './AnnotationControls.vue'
import ProcessingOverlay from './ProcessingOverlay.vue'
import ErrorOverlay from './ErrorOverlay.vue'

interface Props {
  manipulationState: DocumentManipulationState | null
  editMode: EditMode
  pageCount: number
  zoomLevel: number
  coordinateTranslator: CoordinateTranslator | null
  documentTypes: DocumentType[]
  processingProgress?: ProcessingProgress
}

interface Emits {
  (e: 'manipulation-changed', state: DocumentManipulationState): void
  (e: 'mode-changed', mode: EditMode): void
  (e: 'save-requested'): void
  (e: 'discard-requested'): void
  (e: 'processing-cancelled'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const processingError = ref<ManipulationError | null>(null)

// Computed properties
const changeSummary = computed<ChangeSummary>(() => {
  if (!props.manipulationState) {
    return {
      pendingRedactions: 0,
      pendingRotations: 0,
      pendingPageBreaks: 0,
      pendingDeletions: 0,
      totalChanges: 0
    }
  }

  const summary = {
    pendingRedactions: props.manipulationState.redactions.filter(r => !r.applied && !r.deleted).length,
    pendingRotations: props.manipulationState.rotations.length,
    pendingPageBreaks: props.manipulationState.pageBreaks.filter(pb => !pb.deleted).length,
    pendingDeletions: props.manipulationState.pageDeletions.length,
    totalChanges: 0
  }

  summary.totalChanges = summary.pendingRedactions + summary.pendingRotations + 
                        summary.pendingPageBreaks + summary.pendingDeletions

  return summary
})

// Page-specific data getters
const getPageRedactions = (pageNumber: number): RedactionAnnotation[] => {
  if (!props.manipulationState) return []
  return props.manipulationState.redactions.filter(r => 
    r.pageNumber === pageNumber && !r.deleted
  )
}

const getPageBreaks = (pageNumber: number): PageBreakAnnotation[] => {
  if (!props.manipulationState) return []
  return props.manipulationState.pageBreaks.filter(pb => 
    pb.pageIndex === pageNumber && !pb.deleted
  )
}

const getPageRotation = (pageNumber: number): number => {
  if (!props.manipulationState) return 0
  const rotation = props.manipulationState.rotations.find(r => r.pageIndex === pageNumber)
  return rotation?.rotate || 0
}

const isPageDeleted = (pageNumber: number): boolean => {
  if (!props.manipulationState) return false
  return props.manipulationState.pageDeletions.some(pd => pd.pageIndex === pageNumber)
}

// Page content detection
const hasRedactionsOnPage = (pageNumber: number): boolean => {
  return getPageRedactions(pageNumber).length > 0
}

const hasPageBreaksOnPage = (pageNumber: number): boolean => {
  return getPageBreaks(pageNumber).length > 0
}

const hasPageRotation = (pageNumber: number): boolean => {
  return getPageRotation(pageNumber) !== 0
}

// Style calculation for page overlays
const getPageStyle = (pageIndex: number) => {
  // This would be calculated based on PDF.js page positioning
  // For now, assuming single page view
  return {
    top: '0px',
    left: '0px',
    width: '100%',
    height: '100%'
  }
}

// Event handlers
const handleRedactionAdded = (redaction: Omit<RedactionAnnotation, 'id' | 'dateCreated'>) => {
  if (!props.manipulationState) return

  const newRedaction: RedactionAnnotation = {
    ...redaction,
    id: Date.now(), // Temporary ID
    dateCreated: new Date()
  }

  const updatedState = {
    ...props.manipulationState,
    redactions: [...props.manipulationState.redactions, newRedaction],
    hasUnsavedChanges: true,
    lastModified: new Date()
  }

  emit('manipulation-changed', updatedState)
}

const handleRedactionUpdated = (redactionId: number, updates: Partial<RedactionAnnotation>) => {
  if (!props.manipulationState) return

  const updatedState = {
    ...props.manipulationState,
    redactions: props.manipulationState.redactions.map(r => 
      r.id === redactionId ? { ...r, ...updates } : r
    ),
    hasUnsavedChanges: true,
    lastModified: new Date()
  }

  emit('manipulation-changed', updatedState)
}

const handleRedactionRemoved = (redactionId: number) => {
  if (!props.manipulationState) return

  const updatedState = {
    ...props.manipulationState,
    redactions: props.manipulationState.redactions.map(r => 
      r.id === redactionId ? { ...r, deleted: true } : r
    ),
    hasUnsavedChanges: true,
    lastModified: new Date()
  }

  emit('manipulation-changed', updatedState)
}

const handlePageBreakAdded = (pageBreak: Omit<PageBreakAnnotation, 'id'>) => {
  if (!props.manipulationState) return

  const newPageBreak: PageBreakAnnotation = {
    ...pageBreak,
    id: Date.now() // Temporary ID
  }

  const updatedState = {
    ...props.manipulationState,
    pageBreaks: [...props.manipulationState.pageBreaks, newPageBreak],
    hasUnsavedChanges: true,
    lastModified: new Date()
  }

  emit('manipulation-changed', updatedState)
}

const handlePageBreakRemoved = (pageBreakId: number) => {
  if (!props.manipulationState) return

  const updatedState = {
    ...props.manipulationState,
    pageBreaks: props.manipulationState.pageBreaks.map(pb => 
      pb.id === pageBreakId ? { ...pb, deleted: true } : pb
    ),
    hasUnsavedChanges: true,
    lastModified: new Date()
  }

  emit('manipulation-changed', updatedState)
}

const handlePageBreakUpdated = (pageBreakId: number, updates: Partial<PageBreakAnnotation>) => {
  if (!props.manipulationState) return

  const updatedState = {
    ...props.manipulationState,
    pageBreaks: props.manipulationState.pageBreaks.map(pb => 
      pb.id === pageBreakId ? { ...pb, ...updates } : pb
    ),
    hasUnsavedChanges: true,
    lastModified: new Date()
  }

  emit('manipulation-changed', updatedState)
}

const handlePageDeletionToggled = (pageNumber: number) => {
  if (!props.manipulationState) return

  const existingDeletion = props.manipulationState.pageDeletions.find(pd => pd.pageIndex === pageNumber)
  
  let updatedDeletions: PageDeletionAnnotation[]
  if (existingDeletion) {
    // Remove deletion
    updatedDeletions = props.manipulationState.pageDeletions.filter(pd => pd.pageIndex !== pageNumber)
  } else {
    // Add deletion
    const newDeletion: PageDeletionAnnotation = {
      id: Date.now(), // Temporary ID
      imageId: props.manipulationState.documentId,
      pageIndex: pageNumber,
      createdBy: props.manipulationState.modifiedBy,
      dateCreated: new Date()
    }
    updatedDeletions = [...props.manipulationState.pageDeletions, newDeletion]
  }

  const updatedState = {
    ...props.manipulationState,
    pageDeletions: updatedDeletions,
    hasUnsavedChanges: true,
    lastModified: new Date()
  }

  emit('manipulation-changed', updatedState)
}

const handleRotationChanged = (pageNumber: number, rotation: number) => {
  if (!props.manipulationState) return

  const existingRotation = props.manipulationState.rotations.find(r => r.pageIndex === pageNumber)
  
  let updatedRotations: RotationAnnotation[]
  if (existingRotation) {
    if (rotation === 0) {
      // Remove rotation
      updatedRotations = props.manipulationState.rotations.filter(r => r.pageIndex !== pageNumber)
    } else {
      // Update rotation
      updatedRotations = props.manipulationState.rotations.map(r => 
        r.pageIndex === pageNumber ? { ...r, rotate: rotation } : r
      )
    }
  } else if (rotation !== 0) {
    // Add new rotation
    const newRotation: RotationAnnotation = {
      id: Date.now(), // Temporary ID
      imageId: props.manipulationState.documentId,
      pageIndex: pageNumber,
      rotate: rotation
    }
    updatedRotations = [...props.manipulationState.rotations, newRotation]
  } else {
    return // No change needed
  }

  const updatedState = {
    ...props.manipulationState,
    rotations: updatedRotations,
    hasUnsavedChanges: true,
    lastModified: new Date()
  }

  emit('manipulation-changed', updatedState)
}

const handleModeChanged = (mode: EditMode) => {
  emit('mode-changed', mode)
}

const handleSaveChanges = () => {
  emit('save-requested')
}

const handleDiscardChanges = () => {
  emit('discard-requested')
}

const handleCancelProcessing = () => {
  emit('processing-cancelled')
}

const handleRetryProcessing = () => {
  processingError.value = null
  emit('save-requested')
}

const handleDismissError = () => {
  processingError.value = null
}

// Watch for processing errors
watch(() => props.manipulationState?.processingStatus, (status) => {
  if (status === 'error' && props.processingProgress?.error) {
    processingError.value = {
      type: 'processing',
      message: props.processingProgress.error,
      details: props.processingProgress.result
    }
  } else if (status !== 'error') {
    processingError.value = null
  }
})

// Keyboard shortcuts
const handleKeyDown = (event: KeyboardEvent) => {
  // Only handle shortcuts when in manipulation mode
  if (props.editMode === 'view') return

  switch (event.key) {
    case 'Escape':
      handleModeChanged('view')
      break
    case 's':
      if (event.ctrlKey || event.metaKey) {
        event.preventDefault()
        handleSaveChanges()
      }
      break
    case 'z':
      if (event.ctrlKey || event.metaKey) {
        event.preventDefault()
        // TODO: Implement undo
      }
      break
  }
}

onMounted(() => {
  document.addEventListener('keydown', handleKeyDown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeyDown)
})
</script>

<style scoped>
/* Page overlay positioning */
.page-overlay {
  pointer-events: none;
}

.page-overlay > * {
  pointer-events: auto;
}

/* Ensure proper z-indexing */
.z-20 {
  z-index: 20;
}

/* Smooth transitions */
.transition-all {
  transition: all 0.2s ease-in-out;
}
</style>
