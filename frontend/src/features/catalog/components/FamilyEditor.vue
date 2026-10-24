<script setup lang="ts">
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import { useI18n } from 'vue-i18n'
import type { FamilyDto, FamilyInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { useDeleteFamily, useSaveFamily } from '../queries'

const { t } = useI18n()
const editor = useEditor({
  toInput: (f?: FamilyDto): FamilyInput => ({
    latinName: f?.latinName ?? '',
    nameNl: f?.nameNl ?? '',
    nameEn: f?.nameEn ?? '',
    rotationRestYears: f?.rotationRestYears ?? 3,
  }),
  save: useSaveFamily(),
  remove: useDeleteFamily(),
  nameOf: (f) => f.latinName,
})
const { form, errors } = editor
defineExpose({ open: editor.open })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('catalog.family.new') : t('catalog.family.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="520px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField :label="t('catalog.family.latinName')" for="f-latin" :error="errors.field('latinName')" full>
      <InputText id="f-latin" v-model="form.latinName" autofocus :invalid="!!errors.field('latinName')" />
    </FormField>
    <FormField :label="t('catalog.family.nameNl')" for="f-nl" :error="errors.field('nameNl')">
      <InputText id="f-nl" v-model="form.nameNl" :invalid="!!errors.field('nameNl')" />
    </FormField>
    <FormField :label="t('catalog.family.nameEn')" for="f-en" :error="errors.field('nameEn')">
      <InputText id="f-en" v-model="form.nameEn" :invalid="!!errors.field('nameEn')" />
    </FormField>
    <FormField
      :label="t('catalog.family.rotationRestYears')"
      for="f-rest"
      :error="errors.field('rotationRestYears')"
      :hint="t('catalog.family.rotationHint')"
      full
    >
      <InputNumber
        v-model="form.rotationRestYears"
        input-id="f-rest"
        :min="0"
        :max="10"
        show-buttons
        :suffix="` ${t('common.yearsUnit')}`"
      />
    </FormField>
  </EditDialog>
</template>
