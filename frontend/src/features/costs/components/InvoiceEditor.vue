<script setup lang="ts">
import { computed, ref } from 'vue'
import AutoComplete from 'primevue/autocomplete'
import Button from 'primevue/button'
import DatePicker from 'primevue/datepicker'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import { useI18n } from 'vue-i18n'
import type { InvoiceDto, InvoiceInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import { fromIsoDate, todayIso, toIsoDate } from '@/shared/dates'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import InputNumber from '@/shared/ui/NumberInput.vue'
import { useSites } from '@/features/garden/queries'
import { aiExample, allocation, assignRest, parseAiInvoice } from '../costs'
import { attachmentUrl, useCostCategories, useDeleteAttachment, useDeleteInvoice, useInvoiceSuppliers, useSaveInvoice } from '../queries'

/** An invoice split over cost categories, with its PDF or photo; an AI assistant can fill it in from that file. */
const { t } = useI18n()
const { currency } = useFormat()
const notify = useNotify()
const categories = useCostCategories()
const suppliers = useInvoiceSuppliers()
const sites = useSites()
const saveInvoice = useSaveInvoice()
const deleteAttachment = useDeleteAttachment()
const file = ref<File>()

const editor = useEditor({
  toInput: (i?: InvoiceDto): InvoiceInput => ({
    date: i?.date ?? todayIso(),
    supplier: i?.supplier ?? '',
    number: i?.number,
    total: i?.total ?? 0,
    siteId: i?.siteId,
    notes: i?.notes,
    lines: i?.lines.map((l) => ({ ...l })) ?? [{ description: '', categoryId: categories.data.value?.[0]?.id ?? '', amount: 0 }],
  }),
  save: {
    mutateAsync: (v: { id?: string; input: InvoiceInput }) => saveInvoice.mutateAsync({ ...v, file: file.value }),
    isPending: saveInvoice.isPending,
  },
  remove: useDeleteInvoice(),
  nameOf: (i) => `${i.supplier} ${i.number ?? ''}`.trim(),
})
const { form, errors } = editor

const open = (invoice?: InvoiceDto) => {
  file.value = undefined
  aiText.value = ''
  editor.open(invoice)
}
defineExpose({ open })

const date = computed({ get: () => fromIsoDate(form.value.date), set: (d) => (form.value.date = toIsoDate(d) ?? todayIso()) })
const status = computed(() => allocation(form.value.lines, form.value.total))
const supplierSuggestions = ref<string[]>([])
const searchSupplier = (query: string) =>
  (supplierSuggestions.value = (suppliers.data.value ?? []).filter((s) => s.toLowerCase().includes(query.toLowerCase())))

const addLine = () => form.value.lines.push({ description: '', categoryId: categories.data.value?.[0]?.id ?? '', amount: 0 })
const fillRest = () =>
  (form.value.lines = assignRest(form.value.lines, form.value.total, categories.data.value?.at(-1)?.id ?? '', t('costs.rest')))

// AI assistant: copy the prompt (with this garden's categories), paste the JSON answer back.
const aiText = ref('')
const prompt = computed(() =>
  t('costs.ai.prompt', {
    categories: (categories.data.value ?? []).map((c) => `"${c.name}"`).join(', '),
    example: aiExample(categories.data.value ?? []),
  }),
)
async function copyPrompt() {
  await navigator.clipboard.writeText(prompt.value)
  notify.success('common.copied')
}
function fillFromAi() {
  try {
    const { input, warnings } = parseAiInvoice(aiText.value, categories.data.value ?? [])
    form.value = { ...form.value, ...Object.fromEntries(Object.entries(input).filter(([, v]) => v !== undefined)) }
    notify.success('costs.ai.filled')
    warnings.forEach((w) => notify.warn(w.key, w.params))
  } catch {
    notify.warn('costs.ai.invalid')
  }
}

async function removeAttachment(id: string) {
  try {
    await deleteAttachment.mutateAsync(id)
    if (editor.editing.value) editor.editing.value = { ...editor.editing.value, hasAttachment: false, attachmentName: undefined }
  } catch (error) {
    notify.error(error)
  }
}
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('costs.invoice.new') : t('costs.invoice.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="760px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField
      :label="t('costs.invoice.attachment')"
      for="i-file"
      :hint="t('costs.invoice.attachmentHint')"
      :error="errors.field('file')"
      full
    >
      <div class="row">
        <template v-if="editor.editing.value?.hasAttachment && !file">
          <a :href="attachmentUrl(editor.editing.value.id)" target="_blank" rel="noopener"
            >📎 {{ editor.editing.value.attachmentName ?? t('costs.invoice.open') }}</a
          >
          <Button :label="t('common.remove')" text size="small" severity="secondary" @click="removeAttachment(editor.editing.value.id)" />
        </template>
        <input
          id="i-file"
          type="file"
          accept="application/pdf,image/jpeg,image/png,image/webp,image/heic"
          @change="file = ($event.target as HTMLInputElement).files?.[0]"
        />
      </div>
    </FormField>

    <details class="full ai">
      <summary>✨ {{ t('costs.ai.title') }}</summary>
      <p class="muted small">{{ t('costs.ai.help') }}</p>
      <Button :label="t('costs.ai.copy')" icon="pi pi-copy" size="small" severity="secondary" outlined @click="copyPrompt" />
      <Textarea v-model="aiText" rows="4" class="code" :placeholder="t('costs.ai.paste')" :aria-label="t('costs.ai.paste')" />
      <Button :label="t('costs.ai.fill')" icon="pi pi-sparkles" size="small" :disabled="!aiText.trim()" @click="fillFromAi" />
    </details>

    <FormField :label="t('costs.invoice.date')" for="i-date" :error="errors.field('date')">
      <DatePicker v-model="date" input-id="i-date" show-icon date-format="dd/mm/yy" />
    </FormField>
    <FormField :label="t('costs.invoice.supplier')" for="i-supplier" :error="errors.field('supplier')">
      <AutoComplete
        v-model="form.supplier"
        input-id="i-supplier"
        :suggestions="supplierSuggestions"
        :invalid="!!errors.field('supplier')"
        @complete="searchSupplier($event.query)"
      />
    </FormField>
    <FormField :label="t('costs.invoice.number')" for="i-number" :error="errors.field('number')">
      <InputText id="i-number" v-model="form.number" />
    </FormField>
    <FormField :label="t('costs.invoice.total')" for="i-total" :error="errors.field('total')">
      <InputNumber v-model="form.total" input-id="i-total" mode="currency" currency="EUR" :min="0" :invalid="!!errors.field('total')" />
    </FormField>
    <FormField v-if="(sites.data.value?.length ?? 0) > 1" :label="t('costs.invoice.site')" for="i-site" :hint="t('costs.invoice.siteHint')">
      <Select
        v-model="form.siteId"
        input-id="i-site"
        :options="sites.data.value ?? []"
        option-label="name"
        option-value="id"
        show-clear
        :placeholder="t('costs.invoice.allSites')"
      />
    </FormField>

    <h3 class="full">{{ t('costs.invoice.split') }}</h3>
    <div class="full stack">
      <div v-for="(line, i) in form.lines" :key="i" class="line">
        <InputText
          v-model="line.description"
          :placeholder="t('costs.invoice.description')"
          :invalid="!!errors.field(`lines[${i}].description`)"
        />
        <Select
          v-model="line.categoryId"
          :options="categories.data.value ?? []"
          option-label="name"
          option-value="id"
          :aria-label="t('costs.category.one')"
          :invalid="!!errors.field(`lines[${i}].categoryId`)"
        >
          <template #option="{ option }"><span class="dot" :style="{ background: option.color }" /> {{ option.name }}</template>
        </Select>
        <InputNumber v-model="line.amount" mode="currency" currency="EUR" :aria-label="t('costs.invoice.amount')" />
        <Button
          icon="pi pi-times"
          text
          severity="secondary"
          :aria-label="t('common.remove')"
          :disabled="form.lines.length < 2"
          @click="form.lines.splice(i, 1)"
        />
      </div>
      <div class="row">
        <Button :label="t('costs.invoice.addLine')" icon="pi pi-plus" text size="small" @click="addLine" />
        <Button :label="t('costs.invoice.assignRest')" text size="small" :disabled="status.difference === 0" @click="fillRest" />
        <span class="alloc small" :class="{ ok: status.balanced }">
          {{ t('costs.invoice.allocated', { allocated: currency(status.allocated), total: currency(form.total || 0) }) }}
          <template v-if="status.balanced"> ✅</template>
          <template v-else-if="status.difference > 0">
            · {{ t('costs.invoice.remaining', { amount: currency(status.difference) }) }}</template
          >
          <template v-else-if="status.difference < 0"> · {{ t('costs.invoice.over', { amount: currency(-status.difference) }) }}</template>
        </span>
      </div>
      <small v-if="errors.field('lines')" class="error" role="alert">{{ errors.field('lines') }}</small>
    </div>

    <FormField :label="t('costs.invoice.notes')" for="i-notes" full>
      <Textarea id="i-notes" v-model="form.notes" rows="2" auto-resize />
    </FormField>
  </EditDialog>
</template>

<style scoped>
.ai {
  border: 1px solid var(--c-line);
  border-radius: 9px;
  padding: 8px 12px;
  background: var(--c-panel-2);
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.ai summary {
  cursor: pointer;
  font-weight: 600;
}
.ai[open] {
  display: flex;
}
.ai :deep(.code) {
  font-family: ui-monospace, monospace;
  font-size: 12px;
  width: 100%;
  margin: 6px 0;
}
.line {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(0, 1.4fr) minmax(0, 1fr) auto;
  gap: 6px;
  align-items: center;
}
.line :deep(.p-inputnumber),
.line :deep(.p-select) {
  min-width: 0;
}
.line :deep(.p-inputnumber input) {
  width: 100%;
  min-width: 0;
}
.dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  margin-right: 4px;
}
.alloc {
  margin-left: auto;
  color: var(--c-warn);
}
.alloc.ok {
  color: var(--c-accent);
}
.error {
  color: var(--c-warn);
}
@media (max-width: 640px) {
  .line {
    grid-template-columns: 1fr 1fr auto;
  }
  .line > :first-child {
    grid-column: 1 / -1;
  }
}
</style>
