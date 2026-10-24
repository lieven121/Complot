<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import { useI18n } from 'vue-i18n'
import type { SiteDto } from '@/shared/api'
import { useNotify } from '@/shared/composables/useNotify'
import FormField from '@/shared/ui/FormField.vue'
import { useDeleteSite } from '../queries'

/** Deleting a site cannot be undone, so the name has to be typed to confirm. */
const props = defineProps<{ site: SiteDto }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const notify = useNotify()
const remove = useDeleteSite()
const visible = ref(true)
const typed = ref('')
const matches = computed(() => typed.value.trim() === props.site.name)

async function confirm() {
  try {
    await remove.mutateAsync({ id: props.site.id, confirmName: typed.value.trim() })
    notify.success('admin.site.deleted', { name: props.site.name })
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
    :header="t('admin.site.deleteTitle', { name: site.name })"
    :style="{ width: '480px' }"
    :breakpoints="{ '600px': '96vw' }"
    @after-hide="emit('close')"
  >
    <p>{{ t('admin.site.deleteWarning', { beds: site.bedCount }) }}</p>
    <form id="delete-site" @submit.prevent="matches && confirm()">
      <FormField :label="t('admin.site.typeName', { name: site.name })" for="confirm-name">
        <InputText id="confirm-name" v-model="typed" autofocus autocomplete="off" />
      </FormField>
    </form>
    <template #footer>
      <Button :label="t('common.cancel')" severity="secondary" outlined @click="visible = false" />
      <Button
        :label="t('admin.site.deleteForever')"
        severity="danger"
        icon="pi pi-trash"
        type="submit"
        form="delete-site"
        :disabled="!matches"
        :loading="remove.isPending.value"
      />
    </template>
  </Dialog>
</template>
