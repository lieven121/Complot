<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import { useI18n } from 'vue-i18n'
import { useNotify } from '@/shared/composables/useNotify'
import FormField from '@/shared/ui/FormField.vue'
import { buildAiPrompt } from '../exchange'
import { useCategories } from '../queries'

const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ import: [] }>()

const { t } = useI18n()
const notify = useNotify()
const categories = useCategories()
const task = ref('')
const prompt = computed(() => buildAiPrompt(t, task.value, categories.data.value ?? []))

async function copy() {
  await navigator.clipboard.writeText(prompt.value)
  notify.success('common.copied')
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="t('catalog.exchange.aiPrompt')"
    :style="{ width: '820px' }"
    :breakpoints="{ '900px': '96vw' }"
  >
    <p class="muted">{{ t('catalog.exchange.aiPromptHelp') }}</p>
    <FormField :label="t('catalog.exchange.taskLabel')" for="ai-task">
      <InputText id="ai-task" v-model="task" :placeholder="t('catalog.exchange.prompt.taskPlaceholder')" fluid />
    </FormField>
    <Textarea :model-value="prompt" readonly rows="18" class="prompt" :aria-label="t('catalog.exchange.aiPrompt')" />
    <template #footer>
      <Button :label="t('catalog.exchange.toImport')" severity="secondary" outlined @click="emit('import')" />
      <Button :label="t('common.copy')" icon="pi pi-copy" @click="copy" />
    </template>
  </Dialog>
</template>

<style scoped>
.prompt {
  width: 100%;
  margin-top: 10px;
  font-family: ui-monospace, Consolas, monospace;
  font-size: 12px;
}
</style>
