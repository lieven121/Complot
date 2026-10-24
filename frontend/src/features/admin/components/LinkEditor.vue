<script setup lang="ts">
import { computed } from 'vue'
import DatePicker from 'primevue/datepicker'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import { useI18n } from 'vue-i18n'
import type { AccessLinkDto, AccessLinkInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import { addDays, fromIsoDate, todayIso, toIsoDate } from '@/shared/dates'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { usePeople } from '@/features/tasks/queries'
import { useDeleteLink, useSaveLink } from '../queries'
import SiteAccessPicker from './SiteAccessPicker.vue'

/** An access link for volunteers: what it opens, for whom, and until when. */
const emit = defineEmits<{ created: [token: string, link: AccessLinkDto] }>()

const { t } = useI18n()
const people = usePeople()
const save = useSaveLink()
const editor = useEditor({
  toInput: (l?: AccessLinkDto): AccessLinkInput => ({
    label: l?.label ?? '',
    personId: l?.personId,
    validUntil: l?.validUntil ?? addDays(todayIso(), 30),
    sites: (l?.sites ?? []).map((s) => ({ ...s })),
  }),
  save: {
    // A new link returns its secret once: hand it to the page to show.
    mutateAsync: async (v: { id?: string; input: AccessLinkInput }) => {
      const result = await save.mutateAsync(v)
      if ('token' in result) emit('created', result.token, result.link)
    },
    isPending: save.isPending,
  },
  remove: useDeleteLink(),
  nameOf: (l) => l.label,
})
const { form, errors } = editor
const validUntil = computed({
  get: () => fromIsoDate(form.value.validUntil),
  set: (d) => (form.value.validUntil = toIsoDate(d) ?? todayIso()),
})
const today = fromIsoDate(todayIso())!
const lastDay = fromIsoDate(addDays(todayIso(), 366))!
defineExpose({ open: editor.open })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('admin.link.new') : t('admin.link.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    :readonly="false"
    width="620px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField :label="t('admin.link.label')" for="l-label" :error="errors.field('label')" :hint="t('admin.link.labelHint')" full>
      <InputText id="l-label" v-model="form.label" autofocus :invalid="!!errors.field('label')" />
    </FormField>
    <FormField :label="t('admin.link.validUntil')" for="l-until" :error="errors.field('validUntil')">
      <DatePicker v-model="validUntil" input-id="l-until" show-icon date-format="dd/mm/yy" :min-date="today" :max-date="lastDay" />
    </FormField>
    <FormField :label="t('admin.link.person')" for="l-person" :hint="t('admin.link.personHint')">
      <Select
        v-model="form.personId"
        input-id="l-person"
        :options="people.data.value ?? []"
        option-label="name"
        option-value="id"
        show-clear
        :placeholder="t('common.none')"
      />
    </FormField>
    <div class="full">
      <h3>{{ t('admin.user.sites') }}</h3>
      <small v-if="errors.field('sites')" class="error" role="alert">{{ errors.field('sites') }}</small>
      <SiteAccessPicker v-model="form.sites" />
    </div>
  </EditDialog>
</template>

<style scoped>
h3 {
  margin-top: 4px;
}
.error {
  color: var(--c-warn);
}
</style>
