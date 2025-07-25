<template>
  <div v-if="showDevStatus" class="fixed bottom-4 right-4 z-50">
    <!-- Toggle Button -->
    <button
      v-if="!showPanel"
      @click="showPanel = true"
      class="bg-gray-800 text-white p-2 rounded-full shadow-lg hover:bg-gray-700 transition-colors"
      title="Show development status"
    >
      <Cog6ToothIcon class="h-5 w-5" />
    </button>

    <!-- Status Panel -->
    <div
      v-if="showPanel"
      class="bg-white border border-gray-300 rounded-lg shadow-lg w-96 max-h-96 overflow-y-auto"
    >
      <!-- Header -->
      <div class="bg-gray-50 px-4 py-3 border-b border-gray-200 flex items-center justify-between">
        <h3 class="text-sm font-medium text-gray-900">Development Status</h3>
        <div class="flex items-center space-x-2">
          <button
            @click="refreshStatus"
            :disabled="loading"
            class="p-1 text-gray-500 hover:text-gray-700 disabled:opacity-50"
            title="Refresh status"
          >
            <ArrowPathIcon :class="['h-4 w-4', { 'animate-spin': loading }]" />
          </button>
          <button
            @click="showPanel = false"
            class="p-1 text-gray-500 hover:text-gray-700"
            title="Close panel"
          >
            <XMarkIcon class="h-4 w-4" />
          </button>
        </div>
      </div>

      <!-- Loading State -->
      <div v-if="loading && !systemStatus" class="p-4 text-center">
        <div class="animate-spin h-6 w-6 border-2 border-gray-300 border-t-blue-600 rounded-full mx-auto mb-2"></div>
        <p class="text-sm text-gray-600">Checking system status...</p>
      </div>

      <!-- System Status -->
      <div v-else-if="systemStatus" class="p-4 space-y-4">
        <!-- Overall Status -->
        <div class="flex items-center justify-between">
          <span class="text-sm font-medium text-gray-700">Overall Status:</span>
          <span :class="getStatusBadgeClass(systemStatus.overall)">
            {{ systemStatus.overall.toUpperCase() }}
          </span>
        </div>

        <!-- Frontend Status -->
        <div class="border-t border-gray-200 pt-3">
          <div class="flex items-center justify-between mb-2">
            <h4 class="text-sm font-medium text-gray-700">Frontend</h4>
            <span :class="getStatusBadgeClass(systemStatus.frontend.status)">
              {{ systemStatus.frontend.status.toUpperCase() }}
            </span>
          </div>
          <div class="space-y-1">
            <div
              v-for="check in systemStatus.frontend.checks"
              :key="check.name"
              class="flex items-center justify-between text-xs"
            >
              <span class="text-gray-600">{{ check.name }}:</span>
              <div class="flex items-center space-x-1">
                <span :class="getCheckStatusClass(check.status)">
                  {{ check.status === 'pass' ? '✓' : check.status === 'warn' ? '⚠' : '✗' }}
                </span>
                <span class="text-gray-500 max-w-24 truncate" :title="check.message">
                  {{ check.message }}
                </span>
              </div>
            </div>
          </div>
        </div>

        <!-- Backend Status -->
        <div class="border-t border-gray-200 pt-3">
          <div class="flex items-center justify-between mb-2">
            <h4 class="text-sm font-medium text-gray-700">Backend API</h4>
            <span :class="systemStatus.backend ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'" class="px-2 py-1 text-xs rounded">
              {{ systemStatus.backend ? 'CONNECTED' : 'DISCONNECTED' }}
            </span>
          </div>
          <div v-if="systemStatus.backend" class="text-xs text-gray-600 space-y-1">
            <div class="flex justify-between">
              <span>Version:</span>
              <span>{{ systemStatus.backend.version }}</span>
            </div>
            <div class="flex justify-between">
              <span>Environment:</span>
              <span>{{ systemStatus.backend.environment }}</span>
            </div>
          </div>
          <div v-else class="text-xs text-red-600">
            API not accessible
          </div>
        </div>

        <!-- Database Status -->
        <div class="border-t border-gray-200 pt-3">
          <div class="flex items-center justify-between mb-2">
            <h4 class="text-sm font-medium text-gray-700">Database</h4>
            <span :class="systemStatus.database?.status === 'healthy' ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'" class="px-2 py-1 text-xs rounded">
              {{ systemStatus.database?.status?.toUpperCase() || 'UNKNOWN' }}
            </span>
          </div>
          <div v-if="systemStatus.database" class="text-xs text-gray-600 space-y-1">
            <div class="flex justify-between">
              <span>Connection:</span>
              <span>{{ systemStatus.database.connectionState || 'Unknown' }}</span>
            </div>
            <div class="flex justify-between">
              <span>Tables:</span>
              <span>{{ systemStatus.database.tablesAccessible ? 'Accessible' : 'Not accessible' }}</span>
            </div>
            <div class="text-xs text-gray-500 mt-1">
              {{ systemStatus.database.message }}
            </div>
          </div>
        </div>

        <!-- Environment Info -->
        <div class="border-t border-gray-200 pt-3">
          <h4 class="text-sm font-medium text-gray-700 mb-2">Environment</h4>
          <div class="text-xs text-gray-600 space-y-1">
            <div class="flex justify-between">
              <span>Mode:</span>
              <span>{{ appMode }}</span>
            </div>
            <div class="flex justify-between">
              <span>API URL:</span>
              <span class="truncate ml-2">{{ apiBaseUrl }}</span>
            </div>
            <div class="flex justify-between">
              <span>Debug:</span>
              <span>{{ debugEnabled }}</span>
            </div>
          </div>
        </div>

        <!-- Quick Actions -->
        <div class="border-t border-gray-200 pt-3">
          <h4 class="text-sm font-medium text-gray-700 mb-2">Quick Actions</h4>
          <div class="flex space-x-2">
            <button
              @click="openApiDocs"
              class="px-3 py-1 text-xs bg-blue-100 text-blue-800 rounded hover:bg-blue-200 transition-colors"
            >
              API Docs
            </button>
            <button
              @click="clearLocalStorage"
              class="px-3 py-1 text-xs bg-yellow-100 text-yellow-800 rounded hover:bg-yellow-200 transition-colors"
            >
              Clear Storage
            </button>
            <button
              @click="exportDebugInfo"
              class="px-3 py-1 text-xs bg-gray-100 text-gray-800 rounded hover:bg-gray-200 transition-colors"
            >
              Export Debug
            </button>
          </div>
        </div>
      </div>

      <!-- Error State -->
      <div v-else-if="error" class="p-4">
        <div class="text-center text-red-600">
          <ExclamationTriangleIcon class="h-6 w-6 mx-auto mb-2" />
          <p class="text-sm">{{ error }}</p>
          <button
            @click="refreshStatus"
            class="mt-2 px-3 py-1 text-xs bg-red-100 text-red-800 rounded hover:bg-red-200 transition-colors"
          >
            Retry
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { healthService } from '@/services/health.service'
import type { HealthStatus, DetailedHealthStatus } from '@/services/health.service'
import {
  Cog6ToothIcon,
  ArrowPathIcon,
  XMarkIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'

// Component state
const showPanel = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)
const systemStatus = ref<{
  frontend: ReturnType<typeof healthService.validateFrontendConfig>
  backend: HealthStatus | null
  database: DetailedHealthStatus['checks']['database'] | null
  overall: 'healthy' | 'warning' | 'error'
} | null>(null)

// Environment variables
const appMode = computed(() => import.meta.env.MODE)
const apiBaseUrl = computed(() => import.meta.env.VITE_API_BASE_URL || 'Default')
const debugEnabled = computed(() => import.meta.env.VITE_ENABLE_DEBUG_LOGGING === 'true' ? 'Enabled' : 'Disabled')

// Show dev status only in development
const showDevStatus = computed(() => {
  return import.meta.env.MODE === 'development' || import.meta.env.VITE_ENABLE_DEBUG_LOGGING === 'true'
})

// Status check
const refreshStatus = async () => {
  loading.value = true
  error.value = null

  try {
    systemStatus.value = await healthService.runSystemCheck()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Unknown error occurred'
    console.error('System status check failed:', err)
  } finally {
    loading.value = false
  }
}

// Utility functions
const getStatusBadgeClass = (status: string) => {
  switch (status) {
    case 'healthy':
      return 'bg-green-100 text-green-800 px-2 py-1 text-xs rounded'
    case 'warning':
      return 'bg-yellow-100 text-yellow-800 px-2 py-1 text-xs rounded'
    case 'error':
      return 'bg-red-100 text-red-800 px-2 py-1 text-xs rounded'
    default:
      return 'bg-gray-100 text-gray-800 px-2 py-1 text-xs rounded'
  }
}

const getCheckStatusClass = (status: string) => {
  switch (status) {
    case 'pass':
      return 'text-green-600'
    case 'warn':
      return 'text-yellow-600'
    case 'fail':
      return 'text-red-600'
    default:
      return 'text-gray-600'
  }
}

// Quick actions
const openApiDocs = () => {
  const apiUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'
  const swaggerUrl = apiUrl.replace('/api', '/swagger')
  window.open(swaggerUrl, '_blank')
}

const clearLocalStorage = () => {
  localStorage.clear()
  sessionStorage.clear()
  alert('Local storage cleared. Refresh the page to see changes.')
}

const exportDebugInfo = () => {
  const debugInfo = {
    timestamp: new Date().toISOString(),
    systemStatus: systemStatus.value,
    environment: {
      mode: import.meta.env.MODE,
      apiBaseUrl: import.meta.env.VITE_API_BASE_URL,
      debugLogging: import.meta.env.VITE_ENABLE_DEBUG_LOGGING,
      userAgent: navigator.userAgent,
      url: window.location.href
    },
    localStorage: Object.keys(localStorage).reduce((acc, key) => {
      acc[key] = localStorage.getItem(key)
      return acc
    }, {} as Record<string, string | null>)
  }

  const blob = new Blob([JSON.stringify(debugInfo, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `esizzle-debug-${new Date().toISOString().slice(0, 10)}.json`
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

// Initialize on mount
onMounted(() => {
  if (showDevStatus.value) {
    refreshStatus()
  }
})
</script>

<style scoped>
/* Ensure the component is always on top */
.z-50 {
  z-index: 50;
}

/* Scrollbar styling for the panel */
.overflow-y-auto::-webkit-scrollbar {
  width: 6px;
}

.overflow-y-auto::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.overflow-y-auto::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.overflow-y-auto::-webkit-scrollbar-thumb:hover {
  background: #a8a8a8;
}
</style>