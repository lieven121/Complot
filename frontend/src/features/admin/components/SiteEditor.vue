<script setup lang="ts">
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import { useI18n } from 'vue-i18n'
import type { SiteDto, SiteInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { useSaveSite } from '@/features/garden/queries'

const { t } = useI18n()
const editor = useEditor({
  toInput: (s?: SiteDto): SiteInput => ({ name: s?.name ?? '', notes: s?.notes }),
  save: useSaveSite(),
})
const { form, errors } = editor
defineExpose({ open: editor.open })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('admin.site.new') : t('admin.site.edit')"
    :saving="editor.saving.value"
    :readonly="false"
    width="480px"
    @save="editor.submit"
  >
    <FormField :label="t('common.name')" for="site-name" :error="errors.field('name')" full>
      <InputText id="site-name" v-model="form.name" autofocus :invalid="!!errors.field('name')" />
    </FormField>
    <FormField :label="t('admin.site.notes')" for="site-notes" :error="errors.field('notes')" full>
      <Textarea id="site-notes" v-model="form.notes" rows="3" auto-resize />
    </FormField>
  </EditDialog>
</template>
