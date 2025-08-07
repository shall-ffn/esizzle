<template>
  <button
    :class="[
      'px-3 py-2 text-sm font-medium transition-colors flex items-center space-x-2 min-w-[80px] justify-center',
      {
        'bg-hydra-100 text-hydra-800 border-r border-hydra-200': active,
        'bg-white text-gray-700 hover:bg-gray-50 border-r border-gray-300': !active,
        'opacity-50 cursor-not-allowed': disabled,
        'cursor-pointer': !disabled
      }
    ]"
    :disabled="disabled"
    @click="handleClick"
  >
    <slot />
  </button>
</template>

<script setup lang="ts">
import type { EditMode } from '@/types/manipulation'

interface Props {
  mode: EditMode
  active: boolean
  disabled?: boolean
}

interface Emits {
  (e: 'click'): void
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false
})

const emit = defineEmits<Emits>()

const handleClick = () => {
  if (!props.disabled) {
    emit('click')
  }
}
</script>
