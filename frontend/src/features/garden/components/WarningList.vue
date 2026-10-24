<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PlantingWarningDto } from '@/shared/api'
import { useTexts } from '../useTexts'

/** Rule findings as a list; identical messages (both sides of a bad-neighbour pair) are shown once. */
const props = defineProps<{ warnings: PlantingWarningDto[]; emptyText: string; clickable?: boolean }>()
const emit = defineEmits<{ select: [bedId: string] }>()
const { warning } = useTexts()
const { t } = useI18n()

const items = computed(() => {
  const seen = new Set<string>()
  return props.warnings.map((w) => ({ w, text: warning(w) })).filter((x) => !seen.has(x.text) && seen.add(x.text))
})
</script>

<template>
  <div class="stack">
    <component
      :is="clickable ? 'button' : 'div'"
      v-for="{ w, text } in items"
      :key="`${w.plantingId}-${w.code}-${text}`"
      :type="clickable ? 'button' : undefined"
      class="item warning"
      :class="`level-${w.level}`"
      :aria-label="`${t(`checks.level.${w.level}`)}: ${text}`"
      @click="clickable && emit('select', w.bedId)"
    >
      <span aria-hidden="true">{{ w.level === 'warning' ? '⚠️' : 'ℹ️' }}</span>
      <span>{{ text }}</span>
    </component>
    <span v-if="!items.length" class="muted small">{{ emptyText }}</span>
  </div>
</template>

<style scoped>
.warning {
  font: inherit;
  text-align: left;
  width: 100%;
}
button.warning {
  cursor: pointer;
}
.level-warning {
  background: var(--c-warn-soft);
  border-color: #efc9b8;
}
.level-info {
  background: var(--c-info-soft);
  border-color: #c8d8ec;
}
</style>
