<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { useI18n } from 'vue-i18n'
import type { TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import FormField from '@/shared/ui/FormField.vue'
import { useTexts } from '@/features/garden/useTexts'
import { manualTaskId, useDeleteManualTask, useEstimates, usePeople, useSetTaskState } from '../queries'

/** Task details: who does it and how long it should take (base × learned correction, or a manual override). */
const props = defineProps<{ task: TaskDto; where?: string }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const { date, duration, dateTime } = useFormat()
const texts = useTexts()
const notify = useNotify()
const people = usePeople()
const estimates = useEstimates()
const setState = useSetTaskState()
const deleteManual = useDeleteManualTask()
const visible = ref(true)

const assigneeId = ref(props.task.assigneeId)
const estimate = ref(props.task.estimateMinutes)
const factor = computed(() => estimates.data.value?.find((f) => f.kind === props.task.kind))
const manualId = computed(() => manualTaskId(props.task.key))

async function save() {
  try {
    const override = estimate.value !== props.task.estimateMinutes || props.task.estimateOverride ? estimate.value : undefined
    await setState.mutateAsync({
      key: props.task.key,
      input: { assigneeId: assigneeId.value, estimateOverride: override, scheduledStart: props.task.scheduledStart },
    })
    notify.success('common.saved')
    visible.value = false
  } catch (error) {
    notify.error(error)
  }
}

async function remove() {
  if (!manualId.value) return
  try {
    await deleteManual.mutateAsync(manualId.value)
    notify.success('common.deleted')
    visible.value = false
  } catch (error) {
    notify.error(error)
  }
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="texts.task(task)"
    :style="{ width: '520px' }"
    :breakpoints="{ '600px': '96vw' }"
    @after-hide="emit('close')"
  >
    <p class="muted">
      {{ t(`tasks.kindName.${task.kind}`) }}<template v-if="where"> · 📍 {{ where }}</template> · 📅 {{ date(task.date) }}
      <template v-if="task.scheduledStart"> · 🗓️ {{ dateTime(task.scheduledStart) }}</template>
    </p>
    <form id="task-form" class="form-grid" @submit.prevent="save">
      <FormField :label="t('tasks.assignee')" for="t-assignee">
        <Select
          v-model="assigneeId"
          input-id="t-assignee"
          :options="people.data.value ?? []"
          option-label="name"
          option-value="id"
          show-clear
          :placeholder="t('tasks.nobody')"
        />
      </FormField>
      <FormField
        :label="t('tasks.estimate')"
        for="t-estimate"
        :hint="
          factor
            ? t('tasks.estimateExplained', { base: duration(task.baseMinutes), factor: factor.factor, samples: factor.samples })
            : undefined
        "
      >
        <InputNumber
          v-model="estimate"
          input-id="t-estimate"
          :min="1"
          :max="1440"
          :step="5"
          show-buttons
          :suffix="` ${t('common.minutesUnit')}`"
        />
      </FormField>
      <p v-if="task.done" class="full small">
        ✔ {{ t('tasks.doneOn', { date: date(task.doneOn), duration: task.actualMinutes ? duration(task.actualMinutes) : '—' }) }}
      </p>
    </form>
    <template #footer>
      <Button v-if="manualId" :label="t('common.delete')" severity="danger" text icon="pi pi-trash" class="delete" @click="remove" />
      <Button :label="t('common.cancel')" severity="secondary" outlined @click="visible = false" />
      <Button :label="t('common.save')" type="submit" form="task-form" :loading="setState.isPending.value" />
    </template>
  </Dialog>
</template>

<style scoped>
.delete {
  margin-right: auto;
}
</style>
