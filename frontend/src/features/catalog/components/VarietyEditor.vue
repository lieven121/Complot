<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import InputNumber from '@/shared/ui/NumberInput.vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import Textarea from 'primevue/textarea'
import { useI18n } from 'vue-i18n'
import type { Life, SunExposure, VarietyDto } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import { useFormat } from '@/shared/composables/useFormat'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import MonthWindowInput from '@/shared/ui/MonthWindowInput.vue'
import { useTools } from '@/features/tools/queries'
import { useCatalogLookups, useDeleteVariety, useSaveVariety } from '../queries'
import { newVarietyInput, varietyInputFrom } from '../varietyForm'

const { t } = useI18n()
const { monthLong } = useFormat()
const { categories, families, suppliers, familyById, categoryById } = useCatalogLookups()
const tools = useTools()

const editor = useEditor({
  toInput: (v?: VarietyDto) => (v ? varietyInputFrom(v) : newVarietyInput()),
  save: useSaveVariety(),
  remove: useDeleteVariety(),
  nameOf: (v) => [v.name, v.cultivar].filter(Boolean).join(' — '),
})
const { form, errors } = editor

/** Opens the editor; `categoryId` preselects the category for a new variety. */
function open(variety?: VarietyDto, categoryId?: string) {
  editor.open(variety)
  if (!variety && categoryId) form.value.categoryId = categoryId
}
defineExpose({ open })

const lifeOptions = computed(() => (['annual', 'biennial', 'perennial'] as Life[]).map((value) => ({ value, label: t(`life.${value}`) })))
const sunOptions = computed(() => (['full', 'half', 'shade'] as SunExposure[]).map((value) => ({ value, label: t(`sun.${value}`) })))
const monthOptions = computed(() => Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: monthLong(i + 1) })))

const defaultFamily = computed(() => {
  const category = categoryById.value.get(form.value.categoryId)
  return category?.defaultFamilyId ? familyById.value.get(category.defaultFamilyId) : undefined
})
const familyPlaceholder = computed(() =>
  defaultFamily.value ? t('catalog.variety.familyFromCategory', { family: defaultFamily.value.latinName }) : t('common.none'),
)
const unlimitedStay = computed({
  get: () => form.value.maxYearsInBed == null,
  set: (on: boolean) => (form.value.maxYearsInBed = on ? undefined : 1),
})

const addAction = () => form.value.actions.push({ title: '', month: 2, estimatedMinutes: 30 })
const addSupplier = () => form.value.suppliers.push({ supplierId: '', price: 0, unit: undefined, url: undefined })
const toolNeeds = computed(() => (form.value.toolNeeds ??= []))
const addToolNeed = () => toolNeeds.value.push({ toolId: '', per: 'bed', quantity: 1 })
const perOptions = computed(() => (['plant', 'bed'] as const).map((value) => ({ value, label: t(`tools.per.${value}`) })))
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('catalog.variety.new') : t('catalog.variety.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="820px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <h3 class="full">{{ t('catalog.variety.sectionBasics') }}</h3>
    <FormField :label="t('catalog.variety.name')" for="v-name" :error="errors.field('name')">
      <InputText id="v-name" v-model="form.name" :invalid="!!errors.field('name')" autofocus />
    </FormField>
    <FormField :label="t('catalog.variety.cultivar')" for="v-cultivar" :error="errors.field('cultivar')">
      <InputText id="v-cultivar" v-model="form.cultivar" />
    </FormField>
    <FormField :label="t('catalog.variety.latinName')" for="v-latin" :error="errors.field('latinName')">
      <InputText id="v-latin" v-model="form.latinName" />
    </FormField>
    <FormField :label="t('catalog.category.one')" for="v-category" :error="errors.field('categoryId')">
      <Select
        v-model="form.categoryId"
        input-id="v-category"
        :options="categories.data.value ?? []"
        :option-label="(c) => `${c.icon} ${c.name}`"
        option-value="id"
        filter
        :invalid="!!errors.field('categoryId')"
      />
    </FormField>
    <FormField
      :label="t('catalog.variety.family')"
      for="v-family"
      :error="errors.field('familyId')"
      :hint="t('catalog.variety.familyHint')"
    >
      <Select
        v-model="form.familyId"
        input-id="v-family"
        :options="families.data.value ?? []"
        option-label="latinName"
        option-value="id"
        :placeholder="familyPlaceholder"
        show-clear
        filter
      />
    </FormField>
    <FormField :label="t('catalog.variety.life')" :error="errors.field('life')">
      <SelectButton v-model="form.life" :options="lifeOptions" option-label="label" option-value="value" :allow-empty="false" />
    </FormField>

    <h3 class="full">{{ t('catalog.variety.sectionGrowing') }}</h3>
    <FormField :label="t('catalog.variety.sowDepthCm')" for="v-depth" :error="errors.field('sowDepthCm')">
      <InputNumber v-model="form.sowDepthCm" input-id="v-depth" :min-fraction-digits="0" :max-fraction-digits="1" suffix=" cm" />
    </FormField>
    <FormField :label="t('catalog.variety.spacingInRowCm')" for="v-spacing" :error="errors.field('spacingInRowCm')">
      <InputNumber v-model="form.spacingInRowCm" input-id="v-spacing" suffix=" cm" />
    </FormField>
    <FormField :label="t('catalog.variety.spacingBetweenRowsCm')" for="v-row" :error="errors.field('spacingBetweenRowsCm')">
      <InputNumber v-model="form.spacingBetweenRowsCm" input-id="v-row" suffix=" cm" />
    </FormField>
    <FormField :label="t('catalog.variety.heightCm')" for="v-height" :error="errors.field('heightCm')">
      <InputNumber v-model="form.heightCm" input-id="v-height" suffix=" cm" />
    </FormField>
    <FormField :label="t('catalog.variety.sun')" :error="errors.field('sun')" full>
      <SelectButton v-model="form.sun" :options="sunOptions" option-label="label" option-value="value" multiple />
    </FormField>

    <h3 class="full">{{ t('catalog.variety.sectionCalendar') }}</h3>
    <FormField :label="t('catalog.variety.sowIndoor')" :error="errors.field('sowIndoor')">
      <MonthWindowInput v-model="form.sowIndoor" optional />
    </FormField>
    <FormField v-if="form.sowIndoor" :label="t('catalog.variety.weeksIndoor')" for="v-weeks" :error="errors.field('weeksIndoor')">
      <InputNumber v-model="form.weeksIndoor" input-id="v-weeks" :suffix="` ${t('common.weeksUnit')}`" />
    </FormField>
    <FormField :label="t('catalog.variety.sowOutdoor')" :error="errors.field('sowOutdoor')">
      <MonthWindowInput v-model="form.sowOutdoor" optional />
    </FormField>
    <FormField :label="t('catalog.variety.plantOut')" :error="errors.field('plantOut')">
      <MonthWindowInput v-model="form.plantOut" optional />
    </FormField>
    <FormField :label="t('catalog.variety.harvest')" :error="errors.field('harvest')">
      <MonthWindowInput v-model="form.harvest" />
    </FormField>
    <FormField :label="t('catalog.variety.germinationDays')" :error="errors.field('germination')">
      <div class="row nowrap">
        <InputNumber
          :model-value="form.germination?.min"
          :placeholder="t('common.min')"
          :aria-label="t('common.min')"
          @update:model-value="(v) => (form.germination = v == null ? undefined : { min: v, max: form.germination?.max ?? v })"
        />
        <span class="muted">–</span>
        <InputNumber
          :model-value="form.germination?.max"
          :placeholder="t('common.max')"
          :aria-label="t('common.max')"
          :disabled="!form.germination"
          @update:model-value="(v) => form.germination && v != null && (form.germination = { ...form.germination, max: v })"
        />
      </div>
    </FormField>
    <FormField :label="t('catalog.variety.daysToFirstHarvest')" for="v-days" :error="errors.field('daysToFirstHarvest')">
      <InputNumber v-model="form.daysToFirstHarvest" input-id="v-days" :suffix="` ${t('common.daysUnit')}`" />
    </FormField>
    <FormField :label="t('catalog.variety.harvestWeeks')" for="v-hw" :error="errors.field('harvestWeeks')">
      <InputNumber v-model="form.harvestWeeks" input-id="v-hw" :suffix="` ${t('common.weeksUnit')}`" />
    </FormField>
    <FormField :label="t('catalog.variety.maxYearsInBed')" :error="errors.field('maxYearsInBed')" :hint="t('catalog.variety.maxYearsHint')">
      <div class="row nowrap">
        <InputNumber
          v-model="form.maxYearsInBed"
          :disabled="unlimitedStay"
          :suffix="` ${t('common.yearsUnit')}`"
          :aria-label="t('catalog.variety.maxYearsInBed')"
        />
        <label class="row nowrap small"><input v-model="unlimitedStay" type="checkbox" /> {{ t('catalog.variety.unlimited') }}</label>
      </div>
    </FormField>

    <h3 class="full">{{ t('catalog.variety.actions') }}</h3>
    <div class="full stack">
      <div v-for="(action, i) in form.actions" :key="i" class="line">
        <InputText
          v-model="action.title"
          :placeholder="t('catalog.variety.actionTitle')"
          :invalid="!!errors.field(`actions[${i}].title`)"
        />
        <Select v-model="action.month" :options="monthOptions" option-label="label" option-value="value" :aria-label="t('common.month')" />
        <InputNumber
          v-model="action.estimatedMinutes"
          :suffix="` ${t('common.minutesUnit')}`"
          :aria-label="t('catalog.variety.estimatedMinutes')"
        />
        <Button icon="pi pi-times" text severity="secondary" :aria-label="t('common.remove')" @click="form.actions.splice(i, 1)" />
      </div>
      <div><Button :label="t('catalog.variety.addAction')" icon="pi pi-plus" text size="small" @click="addAction" /></div>
    </div>

    <h3 class="full">{{ t('catalog.suppliers') }}</h3>
    <div class="full stack">
      <small v-if="errors.field('suppliers')" class="error">{{ errors.field('suppliers') }}</small>
      <div v-for="(supplier, i) in form.suppliers" :key="i" class="line">
        <Select
          v-model="supplier.supplierId"
          :options="suppliers.data.value ?? []"
          option-label="name"
          option-value="id"
          filter
          :placeholder="t('catalog.supplier.choose')"
          :invalid="!!errors.field(`suppliers[${i}]`)"
        />
        <InputNumber
          v-model="supplier.price"
          mode="currency"
          currency="EUR"
          :locale="$i18n.locale"
          :aria-label="t('catalog.supplier.price')"
        />
        <InputText v-model="supplier.unit" :placeholder="t('catalog.supplier.unit')" />
        <Button icon="pi pi-times" text severity="secondary" :aria-label="t('common.remove')" @click="form.suppliers.splice(i, 1)" />
      </div>
      <div><Button :label="t('catalog.supplier.add')" icon="pi pi-plus" text size="small" @click="addSupplier" /></div>
    </div>

    <h3 class="full">{{ t('tools.needs') }}</h3>
    <div class="full stack">
      <small class="muted">{{ t('tools.needsHelp') }}</small>
      <small v-if="errors.field('toolNeeds')" class="error">{{ errors.field('toolNeeds') }}</small>
      <div v-for="(need, i) in toolNeeds" :key="i" class="line">
        <Select
          v-model="need.toolId"
          :options="tools.data.value ?? []"
          option-label="name"
          option-value="id"
          filter
          :placeholder="t('tools.choose')"
          :invalid="!!errors.field(`toolNeeds[${i}].toolId`)"
        />
        <InputNumber v-model="need.quantity" :min="0" :max-fraction-digits="2" :aria-label="t('tools.quantity')" />
        <SelectButton
          v-model="need.per"
          :options="perOptions"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          size="small"
        />
        <Button icon="pi pi-times" text severity="secondary" :aria-label="t('common.remove')" @click="toolNeeds.splice(i, 1)" />
      </div>
      <div><Button :label="t('tools.addNeed')" icon="pi pi-plus" text size="small" @click="addToolNeed" /></div>
    </div>

    <FormField :label="t('catalog.variety.notes')" for="v-notes" :error="errors.field('notes')" full>
      <Textarea id="v-notes" v-model="form.notes" rows="3" auto-resize />
    </FormField>
  </EditDialog>
</template>

<style scoped>
.nowrap {
  flex-wrap: nowrap;
}
.line {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(0, 1fr) minmax(0, 1fr) auto;
  gap: 6px;
  align-items: center;
}
.error {
  color: var(--c-warn);
}
.row :deep(.p-inputnumber) {
  min-width: 0;
  flex: 1;
}
@media (max-width: 600px) {
  .line {
    grid-template-columns: 1fr 1fr auto;
  }
  .line > :first-child {
    grid-column: 1 / -1;
  }
}
</style>
