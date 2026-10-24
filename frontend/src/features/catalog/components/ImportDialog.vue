<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import Textarea from 'primevue/textarea'
import { useI18n } from 'vue-i18n'
import type { CatalogExchange, ErrorDetail, ImportReport, ImportRow, ImportRowStatus } from '@/shared/api'
import { useNotify } from '@/shared/composables/useNotify'
import { useServerErrors } from '@/shared/composables/useServerErrors'
import StatusTag from '@/shared/ui/StatusTag.vue'
import { EXAMPLE_ANSWER } from '../exchange'
import { useImportCatalog, usePreviewImport } from '../queries'

const visible = defineModel<boolean>('visible', { required: true })

const { t, te } = useI18n()
const notify = useNotify()
const { message } = useServerErrors()
const previewImport = usePreviewImport()
const importCatalog = useImportCatalog()

const text = ref('')
const parseError = ref<string>()
const report = ref<ImportReport>()
const parsed = ref<CatalogExchange>()

watch(visible, (open) => {
  if (open) reset()
})
watch(text, () => (report.value = undefined))

function reset() {
  text.value = ''
  parseError.value = undefined
  report.value = undefined
}

/** Accepts the AI answer as-is, also when wrapped in ```json fences. */
function parse(): CatalogExchange | undefined {
  try {
    const value = JSON.parse(text.value.trim().replace(/^```(?:json)?\s*|\s*```$/g, '')) as CatalogExchange | CatalogExchange['varieties']
    parseError.value = undefined
    return Array.isArray(value) ? { categories: [], varieties: value } : value
  } catch (error) {
    parseError.value = (error as Error).message
    return undefined
  }
}

async function check() {
  parsed.value = parse()
  if (!parsed.value) return
  try {
    report.value = await previewImport.mutateAsync(parsed.value)
  } catch (error) {
    notify.error(error)
  }
}

async function runImport() {
  if (!parsed.value) return
  try {
    const result = await importCatalog.mutateAsync(parsed.value)
    notify.success('catalog.exchange.imported', { n: importable(result) })
    visible.value = false
  } catch (error) {
    notify.error(error)
  }
}

const importable = (r: ImportReport) => [...r.categories, ...r.varieties].filter((row) => row.status !== 'error').length
const rows = computed(() => [
  ...(report.value?.categories ?? []).map((row) => ({ ...row, kind: t('catalog.category.one') })),
  ...(report.value?.varieties ?? []).map((row) => ({ ...row, kind: t('catalog.variety.one') })),
])

const tones: Record<ImportRowStatus, 'ok' | 'info' | 'danger'> = { new: 'ok', update: 'info', error: 'danger' }
const errorText = (row: ImportRow) =>
  Object.entries(row.errors ?? {})
    .map(([field, details]) => `${field}: ${details.map(message).join(' ')}`)
    .join(' · ')
const warningText = (w: ErrorDetail) =>
  te(`catalog.exchange.warnings.${w.code}`) ? t(`catalog.exchange.warnings.${w.code}`, w.params ?? {}) : w.code
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="t('catalog.exchange.importTitle')"
    :style="{ width: '900px' }"
    :breakpoints="{ '900px': '96vw' }"
  >
    <p class="muted">{{ t('catalog.exchange.importHelp') }}</p>
    <Textarea v-model="text" rows="10" class="json" placeholder='{ "varieties": [ … ] }' :aria-label="t('catalog.exchange.importTitle')" />
    <Message v-if="parseError" severity="error" size="small">{{ t('catalog.exchange.invalidJson', { error: parseError }) }}</Message>

    <DataTable v-if="report" :value="rows" size="small" class="report">
      <Column :header="t('catalog.exchange.status')">
        <template #body="{ data }"
          ><StatusTag :tone="tones[data.status as ImportRowStatus]">{{
            t(`catalog.exchange.statuses.${data.status}`)
          }}</StatusTag></template
        >
      </Column>
      <Column field="kind" :header="t('catalog.exchange.kind')" />
      <Column :header="t('common.name')">
        <template #body="{ data }"
          ><b>{{ data.name }}</b> <span class="muted small">{{ data.detail }}</span></template
        >
      </Column>
      <Column :header="t('catalog.exchange.remarks')">
        <template #body="{ data }">
          <div v-if="data.errors" class="error small">{{ errorText(data) }}</div>
          <div v-for="(w, i) in data.warnings" :key="i" class="muted small">⚠️ {{ warningText(w) }}</div>
        </template>
      </Column>
    </DataTable>

    <template #footer>
      <Button
        :label="t('catalog.exchange.loadExample')"
        severity="secondary"
        text
        @click="text = JSON.stringify(EXAMPLE_ANSWER, null, 2)"
      />
      <Button
        :label="t('catalog.exchange.check')"
        severity="secondary"
        outlined
        :loading="previewImport.isPending.value"
        :disabled="!text.trim()"
        @click="check"
      />
      <Button
        :label="t('catalog.exchange.importCount', { n: report ? importable(report) : 0 })"
        :disabled="!report || importable(report) === 0"
        :loading="importCatalog.isPending.value"
        @click="runImport"
      />
    </template>
  </Dialog>
</template>

<style scoped>
.json {
  width: 100%;
  font-family: ui-monospace, Consolas, monospace;
  font-size: 12px;
}
.report {
  margin-top: 12px;
}
.error {
  color: var(--c-warn);
}
</style>
