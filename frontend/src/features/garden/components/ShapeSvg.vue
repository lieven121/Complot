<script setup lang="ts">
import { computed } from 'vue'
import type { ShapeDto } from '@/shared/api'
import { keyholePath } from '../map'

/** Draws a map shape; extra attributes (fill, class, stroke…) fall through to the SVG element. */
const props = defineProps<{ shape: ShapeDto; rx?: number }>()
const keyhole = computed(() => keyholePath(props.shape))
</script>

<template>
  <ellipse
    v-if="shape.kind === 'ellipse'"
    :cx="shape.x + shape.width / 2"
    :cy="shape.y + shape.height / 2"
    :rx="shape.width / 2"
    :ry="shape.height / 2"
  />
  <path v-else-if="shape.kind === 'keyhole'" :d="keyhole" />
  <rect v-else :x="shape.x" :y="shape.y" :width="shape.width" :height="shape.height" :rx="rx ?? 0.08" />
</template>
