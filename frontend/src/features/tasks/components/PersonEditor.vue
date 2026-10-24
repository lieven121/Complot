<script setup lang="ts">
import InputText from 'primevue/inputtext'
import { useI18n } from 'vue-i18n'
import type { PersonDto, PersonInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import ColorInput from '@/shared/ui/ColorInput.vue'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { useDeletePerson, useSavePerson } from '../queries'

const { t } = useI18n()
const editor = useEditor({
  toInput: (p?: PersonDto): PersonInput => ({ name: p?.name ?? '', color: p?.color ?? '#c2833a' }),
  save: useSavePerson(),
  remove: useDeletePerson(),
  nameOf: (p) => p.name,
})
const { form, errors } = editor
defineExpose({ open: editor.open })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('tasks.people.new') : t('tasks.people.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="440px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField :label="t('common.name')" for="p-name" :error="errors.field('name')">
      <InputText id="p-name" v-model="form.name" autofocus :invalid="!!errors.field('name')" />
    </FormField>
    <FormField :label="t('tasks.people.color')" :error="errors.field('color')">
      <ColorInput v-model="form.color" />
    </FormField>
  </EditDialog>
</template>
