<script setup lang="ts">
import { computed } from 'vue'
import DatePicker from 'primevue/datepicker'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'
import Textarea from 'primevue/textarea'
import ToggleSwitch from 'primevue/toggleswitch'
import AutoComplete from 'primevue/autocomplete'
import { useI18n } from 'vue-i18n'
import type { ToolDto, ToolInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import { fromIsoDate, toIsoDate } from '@/shared/dates'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import InputNumber from '@/shared/ui/NumberInput.vue'
import { useAllBeds } from '@/features/garden/queries'
import { useDeleteTool, useSaveTool, useTools } from '../queries'

/** A tool or material: how many in which condition, where it is used, and when it needs care or replacing. */
const { t } = useI18n()
const beds = useAllBeds()
const tools = useTools()
const editor = useEditor({
  toInput: (x?: ToolDto): ToolInput => ({
    name: x?.name ?? '',
    kind: x?.kind ?? '',
    icon: x?.icon ?? '🧰',
    unit: x?.unit ?? t('tools.defaultUnit'),
    qtyGood: x?.qtyGood ?? 1,
    qtyRepair: x?.qtyRepair ?? 0,
    qtyWorn: x?.qtyWorn ?? 0,
    material: x?.material,
    isReused: x?.isReused ?? false,
    boughtOn: x?.boughtOn,
    unitPrice: x?.unitPrice,
    supplierName: x?.supplierName,
    lifespanYears: x?.lifespanYears,
    maintenanceIntervalMonths: x?.maintenanceIntervalMonths,
    maintenanceMinutes: x?.maintenanceMinutes ?? 30,
    bedIds: [...(x?.bedIds ?? [])],
    notes: x?.notes,
  }),
  save: useSaveTool(),
  remove: useDeleteTool(),
  nameOf: (x) => x.name,
})
const { form, errors } = editor
defineExpose({ open: editor.open })

const boughtOn = computed({ get: () => fromIsoDate(form.value.boughtOn), set: (d) => (form.value.boughtOn = toIsoDate(d)) })

// Kinds already in use, as suggestions (a free text keeps it simple to add a new group).
const kinds = computed(() => [...new Set((tools.data.value ?? []).map((x) => x.kind))].sort())
const kindSuggestions = computed(() => kinds.value.filter((k) => k.toLowerCase().includes(form.value.kind.toLowerCase())))
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('tools.new') : t('tools.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="720px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField :label="t('common.name')" for="t-name" :error="errors.field('name')">
      <InputText id="t-name" v-model="form.name" autofocus :invalid="!!errors.field('name')" />
    </FormField>
    <FormField :label="t('tools.kind')" for="t-kind" :error="errors.field('kind')" :hint="t('tools.kindHint')">
      <AutoComplete
        v-model="form.kind"
        input-id="t-kind"
        :suggestions="kindSuggestions"
        dropdown
        :invalid="!!errors.field('kind')"
        @complete="() => {}"
      />
    </FormField>
    <FormField :label="t('tools.icon')" for="t-icon" :error="errors.field('icon')">
      <InputText id="t-icon" v-model="form.icon" maxlength="16" />
    </FormField>
    <FormField :label="t('tools.unit')" for="t-unit" :error="errors.field('unit')" :hint="t('tools.unitHint')">
      <InputText id="t-unit" v-model="form.unit" />
    </FormField>

    <h3 class="full">{{ t('tools.condition') }}</h3>
    <FormField :label="t('tools.good')" for="t-good" :error="errors.field('qtyGood')">
      <InputNumber v-model="form.qtyGood" input-id="t-good" :min="0" show-buttons />
    </FormField>
    <FormField :label="t('tools.repair')" for="t-repair" :error="errors.field('qtyRepair')">
      <InputNumber v-model="form.qtyRepair" input-id="t-repair" :min="0" show-buttons />
    </FormField>
    <FormField :label="t('tools.worn')" for="t-worn" :error="errors.field('qtyWorn')">
      <InputNumber v-model="form.qtyWorn" input-id="t-worn" :min="0" show-buttons />
    </FormField>

    <h3 class="full">{{ t('tools.origin') }}</h3>
    <FormField :label="t('tools.material')" for="t-material" :error="errors.field('material')">
      <InputText id="t-material" v-model="form.material" />
    </FormField>
    <FormField :label="t('tools.reused')" :hint="t('tools.reusedHint')">
      <ToggleSwitch v-model="form.isReused" />
    </FormField>
    <FormField :label="t('tools.boughtOn')" for="t-bought">
      <DatePicker v-model="boughtOn" input-id="t-bought" show-icon show-button-bar date-format="dd/mm/yy" />
    </FormField>
    <FormField :label="t('tools.unitPrice')" for="t-price" :error="errors.field('unitPrice')">
      <InputNumber v-model="form.unitPrice" input-id="t-price" mode="currency" currency="EUR" :min="0" />
    </FormField>
    <FormField :label="t('tools.supplier')" for="t-supplier" :error="errors.field('supplierName')">
      <InputText id="t-supplier" v-model="form.supplierName" />
    </FormField>
    <FormField :label="t('tools.lifespan')" for="t-life" :error="errors.field('lifespanYears')">
      <InputNumber v-model="form.lifespanYears" input-id="t-life" :min="1" :suffix="` ${t('common.yearsUnit')}`" />
    </FormField>

    <h3 class="full">{{ t('tools.care') }}</h3>
    <FormField
      :label="t('tools.interval')"
      for="t-interval"
      :error="errors.field('maintenanceIntervalMonths')"
      :hint="t('tools.intervalHint')"
    >
      <InputNumber v-model="form.maintenanceIntervalMonths" input-id="t-interval" :min="1" :suffix="` ${t('tools.months')}`" />
    </FormField>
    <FormField :label="t('tools.maintenanceMinutes')" for="t-minutes" :error="errors.field('maintenanceMinutes')">
      <InputNumber v-model="form.maintenanceMinutes" input-id="t-minutes" :min="5" :step="5" :suffix="` ${t('common.minutesUnit')}`" />
    </FormField>
    <FormField :label="t('tools.beds')" for="t-beds" :hint="t('tools.bedsHint')" full>
      <MultiSelect
        v-model="form.bedIds"
        input-id="t-beds"
        :options="beds"
        option-label="label"
        option-value="id"
        filter
        display="chip"
        :placeholder="t('tools.inStorage')"
      />
    </FormField>
    <FormField :label="t('tools.notes')" for="t-notes" :error="errors.field('notes')" full>
      <Textarea id="t-notes" v-model="form.notes" rows="2" auto-resize />
    </FormField>
  </EditDialog>
</template>
