<template>
  <div class="h-screen flex flex-col bg-gray-50">
    <!-- Header/Menu Bar matching legacy eStacker -->
    <header class="bg-white border-b border-gray-200 px-4 py-2 flex items-center justify-between">
      <div class="flex items-center space-x-4">
        <h1 class="text-lg font-semibold text-gray-900">
          Hydra Due Diligence - eStacker Web
        </h1>
        
        <!-- Breadcrumb showing current selection -->
        <nav class="flex items-center space-x-2 text-sm text-gray-500" v-if="mainStore.selectionPath.length > 0">
          <span>•</span>
          <template v-for="(item, index) in mainStore.selectionPath" :key="index">
            <span class="text-gray-700">{{ item.name }}</span>
            <span v-if="index < mainStore.selectionPath.length - 1" class="text-gray-400">→</span>
          </template>
        </nav>
      </div>
      
      <div class="flex items-center space-x-4">
        <!-- User info -->
        <div class="text-sm text-gray-600" v-if="mainStore.currentUser">
          Logged in: {{ mainStore.currentUser.name }}
        </div>
        
        <!-- Menu items matching legacy -->
        <div class="flex items-center space-x-2">
          <button class="toolbar-button text-xs">IT Admin</button>
          <button class="toolbar-button text-xs">Utilities</button>
          <button class="toolbar-button text-xs">Imaging Admin</button>
          <button class="toolbar-button text-xs">eStacker</button>
          <button class="toolbar-button text-xs">Export</button>
          <button class="toolbar-button text-xs">View</button>
          <button class="toolbar-button text-xs">Reports</button>
          <button class="toolbar-button text-xs">Help</button>
        </div>
      </div>
    </header>

    <!-- Main Content Area - Three Panel Layout -->
    <div class="flex-1 flex overflow-hidden">
      <!-- Left Panel - Document Grid and Actions -->
      <div 
        class="bg-white border-r border-gray-200 flex flex-col"
        :style="{ width: `${mainStore.panelSizes.left}px` }"
      >
        <LeftPanel />
      </div>

      <!-- Resize Handle for Left Panel -->
      <div 
        class="resize-handle"
        @mousedown="startResize('left', $event)"
      ></div>

      <!-- Center Panel - PDF Viewer -->
      <div 
        class="bg-gray-800 flex flex-col flex-1"
        :style="{ width: `${mainStore.panelSizes.center}px` }"
      >
        <CenterPanel />
      </div>

      <!-- Resize Handle for Right Panel -->
      <div 
        class="resize-handle"
        @mousedown="startResize('right', $event)"
      ></div>

      <!-- Right Panel - Selection and Indexing -->
      <div 
        class="bg-white border-l border-gray-200 flex flex-col"
        :style="{ width: `${mainStore.panelSizes.right}px` }"
      >
        <RightPanel />
      </div>
    </div>

    <!-- Status Bar -->
    <footer class="bg-gray-100 border-t border-gray-200 px-4 py-1 text-xs text-gray-600 flex items-center justify-between">
      <div class="flex items-center space-x-4">
        <span v-if="mainStore.selectedLoan">
          Loan: {{ mainStore.selectedLoan.assetName }} ({{ mainStore.selectedLoan.assetNo }})
        </span>
        <span v-if="mainStore.selectedDocument && mainStore.totalPages > 0">
          Page {{ mainStore.currentPage }} of {{ mainStore.totalPages }}
        </span>
      </div>
      
      <div class="flex items-center space-x-4">
        <span v-if="mainStore.selectedDocument">
          {{ mainStore.selectedDocument.originalName }}
        </span>
        <span v-if="mainStore.isLoading" class="text-hydra-600">
          Loading...
        </span>
        <span>Ready</span>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useMainStore } from '@/stores/main'
import LeftPanel from './LeftPanel.vue'
import CenterPanel from './CenterPanel.vue'
import RightPanel from './RightPanel.vue'

const mainStore = useMainStore()

// Panel resizing functionality
const isResizing = ref(false)
const resizePanel = ref<'left' | 'right' | null>(null)
const initialMouseX = ref(0)
const initialPanelSize = ref(0)

const startResize = (panel: 'left' | 'right', event: MouseEvent) => {
  isResizing.value = true
  resizePanel.value = panel
  initialMouseX.value = event.clientX
  initialPanelSize.value = panel === 'left' 
    ? mainStore.panelSizes.left 
    : mainStore.panelSizes.right

  document.addEventListener('mousemove', handleResize)
  document.addEventListener('mouseup', stopResize)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}

const handleResize = (event: MouseEvent) => {
  if (!isResizing.value || !resizePanel.value) return

  const deltaX = event.clientX - initialMouseX.value
  const newSize = initialPanelSize.value + (resizePanel.value === 'left' ? deltaX : -deltaX)
  
  // Set minimum and maximum panel sizes
  const minSize = 200
  const maxSize = window.innerWidth * 0.4
  
  if (newSize >= minSize && newSize <= maxSize) {
    if (resizePanel.value === 'left') {
      mainStore.setPanelSizes({ left: newSize })
    } else {
      mainStore.setPanelSizes({ right: newSize })
    }
  }
}

const stopResize = () => {
  isResizing.value = false
  resizePanel.value = null
  document.removeEventListener('mousemove', handleResize)
  document.removeEventListener('mouseup', stopResize)
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
}

// Cleanup event listeners on unmount
onUnmounted(() => {
  document.removeEventListener('mousemove', handleResize)
  document.removeEventListener('mouseup', stopResize)
})

// Load user offerings on mount
onMounted(() => {
  mainStore.loadUserOfferings()
})
</script>

<style scoped>
/* Prevent text selection during resize */
.resize-handle:active {
  user-select: none;
}
</style>