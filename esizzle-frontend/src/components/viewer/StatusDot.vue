<template>
  <div 
    class="status-dot"
    :class="getStatusClass()"
    :title="getStatusTitle()"
  >
    <div class="dot-inner"></div>
  </div>
</template>

<script setup lang="ts">
import type { ProcessingStatus } from '@/types/manipulation'

interface Props {
  status: ProcessingStatus
}

const props = defineProps<Props>()

const getStatusClass = () => {
  switch (props.status) {
    case 'processing':
      return 'status-processing'
    case 'completed':
      return 'status-completed'
    case 'error':
      return 'status-error'
    default:
      return 'status-idle'
  }
}

const getStatusTitle = () => {
  switch (props.status) {
    case 'processing':
      return 'Processing in progress'
    case 'completed':
      return 'Processing completed'
    case 'error':
      return 'Processing failed'
    default:
      return 'Ready'
  }
}
</script>

<style scoped>
.status-dot {
  position: relative;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

.dot-inner {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  transition: all 0.2s ease;
}

.status-idle .dot-inner {
  background-color: #6b7280; /* gray-500 */
}

.status-processing .dot-inner {
  background-color: #3b82f6; /* blue-500 */
  animation: pulse 1.5s ease-in-out infinite;
}

.status-completed .dot-inner {
  background-color: #10b981; /* emerald-500 */
}

.status-error .dot-inner {
  background-color: #ef4444; /* red-500 */
  animation: flash 2s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.7;
    transform: scale(1.2);
  }
}

@keyframes flash {
  0%, 50%, 100% {
    opacity: 1;
  }
  25%, 75% {
    opacity: 0.5;
  }
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  .dot-inner {
    animation: none !important;
  }
}
</style>
