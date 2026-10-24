<script setup lang="ts">
/** Label + control + (translated) server error, with the accessibility wiring in one place. */
defineProps<{ label: string; for?: string; error?: string; hint?: string; full?: boolean }>()
</script>

<template>
  <div class="field" :class="{ full }">
    <label :for="$props.for">{{ label }}</label>
    <slot />
    <small v-if="error" class="error" role="alert">{{ error }}</small>
    <small v-else-if="hint" class="hint">{{ hint }}</small>
  </div>
</template>

<style scoped>
.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}
.field.full {
  grid-column: 1 / -1;
}
label {
  font-size: 12px;
  color: var(--c-muted);
  font-weight: 600;
}
.error {
  color: var(--c-warn);
}
.hint {
  color: var(--c-muted);
}
</style>
