<template>
  <div id="app" class="h-screen bg-gray-50">
    <RouterView />
    
    <!-- Global Error Toast -->
    <Transition
      enter-active-class="transition ease-out duration-300"
      enter-from-class="opacity-0 transform translate-y-2"
      enter-to-class="opacity-100 transform translate-y-0"
      leave-active-class="transition ease-in duration-200"
      leave-from-class="opacity-100 transform translate-y-0"
      leave-to-class="opacity-0 transform translate-y-2"
    >
      <div
        v-if="mainStore.error"
        class="fixed top-4 right-4 z-50 max-w-md"
      >
        <div class="bg-red-50 border border-red-200 rounded-lg p-4 shadow-lg">
          <div class="flex items-start">
            <div class="flex-shrink-0">
              <ExclamationTriangleIcon class="h-5 w-5 text-red-400" />
            </div>
            <div class="ml-3 flex-1">
              <p class="text-sm font-medium text-red-800">
                Error
              </p>
              <p class="mt-1 text-sm text-red-700">
                {{ mainStore.error }}
              </p>
            </div>
            <div class="ml-4 flex-shrink-0">
              <button
                @click="mainStore.clearError()"
                class="inline-flex text-red-400 hover:text-red-500 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 focus:ring-offset-red-50 rounded-md"
              >
                <span class="sr-only">Dismiss</span>
                <XMarkIcon class="h-5 w-5" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Global Loading Overlay -->
    <Transition
      enter-active-class="transition ease-out duration-200"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition ease-in duration-150"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="showGlobalLoading"
        class="fixed inset-0 z-40 bg-white bg-opacity-75 flex items-center justify-center"
      >
        <div class="text-center">
          <div class="spinner mb-4"></div>
          <p class="text-sm text-gray-600">Loading...</p>
        </div>
      </div>
    </Transition>

    <!-- Development Status Component -->
    <DevStatus />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterView } from 'vue-router'
import { useMainStore } from '@/stores/main'
import DevStatus from '@/components/dev/DevStatus.vue'
import { ExclamationTriangleIcon, XMarkIcon } from '@heroicons/vue/24/outline'

const mainStore = useMainStore()

// Show global loading overlay for critical operations
const showGlobalLoading = computed(() => {
  return mainStore.loading.offerings && mainStore.userOfferings.length === 0
})

// Auto-dismiss errors after 10 seconds
let errorTimeout: NodeJS.Timeout | null = null

// Watch for error changes to set auto-dismiss timer
// Using a watcher would require import { watch } from 'vue'
// For simplicity, we'll handle this in the component that sets the error
</script>

<style scoped>
/* Component-specific styles if needed */
</style>