<script setup lang="ts">
import InputText from 'primevue/inputtext'
import { useI18n } from 'vue-i18n'
import type { CostCategoryDto, CostCategoryInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import ColorInput from '@/shared/ui/ColorInput.vue'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { useDeleteCostCategory, useSaveCostCategory } from '../queries'

const { t } = useI18n()
const editor = useEditor({
  toInput: (c?: CostCategoryDto): CostCategoryInput => ({ name: c?.name ?? '', color: c?.color ?? '#9a958a' }),
  save: useSaveCostCategory(),
  remove: useDeleteCostCategory(),
  nameOf: (c) => c.name,
})
const { form, errors } = editor
defineExpose({ open: editor.open })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('costs.category.new') : t('costs.category.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="440px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField :label="t('common.name')" for="cc-name" :error="errors.field('name')">
      <InputText id="cc-name" v-model="form.name" autofocus :invalid="!!errors.field('name')" />
    </FormField>
    <FormField :label="t('costs.category.color')" :error="errors.field('color')">
      <ColorInput v-model="form.color" />
    </FormField>
  </EditDialog>
</template>
