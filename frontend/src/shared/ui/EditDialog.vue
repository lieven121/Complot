<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import { useI18n } from 'vue-i18n'
import { useSession } from '@/shared/session'

/**
 * Modal form shell: title, scrollable body, and save / cancel / delete actions. Read-only shows the same form
 * without the actions; by default that is the case for users who may not change shared data (the catalog).
 */
// `readonly: undefined` (not false) so an absent prop falls back to the session.
const props = withDefaults(defineProps<{ title: string; saving?: boolean; canDelete?: boolean; width?: string; readonly?: boolean }>(), {
  readonly: undefined,
})
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ save: []; delete: [] }>()
const { t } = useI18n()
const { isEditor } = useSession()
const locked = computed(() => props.readonly ?? !isEditor.value)
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="title"
    :style="{ width: width ?? '640px' }"
    :breakpoints="{ '900px': '96vw' }"
    :draggable="false"
  >
    <form id="edit-dialog-form" novalidate @submit.prevent="locked || emit('save')">
      <fieldset class="form-grid" :disabled="locked">
        <slot />
      </fieldset>
    </form>
    <template #footer>
      <Button
        v-if="canDelete && !locked"
        :label="t('common.delete')"
        severity="danger"
        text
        icon="pi pi-trash"
        class="delete"
        @click="emit('delete')"
      />
      <template v-if="locked">
        <span class="muted small readonly">{{ t('access.readOnlyHint') }}</span>
        <Button :label="t('common.close')" severity="secondary" outlined @click="visible = false" />
      </template>
      <template v-else>
        <Button :label="t('common.cancel')" severity="secondary" outlined @click="visible = false" />
        <Button :label="t('common.save')" type="submit" form="edit-dialog-form" :loading="saving" />
      </template>
    </template>
  </Dialog>
</template>

<style scoped>
.delete,
.readonly {
  margin-right: auto;
}
fieldset {
  border: 0;
  margin: 0;
  padding: 0;
  min-width: 0;
}
</style>
