<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full space-y-8">
      <div>
        <div class="mx-auto h-12 w-12 flex items-center justify-center bg-hydra-100 rounded-full">
          <DocumentIcon class="h-8 w-8 text-hydra-600" />
        </div>
        <h2 class="mt-6 text-center text-3xl font-extrabold text-gray-900">
          Sign in to Hydra DD
        </h2>
        <p class="mt-2 text-center text-sm text-gray-600">
          Due Diligence Application
        </p>
      </div>
      
      <form class="mt-8 space-y-6" @submit.prevent="handleLogin">
        <div class="space-y-4">
          <div>
            <label for="username" class="block text-sm font-medium text-gray-700">
              Username
            </label>
            <input
              id="username"
              name="username"
              type="text"
              required
              class="mt-1 appearance-none relative block w-full px-3 py-2 border border-gray-300 placeholder-gray-500 text-gray-900 rounded-md focus:outline-none focus:ring-hydra-500 focus:border-hydra-500 focus:z-10 sm:text-sm"
              placeholder="Enter your username"
              v-model="loginForm.username"
              :disabled="isLoading"
            />
          </div>
          
          <div>
            <label for="password" class="block text-sm font-medium text-gray-700">
              Password
            </label>
            <input
              id="password"
              name="password"
              type="password"
              required
              class="mt-1 appearance-none relative block w-full px-3 py-2 border border-gray-300 placeholder-gray-500 text-gray-900 rounded-md focus:outline-none focus:ring-hydra-500 focus:border-hydra-500 focus:z-10 sm:text-sm"
              placeholder="Enter your password"
              v-model="loginForm.password"
              :disabled="isLoading"
            />
          </div>
        </div>

        <div v-if="error" class="bg-red-50 border border-red-200 rounded-md p-3">
          <div class="flex">
            <ExclamationTriangleIcon class="h-5 w-5 text-red-400" />
            <div class="ml-3">
              <p class="text-sm text-red-800">{{ error }}</p>
            </div>
          </div>
        </div>

        <div>
          <button
            type="submit"
            :disabled="isLoading || !loginForm.username || !loginForm.password"
            class="group relative w-full flex justify-center py-2 px-4 border border-transparent text-sm font-medium rounded-md text-white bg-hydra-600 hover:bg-hydra-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-hydra-500 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <div v-if="isLoading" class="spinner mr-2 !h-4 !w-4 !border-white !border-t-transparent"></div>
            {{ isLoading ? 'Signing in...' : 'Sign in' }}
          </button>
        </div>
      </form>
      
      <div class="text-center text-xs text-gray-500">
        <p>For demo purposes, use any username/password combination</p>
      </div>
      
      <div id="login-debug" class="mt-4 p-2 bg-gray-100 text-xs font-mono text-gray-700 rounded" style="white-space: pre-line; display: none;"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { DocumentIcon, ExclamationTriangleIcon } from '@heroicons/vue/24/outline'
import { apiClient } from '@/services/api'
import { useMainStore } from '@/stores/main'

const router = useRouter()
const mainStore = useMainStore()

const isLoading = ref(false)
const error = ref('')

const loginForm = ref({
  username: '',
  password: ''
})

const handleLogin = async () => {
  try {
    // Make debug element visible
    const debugElement = document.getElementById('login-debug');
    if (debugElement) {
      debugElement.style.display = 'block';
      debugElement.textContent = 'Login attempt started: ' + new Date().toISOString();
    }
    
    console.log('Login attempt started');
    
    if (!loginForm.value.username || !loginForm.value.password) {
      console.log('Missing username or password');
      if (debugElement) {
        debugElement.textContent += '\nMissing username or password';
      }
      return;
    }

    isLoading.value = true;
    error.value = '';
    
    if (debugElement) {
      debugElement.textContent += '\nSetting up mock user for: ' + loginForm.value.username;
    }
    console.log('Setting up mock user for', loginForm.value.username);
    
    // For demo purposes, use simplified mock login
    // Include all required fields for the User type
    const mockUser = {
      id: 1,
      name: loginForm.value.username,
      userName: loginForm.value.username,
      email: `${loginForm.value.username}@ffncorp.com`,
      accessLevel: 2,
      clientId: 1
    };

    // Set a mock JWT token
    const mockToken = 'mock-jwt-token-' + Date.now();
    if (debugElement) {
      debugElement.textContent += '\nSetting auth token: ' + mockToken;
    }
    localStorage.setItem('auth_token', mockToken);
    
    // Store user info for API headers
    localStorage.setItem('mock_user_info', JSON.stringify({
      id: 21496,  // Stephen Hall (admin user) ID
      email: email,
      name: 'Stephen Hall'
    }));
    
    // Set user in store
    if (debugElement) {
      debugElement.textContent += '\nSetting user in store';
    }
    mainStore.currentUser = mockUser;

    // Navigate to home page
    if (debugElement) {
      debugElement.textContent += '\nNavigating to home page...';
    }
    console.log('About to navigate to home page');
    
    // Direct navigation - the router guard will handle the redirect logic
    await router.push({ path: '/' });
    console.log('Navigation completed');
    
    if (debugElement) {
      debugElement.textContent += '\nNavigation completed successfully!';
    }
    
  } catch (err: unknown) {
    const debugElement = document.getElementById('login-debug');
    if (debugElement) {
      debugElement.style.display = 'block';
    }
    console.error('Login error:', err);
    
    const errorMessage = err instanceof Error ? err.message : String(err);
    
    if (debugElement) {
      debugElement.textContent += '\nError: ' + errorMessage;
    }
    error.value = 'Error during login: ' + errorMessage;
  } finally {
    isLoading.value = false;
  }
}
</script>