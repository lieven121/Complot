<script setup lang="ts">
import type { CategoryDto } from '@/shared/api'

defineProps<{ category?: CategoryDto }>()
</script>

<template>
  <span v-if="category" class="chip" :style="{ '--c': category.color }">{{ category.icon }} {{ category.name }}</span>
</template>

<style scoped>
.chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11.5px;
  padding: 2px 8px;
  border-radius: 999px;
  font-weight: 600;
  white-space: nowrap;
  background: color-mix(in srgb, var(--c) 18%, #fff);
  color: color-mix(in srgb, var(--c) 75%, #000);
  border: 1px solid color-mix(in srgb, var(--c) 35%, #fff);
}
</style>
