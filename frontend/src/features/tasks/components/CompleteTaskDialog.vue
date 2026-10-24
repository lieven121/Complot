<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import { useI18n } from 'vue-i18n'
import type { TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import FormField from '@/shared/ui/FormField.vue'
import { useTexts } from '@/features/garden/useTexts'
import { useCompleteTask } from '../queries'

/** Checks a task off and asks how long it really took — that is what makes the estimates learn. */
const props = defineProps<{ task: TaskDto }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const { duration } = useFormat()
const texts = useTexts()
const notify = useNotify()
const complete = useCompleteTask()
const custom = ref<number>()
const visible = ref(true)

const quick = computed(() =>
  [props.task.estimateMinutes - 15, props.task.estimateMinutes + 15, props.task.estimateMinutes + 30].filter((m) => m >= 5),
)

async function save(actualMinutes?: number) {
  try {
    await complete.mutateAsync({ key: props.task.key, input: { actualMinutes, doneOn: undefined } })
    notify.success('tasks.completed')
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
    :header="t('tasks.completeTitle', { task: texts.task(task) })"
    :style="{ width: '520px' }"
    :breakpoints="{ '600px': '96vw' }"
    @after-hide="emit('close')"
  >
    <p class="muted">{{ t('tasks.howLong') }}</p>
    <div class="quick">
      <Button
        :label="t('tasks.asEstimated', { duration: duration(task.estimateMinutes) })"
        :loading="complete.isPending.value"
        @click="save(task.estimateMinutes)"
      />
      <Button v-for="m in quick" :key="m" :label="duration(m)" severity="secondary" outlined @click="save(m)" />
    </div>
    <form class="custom" @submit.prevent="custom && save(custom)">
      <FormField :label="t('tasks.actualMinutes')" for="actual">
        <InputNumber v-model="custom" input-id="actual" :min="1" :max="1440" :suffix="` ${t('common.minutesUnit')}`" />
      </FormField>
      <Button type="submit" :label="t('common.save')" :disabled="!custom" />
    </form>
    <template #footer>
      <Button :label="t('tasks.skipDuration')" text severity="secondary" @click="save(undefined)" />
    </template>
  </Dialog>
</template>

<style scoped>
.quick {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
.quick :deep(.p-button) {
  padding: 10px 14px;
}
.custom {
  display: flex;
  align-items: flex-end;
  gap: 8px;
  margin-top: 14px;
}
.custom > :first-child {
  flex: 1;
}
</style>
