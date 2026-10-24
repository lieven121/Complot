<script setup lang="ts">
import { computed } from 'vue'
import DatePicker from 'primevue/datepicker'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import { useI18n } from 'vue-i18n'
import type { BedDto, ManualTaskInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import { fromIsoDate, todayIso, toIsoDate } from '@/shared/dates'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import InputNumber from '@/shared/ui/NumberInput.vue'
import { usePeople, useSaveManualTask } from '../queries'

/** A one-off task of your own ("fix the fence"); planting and tree tasks come from the plan itself. */
const props = defineProps<{ siteId?: string; beds?: BedDto[] }>()

const { t } = useI18n()
const people = usePeople()
const editor = useEditor({
  toInput: (): ManualTaskInput => ({
    siteId: props.siteId,
    bedId: undefined,
    title: '',
    dueOn: todayIso(),
    baseMinutes: 30,
    assigneeId: undefined,
  }),
  save: useSaveManualTask(),
})
const { form, errors } = editor
const dueOn = computed({ get: () => fromIsoDate(form.value.dueOn), set: (d) => (form.value.dueOn = toIsoDate(d) ?? todayIso()) })
defineExpose({ open: () => editor.open() })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="t('tasks.manual.new')"
    :saving="editor.saving.value"
    width="520px"
    @save="editor.submit"
  >
    <FormField :label="t('tasks.manual.title')" for="m-title" :error="errors.field('title')" full>
      <InputText id="m-title" v-model="form.title" autofocus :invalid="!!errors.field('title')" />
    </FormField>
    <FormField :label="t('tasks.manual.dueOn')" for="m-due">
      <DatePicker v-model="dueOn" input-id="m-due" show-icon date-format="dd/mm/yy" />
    </FormField>
    <FormField :label="t('tasks.estimate')" for="m-min" :error="errors.field('baseMinutes')">
      <InputNumber
        v-model="form.baseMinutes"
        input-id="m-min"
        :min="1"
        :max="1440"
        :step="5"
        show-buttons
        :suffix="` ${t('common.minutesUnit')}`"
      />
    </FormField>
    <FormField :label="t('tasks.assignee')" for="m-person">
      <Select
        v-model="form.assigneeId"
        input-id="m-person"
        :options="people.data.value ?? []"
        option-label="name"
        option-value="id"
        show-clear
        :placeholder="t('tasks.nobody')"
      />
    </FormField>
    <FormField v-if="beds?.length" :label="t('tasks.manual.bed')" for="m-bed" :error="errors.field('bedId')">
      <Select
        v-model="form.bedId"
        input-id="m-bed"
        :options="beds"
        option-label="code"
        option-value="id"
        show-clear
        filter
        :placeholder="t('common.none')"
      />
    </FormField>
  </EditDialog>
</template>
