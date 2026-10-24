<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import InputText from 'primevue/inputtext'
import { useI18n } from 'vue-i18n'
import type { VarietyDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import CategoryChip from '@/shared/ui/CategoryChip.vue'
import { useCatalogLookups } from '../queries'
import VarietyEditor from './VarietyEditor.vue'

const { t } = useI18n()
const { monthWindow, decimal } = useFormat()
const router = useRouter()
const { categories, varieties, categoryById } = useCatalogLookups()

const categoryFilter = ref<string>()
const search = ref('')
const editor = ref<InstanceType<typeof VarietyEditor>>()

const visible = computed(() => {
  const term = search.value.trim().toLowerCase()
  return (varieties.data.value ?? []).filter(
    (v) =>
      (!categoryFilter.value || v.categoryId === categoryFilter.value) &&
      (!term || [v.name, v.cultivar, v.latinName].some((s) => s?.toLowerCase().includes(term))),
  )
})

const sunIcons: Record<string, string> = { full: '☀️', half: '⛅', shade: '☁️' }
const stay = (v: VarietyDto) =>
  v.life !== 'perennial' ? t('catalog.oneSeason') : v.maxYearsInBed ? t('common.years', { n: v.maxYearsInBed }) : '∞'
const stock = (v: VarietyDto) => v.stock.map((s) => `${decimal(s.quantity)} ${s.unit}`).join(', ')
</script>

<template>
  <div class="layout">
    <nav class="categories" :aria-label="t('catalog.category.filter')">
      <button type="button" :class="{ on: !categoryFilter }" @click="categoryFilter = undefined">
        🌍 {{ t('catalog.allPlants') }}<span class="n">{{ varieties.data.value?.length ?? 0 }}</span>
      </button>
      <button
        v-for="c in categories.data.value ?? []"
        :key="c.id"
        type="button"
        :class="{ on: categoryFilter === c.id }"
        @click="categoryFilter = c.id"
      >
        <span class="dot" :style="{ background: c.color }" />{{ c.icon }} {{ c.name }}<span class="n">{{ c.varietyCount }}</span>
      </button>
    </nav>

    <div class="stack">
      <div class="row">
        <IconField class="grow">
          <InputIcon class="pi pi-search" />
          <InputText v-model="search" :placeholder="t('catalog.searchPlaceholder')" :aria-label="t('common.search')" fluid />
        </IconField>
        <Button :label="t('catalog.variety.new')" icon="pi pi-plus" @click="editor?.open(undefined, categoryFilter)" />
      </div>

      <DataTable
        :value="visible"
        :loading="varieties.isLoading.value"
        data-key="id"
        row-hover
        class="clickable"
        size="small"
        @row-click="(e) => router.push({ name: 'variety', params: { id: (e.data as VarietyDto).id } })"
      >
        <template #empty>{{ t('catalog.noMatch') }}</template>
        <Column :header="t('catalog.variety.one')">
          <template #body="{ data }">
            {{ categoryById.get(data.categoryId)?.icon }} <b>{{ data.name }}</b>
            <div class="muted small">{{ data.cultivar }}</div>
          </template>
        </Column>
        <Column :header="t('catalog.category.one')" class="hide-sm">
          <template #body="{ data }"><CategoryChip :category="categoryById.get(data.categoryId)" /></template>
        </Column>
        <Column :header="t('catalog.variety.sun')" class="hide-sm">
          <template #body="{ data }">{{ data.sun.map((s: string) => sunIcons[s]).join(' ') }}</template>
        </Column>
        <Column :header="t('catalog.variety.spacing')" class="hide-sm">
          <template #body="{ data }">
            <span class="small">{{
              data.spacingInRowCm ? `${data.spacingInRowCm} × ${data.spacingBetweenRowsCm ?? '—'} cm` : t('catalog.broadcast')
            }}</span>
          </template>
        </Column>
        <Column :header="t('catalog.variety.sowing')">
          <template #body="{ data }">
            <div v-if="data.sowIndoor" class="small">🏠 {{ monthWindow(data.sowIndoor) }}</div>
            <div v-if="data.sowOutdoor" class="small">🌱 {{ monthWindow(data.sowOutdoor) }}</div>
            <div v-else-if="data.plantOut" class="small">🪴 {{ monthWindow(data.plantOut) }}</div>
          </template>
        </Column>
        <Column :header="t('catalog.variety.harvest')">
          <template #body="{ data }"
            ><span class="small">🧺 {{ monthWindow(data.harvest) }}</span></template
          >
        </Column>
        <Column :header="t('catalog.restStay')" class="hide-sm">
          <template #body="{ data }">
            <span class="small">{{ t('common.years', { n: data.rotationRestYears }) }} / {{ stay(data) }}</span>
          </template>
        </Column>
        <Column :header="t('catalog.stock')" class="hide-sm">
          <template #body="{ data }"
            ><span class="small">{{ stock(data) || '—' }}</span></template
          >
        </Column>
      </DataTable>
    </div>

    <VarietyEditor ref="editor" />
  </div>
</template>

<style scoped>
.layout {
  display: grid;
  grid-template-columns: 230px minmax(0, 1fr);
  gap: 16px;
  align-items: start;
}
.categories {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.categories button {
  all: unset;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 10px;
  border-radius: 8px;
  cursor: pointer;
  white-space: nowrap;
}
.categories button:hover {
  background: #ece6d6;
}
.categories button.on {
  background: var(--c-panel);
  box-shadow: var(--shadow);
  font-weight: 650;
}
.categories button:focus-visible {
  outline: 2px solid var(--c-accent);
}
.n {
  margin-left: auto;
  font-size: 11px;
  color: var(--c-muted);
}
.dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex: none;
}
.grow {
  flex: 1;
}
.clickable :deep(tbody tr) {
  cursor: pointer;
}
@media (max-width: 900px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }
  .categories {
    flex-direction: row;
    overflow-x: auto;
  }
  :deep(.hide-sm) {
    display: none;
  }
}
</style>
