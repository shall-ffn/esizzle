import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'Home',
    component: () => import('@/views/HomeView.vue'),
    meta: {
      requiresAuth: true
    }
  },
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/LoginView.vue'),
    meta: {
      requiresAuth: false
    }
  },
  {
    path: '/offering/:offeringId',
    name: 'Offering',
    component: () => import('@/views/HomeView.vue'),
    props: route => ({ offeringId: parseInt(route.params.offeringId as string) }),
    meta: {
      requiresAuth: true
    }
  },
  {
    path: '/offering/:offeringId/sale/:saleId',
    name: 'Sale',
    component: () => import('@/views/HomeView.vue'),
    props: route => ({
      offeringId: parseInt(route.params.offeringId as string),
      saleId: parseInt(route.params.saleId as string)
    }),
    meta: {
      requiresAuth: true
    }
  },
  {
    path: '/offering/:offeringId/sale/:saleId/loan/:loanId',
    name: 'Loan',
    component: () => import('@/views/HomeView.vue'),
    props: route => ({
      offeringId: parseInt(route.params.offeringId as string),
      saleId: parseInt(route.params.saleId as string),
      loanId: parseInt(route.params.loanId as string)
    }),
    meta: {
      requiresAuth: true
    }
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    component: () => import('@/views/NotFoundView.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// Navigation guard for authentication
router.beforeEach((to, from, next) => {
  console.log('🔐 Router navigation guard - Navigating from:', from.fullPath, 'to:', to.fullPath);
  console.log('🔐 Route names - from:', from.name, 'to:', to.name);
  
  const token = localStorage.getItem('auth_token');
  console.log('🔐 Auth token exists:', !!token);
  console.log('🔐 Auth token value:', token);
  
  const requiresAuth = to.meta.requiresAuth !== false;
  console.log('🔐 Destination requires auth:', requiresAuth);
  console.log('🔐 to.meta:', to.meta);

  // Check if user is trying to access login page while already authenticated
  if (to.name === 'Login' && token) {
    // Redirect to home if already authenticated and trying to access login
    console.log('🔐 Already authenticated, redirecting to home from login page');
    next('/');
    return;
  }

  // Check if user is trying to access protected route without authentication
  if (requiresAuth && !token) {
    // Redirect to login if authentication is required but no token exists
    console.log('🔐 No token, redirecting to login');
    next('/login');
    return;
  }

  // All other cases - proceed with navigation
  console.log('🔐 Proceeding with navigation to', to.fullPath);
  next();
})

export default router