<template>
  <div 
    class="spinner"
    :class="[sizeClass, colorClass]"
    role="status"
    :aria-label="label"
  >
    <div class="spinner-inner"></div>
    <span v-if="showLabel" class="sr-only">{{ label }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl'
  color?: 'blue' | 'white' | 'gray' | 'red' | 'green'
  label?: string
  showLabel?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  size: 'md',
  color: 'blue',
  label: 'Loading...',
  showLabel: false
})

const sizeClass = computed(() => {
  const sizes = {
    xs: 'spinner-xs',
    sm: 'spinner-sm', 
    md: 'spinner-md',
    lg: 'spinner-lg',
    xl: 'spinner-xl'
  }
  return sizes[props.size]
})

const colorClass = computed(() => {
  const colors = {
    blue: 'spinner-blue',
    white: 'spinner-white',
    gray: 'spinner-gray',
    red: 'spinner-red',
    green: 'spinner-green'
  }
  return colors[props.color]
})
</script>

<style scoped>
.spinner {
  display: inline-block;
  position: relative;
}

.spinner-inner {
  border-radius: 50%;
  border-style: solid;
  animation: spin 1s linear infinite;
}

/* Sizes */
.spinner-xs .spinner-inner {
  width: 12px;
  height: 12px;
  border-width: 1px;
}

.spinner-sm .spinner-inner {
  width: 16px;
  height: 16px;
  border-width: 2px;
}

.spinner-md .spinner-inner {
  width: 20px;
  height: 20px;
  border-width: 2px;
}

.spinner-lg .spinner-inner {
  width: 24px;
  height: 24px;
  border-width: 3px;
}

.spinner-xl .spinner-inner {
  width: 32px;
  height: 32px;
  border-width: 3px;
}

/* Colors */
.spinner-blue .spinner-inner {
  border-color: #e5e7eb;
  border-top-color: #3b82f6;
}

.spinner-white .spinner-inner {
  border-color: rgba(255, 255, 255, 0.3);
  border-top-color: #ffffff;
}

.spinner-gray .spinner-inner {
  border-color: #e5e7eb;
  border-top-color: #6b7280;
}

.spinner-red .spinner-inner {
  border-color: #fecaca;
  border-top-color: #ef4444;
}

.spinner-green .spinner-inner {
  border-color: #bbf7d0;
  border-top-color: #10b981;
}

/* Animation */
@keyframes spin {
  0% {
    transform: rotate(0deg);
  }
  100% {
    transform: rotate(360deg);
  }
}

/* Screen reader only class */
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  .spinner-inner {
    animation: none;
  }
}
</style>
