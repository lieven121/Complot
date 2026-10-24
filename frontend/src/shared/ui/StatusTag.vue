<script setup lang="ts">
/** Small coloured pill; `tone` picks the palette colour. */
defineProps<{ tone?: 'ok' | 'warn' | 'danger' | 'info' | 'neutral' }>()
</script>

<template>
  <span class="tag" :class="tone ?? 'neutral'"><slot /></span>
</template>

<style scoped>
.tag {
  display: inline-block;
  font-size: 11px;
  font-weight: 700;
  padding: 1px 8px;
  border-radius: 999px;
  white-space: nowrap;
}
.ok {
  background: var(--c-accent-soft);
  color: var(--c-accent);
}
.warn {
  background: #f6e2b4;
  color: #8a5b00;
}
.danger {
  background: var(--c-warn-soft);
  color: var(--c-warn);
}
.info {
  background: var(--c-info-soft);
  color: var(--c-info);
}
.neutral {
  background: #ebe6da;
  color: var(--c-muted);
}
</style>
