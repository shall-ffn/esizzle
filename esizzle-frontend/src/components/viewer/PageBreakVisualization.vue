<template>
  <div 
    v-if="editable || visiblePageBreaks.length > 0"
    class="absolute inset-0 page-break-overlay"
    :class="{ 'editing-mode': editable }"
    @click="handlePageClick"
  >
    <!-- Existing page breaks -->
    <div
      v-for="pageBreak in visiblePageBreaks"
      :key="pageBreak.id"
      :class="getPageBreakClass(pageBreak)"
      :style="getPageBreakStyle(pageBreak)"
      @click.stop="handlePageBreakClick(pageBreak)"
    >
      <div class="page-break-text">
        {{ getPageBreakDisplayText(pageBreak) }}
      </div>
      
      <!-- Page break controls for editing -->
      <div 
        v-if="editable"
        class="page-break-controls absolute -top-12 left-0 flex items-center space-x-2 bg-white border border-gray-300 rounded-lg px-3 py-2 shadow-lg z-10"
      >
        <select
          v-model="pageBreak.imageDocumentTypeId"
          @change="updatePageBreak(pageBreak)"
          class="text-xs border border-gray-300 rounded px-2 py-1"
        >
          <option value="">Select type...</option>
          <option 
            v-for="docType in documentTypes" 
            :key="docType.id" 
            :value="docType.id"
          >
            {{ docType.name }}
          </option>
        </select>
        
        <button
          @click="removePageBreak(pageBreak.id)"
          class="text-xs bg-red-600 text-white px-2 py-1 rounded hover:bg-red-700 transition-colors"
          title="Remove page break"
        >
          Remove
        </button>
        
        <input
          v-if="pageBreak.comments !== undefined"
          v-model="pageBreak.comments"
          @input="updatePageBreak(pageBreak)"
          class="text-xs border border-gray-300 rounded px-2 py-1 w-24"
          placeholder="Comments..."
          title="Optional comments"
        />
      </div>
    </div>

    <!-- Instructions overlay for edit mode -->
    <div 
      v-if="editable && showInstructions"
      class="instructions-overlay absolute top-4 left-4 bg-black bg-opacity-75 text-white p-3 rounded-lg max-w-sm z-20"
    >
      <div class="text-sm">
        <p class="font-semibold mb-2">Page Break Mode Active</p>
        <ul class="text-xs space-y-1">
          <li>• Click at the top of a page to create a document split</li>
          <li>• Green = normal documents, Orange = generic breaks</li>
          <li>• Select document type from dropdown</li>
          <li>• Splits will create separate PDF files</li>
        </ul>
      </div>
      <button
        @click="showInstructions = false"
        class="mt-3 px-2 py-1 bg-gray-600 text-white text-xs rounded hover:bg-gray-700 transition-colors"
      >
        Got it
      </button>
    </div>

    <!-- Page break preview line -->
    <div 
      v-if="editable && hoverY !== null"
      class="page-break-preview absolute left-0 right-0 h-1 bg-blue-500 bg-opacity-60 border-2 border-dashed border-blue-600 z-5"
      :style="{ top: `${hoverY}px` }"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { 
  PageBreakAnnotation,
  DocumentType
} from '@/types/manipulation'
import { 
  isGenericPageBreak, 
  getPageBreakDisplayText, 
  getPageBreakClass 
} from '@/types/manipulation'

interface Props {
  pageBreaks: PageBreakAnnotation[]
  pageNumber: number
  editable: boolean
  documentTypes: DocumentType[]
}

interface Emits {
  (e: 'break-added', pageBreak: Omit<PageBreakAnnotation, 'id'>): void
  (e: 'break-removed', pageBreakId: number): void
  (e: 'break-updated', pageBreakId: number, updates: Partial<PageBreakAnnotation>): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Local state
const showInstructions = ref(true)
const hoverY = ref<number | null>(null)

// Computed properties
const visiblePageBreaks = computed(() => 
  props.pageBreaks.filter(pb => pb.pageIndex === props.pageNumber && !pb.deleted)
)

// Event handlers
const handlePageClick = (event: MouseEvent) => {
  if (!props.editable) return
  
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  const clickY = event.clientY - rect.top
  
  // Only allow clicks in the top portion of the page (first 50px)
  if (clickY > 50) return
  
  // Create new page break
  const newPageBreak: Omit<PageBreakAnnotation, 'id'> = {
    imageId: 0, // Will be set by parent
    pageIndex: props.pageNumber,
    text: 'Document Break',
    imageDocumentTypeId: 0, // User will need to select
    displayText: 'New Document Break',
    deleted: false,
    documentDate: new Date(),
    comments: ''
  }
  
  emit('break-added', newPageBreak)
}

const handlePageBreakClick = (pageBreak: PageBreakAnnotation) => {
  if (!props.editable) return
  console.log('Page break clicked:', pageBreak)
}

const removePageBreak = (pageBreakId: number) => {
  emit('break-removed', pageBreakId)
}

const updatePageBreak = (pageBreak: PageBreakAnnotation) => {
  // Update display text based on document type
  const docType = props.documentTypes.find(dt => dt.id === pageBreak.imageDocumentTypeId)
  if (docType) {
    const updates: Partial<PageBreakAnnotation> = {
      imageDocumentTypeId: pageBreak.imageDocumentTypeId,
      displayText: docType.name,
      text: docType.name,
      comments: pageBreak.comments
    }
    emit('break-updated', pageBreak.id, updates)
  }
}

// Style calculations - using imported utility function
// getPageBreakClass is imported from types/manipulation

const getPageBreakStyle = (pageBreak: PageBreakAnnotation) => {
  return {
    top: '-22px', // Position above the page
    left: '0',
    right: '0',
    zIndex: 10
  }
}

const getDocumentTypeName = (docTypeId: number): string => {
  const docType = props.documentTypes.find(dt => dt.id === docTypeId)
  return docType ? docType.name : 'Unknown Type'
}

// Mouse tracking for preview line
const handleMouseMove = (event: MouseEvent) => {
  if (!props.editable) return
  
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  const mouseY = event.clientY - rect.top
  
  // Only show preview in top portion
  if (mouseY <= 50) {
    hoverY.value = mouseY
  } else {
    hoverY.value = null
  }
}

const handleMouseLeave = () => {
  hoverY.value = null
}

// Lifecycle
onMounted(() => {
  // Auto-hide instructions after 8 seconds
  setTimeout(() => {
    showInstructions.value = false
  }, 8000)
  
  // Add mouse tracking if editable
  if (props.editable) {
    const overlay = document.querySelector('.page-break-overlay') as HTMLElement
    if (overlay) {
      overlay.addEventListener('mousemove', handleMouseMove)
      overlay.addEventListener('mouseleave', handleMouseLeave)
    }
  }
})
</script>

<style scoped>
.page-break-overlay {
  pointer-events: auto;
  cursor: default;
}

.page-break-overlay.editing-mode {
  cursor: crosshair;
}

/* Normal document breaks - green (matches Hydra original) */
.page-break-normal {
  background-color: rgba(0, 128, 0, 0.9);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.page-break-normal:hover {
  background-color: rgba(0, 128, 0, 1);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

/* Generic breaks - orange (matches Hydra original) */
.page-break-generic {
  background-color: rgba(255, 165, 0, 0.9);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.page-break-generic:hover {
  background-color: rgba(255, 165, 0, 1);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

.page-break-text {
  color: white;
  font-weight: bold;
  font-size: 12px;
  text-shadow: 1px 1px 2px rgba(0,0,0,0.5);
  padding: 2px 6px;
  background-color: rgba(0,0,0,0.3);
  border-radius: 3px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
}

/* Control styling */
.page-break-controls {
  opacity: 0;
  transition: opacity 0.2s ease-in-out;
  pointer-events: none;
}

.page-break-normal:hover .page-break-controls,
.page-break-generic:hover .page-break-controls {
  opacity: 1;
  pointer-events: auto;
}

/* Preview line */
.page-break-preview {
  transition: top 0.1s ease-out;
}

/* Instructions overlay */
.instructions-overlay {
  animation: fadeInUp 0.3s ease-out;
}

@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Transitions */
.transition-all {
  transition: all 0.2s ease-in-out;
}

/* Responsive controls */
@media (max-width: 768px) {
  .page-break-controls {
    flex-direction: column;
    align-items: stretch;
  }
  
  .page-break-controls > * {
    margin: 2px 0;
  }
}
</style>
