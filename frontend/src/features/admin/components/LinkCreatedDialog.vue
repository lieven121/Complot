<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import { useI18n } from 'vue-i18n'
import type { AccessLinkDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import { linkUrl } from '../queries'

/** Shows a new link once: only its hash is stored, so it cannot be shown again later. */
const props = defineProps<{ token: string; link: AccessLinkDto }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const { date } = useFormat()
const notify = useNotify()
const visible = ref(true)
const url = computed(() => linkUrl(props.token))

async function copy() {
  await navigator.clipboard.writeText(url.value)
  notify.success('common.copied')
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="t('admin.link.createdTitle', { label: link.label })"
    :style="{ width: '560px' }"
    :breakpoints="{ '600px': '96vw' }"
    @after-hide="emit('close')"
  >
    <p>{{ t('admin.link.createdHelp', { date: date(link.validUntil) }) }}</p>
    <div class="url">
      <InputText :model-value="url" readonly aria-label="URL" @focus="($event.target as HTMLInputElement).select()" />
      <Button :label="t('common.copy')" icon="pi pi-copy" @click="copy" />
    </div>
    <p class="muted small">{{ t('admin.link.onlyOnce') }}</p>
    <template #footer>
      <Button :label="t('common.close')" severity="secondary" outlined @click="visible = false" />
    </template>
  </Dialog>
</template>

<style scoped>
.url {
  display: flex;
  gap: 6px;
}
.url :deep(input) {
  flex: 1;
  min-width: 0;
  font-family: ui-monospace, monospace;
  font-size: 12px;
}
</style>
