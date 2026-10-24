<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import { useI18n } from 'vue-i18n'
import { useSession } from '@/shared/session'
import type { SupplierDto } from '@/shared/api'
import StatusTag from '@/shared/ui/StatusTag.vue'
import { useSuppliers } from '../queries'
import SupplierEditor from './SupplierEditor.vue'

const { t } = useI18n()
const { isEditor } = useSession()
const suppliers = useSuppliers()
const editor = ref<InstanceType<typeof SupplierEditor>>()
</script>

<template>
  <div class="stack">
    <div class="row end">
      <Button v-if="isEditor" :label="t('catalog.supplier.new')" icon="pi pi-plus" @click="editor?.open()" />
    </div>
    <DataTable
      :value="suppliers.data.value ?? []"
      :loading="suppliers.isLoading.value"
      data-key="id"
      size="small"
      row-hover
      class="clickable"
      @row-click="(e) => editor?.open(e.data as SupplierDto)"
    >
      <Column :header="t('catalog.supplier.name')">
        <template #body="{ data }">
          <b>{{ data.name }}</b> <StatusTag v-if="data.isOwnSource" tone="ok">♻️ {{ t('catalog.supplier.own') }}</StatusTag>
        </template>
      </Column>
      <Column :header="t('catalog.supplier.url')">
        <template #body="{ data }">
          <a v-if="data.url" :href="data.url" target="_blank" rel="noopener noreferrer" @click.stop>{{ data.url }}</a>
        </template>
      </Column>
      <Column :header="t('catalog.supplier.notes')" class="hide-sm">
        <template #body="{ data }"
          ><span class="muted small">{{ data.notes }}</span></template
        >
      </Column>
    </DataTable>
    <SupplierEditor ref="editor" />
  </div>
</template>

<style scoped>
.end {
  justify-content: flex-end;
}
.clickable :deep(tbody tr) {
  cursor: pointer;
}
@media (max-width: 900px) {
  :deep(.hide-sm) {
    display: none;
  }
}
</style>
