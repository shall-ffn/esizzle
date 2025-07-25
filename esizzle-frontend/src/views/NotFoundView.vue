<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full text-center">
      <div class="mx-auto h-16 w-16 flex items-center justify-center bg-red-100 rounded-full mb-6">
        <ExclamationTriangleIcon class="h-10 w-10 text-red-600" />
      </div>
      
      <h1 class="text-4xl font-bold text-gray-900 mb-4">404</h1>
      <h2 class="text-xl font-semibold text-gray-700 mb-4">Page Not Found</h2>
      <p class="text-gray-600 mb-8">
        The page you're looking for doesn't exist or has been moved.
      </p>
      
      <div class="space-y-4">
        <RouterLink
          to="/"
          class="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-hydra-600 hover:bg-hydra-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-hydra-500"
        >
          <ArrowLeftIcon class="h-4 w-4 mr-2" />
          Go Home
        </RouterLink>
        
        <div>
          <button
            @click="$router.go(-1)"
            class="text-sm text-hydra-600 hover:text-hydra-700"
          >
            ← Go back to previous page
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { ExclamationTriangleIcon, ArrowLeftIcon } from '@heroicons/vue/24/outline'
</script>