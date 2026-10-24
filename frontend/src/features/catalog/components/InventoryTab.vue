<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import InputText from 'primevue/inputtext'
import { useI18n } from 'vue-i18n'
import type { InventoryLotDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useCatalogLookups, useInventoryLots } from '../queries'
import LotEditor from './LotEditor.vue'
import LotStatusTag from './LotStatusTag.vue'

const { t } = useI18n()
const { date, decimal } = useFormat()
const lots = useInventoryLots()
const { varietyById, supplierById, categoryById } = useCatalogLookups()
const editor = ref<InstanceType<typeof LotEditor>>()
const search = ref('')

const rows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return (lots.data.value ?? [])
    .map((lot) => ({ lot, variety: varietyById.value.get(lot.varietyId), supplier: supplierById.value.get(lot.supplierId) }))
    .filter(({ variety }) => !term || [variety?.name, variety?.cultivar].some((s) => s?.toLowerCase().includes(term)))
})
</script>

<template>
  <div class="stack">
    <div class="row">
      <InputText v-model="search" :placeholder="t('catalog.searchPlaceholder')" :aria-label="t('common.search')" class="grow" />
      <Button :label="t('catalog.lot.new')" icon="pi pi-plus" @click="editor?.open()" />
    </div>
    <DataTable
      :value="rows"
      :loading="lots.isLoading.value"
      data-key="lot.id"
      size="small"
      row-hover
      class="clickable"
      @row-click="(e) => editor?.open((e.data as { lot: InventoryLotDto }).lot)"
    >
      <template #empty>{{ t('catalog.lot.none') }}</template>
      <Column :header="t('catalog.variety.one')">
        <template #body="{ data }">
          {{ categoryById.get(data.variety?.categoryId)?.icon }} {{ data.variety?.name }}
          <span class="muted small">{{ data.variety?.cultivar }}</span>
        </template>
      </Column>
      <Column :header="t('catalog.lot.quantity')" class="num">
        <template #body="{ data }">{{ decimal(data.lot.quantity) }} {{ data.lot.unit }}</template>
      </Column>
      <Column :header="t('catalog.lot.source')">
        <template #body="{ data }">{{ data.supplier?.isOwnSource ? '♻️ ' : '' }}{{ data.supplier?.name }}</template>
      </Column>
      <Column :header="t('catalog.lot.boughtOn')" class="hide-sm">
        <template #body="{ data }"
          ><span class="small">{{ date(data.lot.boughtOn) }}</span></template
        >
      </Column>
      <Column :header="t('catalog.lot.bestBefore')">
        <template #body="{ data }"
          ><span class="small">{{ date(data.lot.bestBefore) }}</span></template
        >
      </Column>
      <Column :header="t('catalog.lot.status')">
        <template #body="{ data }"><LotStatusTag :status="data.lot.status" /></template>
      </Column>
    </DataTable>
    <LotEditor ref="editor" />
  </div>
</template>

<style scoped>
.grow {
  flex: 1;
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
