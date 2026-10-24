<script setup lang="ts">
import { computed } from 'vue'
import DatePicker from 'primevue/datepicker'
import InputNumber from '@/shared/ui/NumberInput.vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import { useI18n } from 'vue-i18n'
import type { InventoryLotDto, InventoryLotInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import { fromIsoDate, todayIso, toIsoDate } from '@/shared/dates'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { useCatalogLookups, useDeleteLot, useSaveLot } from '../queries'

const { t } = useI18n()
const { varieties, suppliers, varietyById } = useCatalogLookups()

const editor = useEditor({
  toInput: (l?: InventoryLotDto): InventoryLotInput => ({
    varietyId: l?.varietyId ?? '',
    supplierId: l?.supplierId ?? '',
    quantity: l?.quantity ?? 1,
    unit: l?.unit ?? 'g',
    boughtOn: l?.boughtOn ?? todayIso(),
    bestBefore: l?.bestBefore,
    notes: l?.notes,
  }),
  save: useSaveLot(),
  remove: useDeleteLot(),
  nameOf: (l) => varietyById.value.get(l.varietyId)?.name ?? '',
})
const { form, errors } = editor

function open(lot?: InventoryLotDto, varietyId?: string) {
  editor.open(lot)
  if (!lot && varietyId) form.value.varietyId = varietyId
}
defineExpose({ open })

const varietyLabel = (v: { name: string; cultivar?: string }) => [v.name, v.cultivar].filter(Boolean).join(' — ')
const boughtOn = computed({ get: () => fromIsoDate(form.value.boughtOn), set: (d) => (form.value.boughtOn = toIsoDate(d) ?? todayIso()) })
const bestBefore = computed({ get: () => fromIsoDate(form.value.bestBefore), set: (d) => (form.value.bestBefore = toIsoDate(d)) })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('catalog.lot.new') : t('catalog.lot.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="560px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField :label="t('catalog.variety.one')" for="l-variety" :error="errors.field('varietyId')" full>
      <Select
        v-model="form.varietyId"
        input-id="l-variety"
        :options="varieties.data.value ?? []"
        :option-label="varietyLabel"
        option-value="id"
        filter
        :invalid="!!errors.field('varietyId')"
      />
    </FormField>
    <FormField :label="t('catalog.lot.source')" for="l-supplier" :error="errors.field('supplierId')" full>
      <Select
        v-model="form.supplierId"
        input-id="l-supplier"
        :options="suppliers.data.value ?? []"
        option-label="name"
        option-value="id"
        filter
        :invalid="!!errors.field('supplierId')"
      />
    </FormField>
    <FormField :label="t('catalog.lot.quantity')" for="l-qty" :error="errors.field('quantity')">
      <InputNumber v-model="form.quantity" input-id="l-qty" :min-fraction-digits="0" :max-fraction-digits="2" />
    </FormField>
    <FormField :label="t('catalog.lot.unit')" for="l-unit" :error="errors.field('unit')" :hint="t('catalog.lot.unitHint')">
      <InputText id="l-unit" v-model="form.unit" :invalid="!!errors.field('unit')" />
    </FormField>
    <FormField :label="t('catalog.lot.boughtOn')" for="l-bought" :error="errors.field('boughtOn')">
      <DatePicker v-model="boughtOn" input-id="l-bought" show-icon date-format="dd/mm/yy" />
    </FormField>
    <FormField :label="t('catalog.lot.bestBefore')" for="l-bb" :error="errors.field('bestBefore')">
      <DatePicker v-model="bestBefore" input-id="l-bb" show-icon show-button-bar date-format="dd/mm/yy" />
    </FormField>
    <FormField :label="t('catalog.lot.notes')" for="l-notes" full>
      <InputText id="l-notes" v-model="form.notes" />
    </FormField>
  </EditDialog>
</template>
