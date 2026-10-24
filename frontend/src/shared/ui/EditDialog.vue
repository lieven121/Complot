<script setup lang="ts">
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import { useI18n } from 'vue-i18n'

/** Modal form shell: title, scrollable body, and save / cancel / delete actions. */
defineProps<{ title: string; saving?: boolean; canDelete?: boolean; width?: string }>()
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ save: []; delete: [] }>()
const { t } = useI18n()
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
    <form id="edit-dialog-form" class="form-grid" novalidate @submit.prevent="emit('save')">
      <slot />
    </form>
    <template #footer>
      <Button
        v-if="canDelete"
        :label="t('common.delete')"
        severity="danger"
        text
        icon="pi pi-trash"
        class="delete"
        @click="emit('delete')"
      />
      <Button :label="t('common.cancel')" severity="secondary" outlined @click="visible = false" />
      <Button :label="t('common.save')" type="submit" form="edit-dialog-form" :loading="saving" />
    </template>
  </Dialog>
</template>

<style scoped>
.delete {
  margin-right: auto;
}
</style>
