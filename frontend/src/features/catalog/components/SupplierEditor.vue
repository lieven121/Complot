<script setup lang="ts">
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import ToggleSwitch from 'primevue/toggleswitch'
import { useI18n } from 'vue-i18n'
import type { SupplierDto, SupplierInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { useDeleteSupplier, useSaveSupplier } from '../queries'

const { t } = useI18n()
const editor = useEditor({
  toInput: (s?: SupplierDto): SupplierInput => ({
    name: s?.name ?? '',
    url: s?.url,
    isOwnSource: s?.isOwnSource ?? false,
    notes: s?.notes,
  }),
  save: useSaveSupplier(),
  remove: useDeleteSupplier(),
  nameOf: (s) => s.name,
})
const { form, errors } = editor
defineExpose({ open: editor.open })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('catalog.supplier.new') : t('catalog.supplier.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="520px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField :label="t('catalog.supplier.name')" for="s-name" :error="errors.field('name')" full>
      <InputText id="s-name" v-model="form.name" autofocus :invalid="!!errors.field('name')" />
    </FormField>
    <FormField :label="t('catalog.supplier.url')" for="s-url" :error="errors.field('url')" full>
      <InputText id="s-url" v-model="form.url" type="url" placeholder="https://" :invalid="!!errors.field('url')" />
    </FormField>
    <FormField :label="t('catalog.supplier.isOwnSource')" :hint="t('catalog.supplier.isOwnSourceHint')" full>
      <ToggleSwitch v-model="form.isOwnSource" />
    </FormField>
    <FormField :label="t('catalog.supplier.notes')" for="s-notes" :error="errors.field('notes')" full>
      <Textarea id="s-notes" v-model="form.notes" rows="3" auto-resize />
    </FormField>
  </EditDialog>
</template>
