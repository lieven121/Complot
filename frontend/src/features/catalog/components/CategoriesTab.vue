<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import { useI18n } from 'vue-i18n'
import { useSession } from '@/shared/session'
import type { CategoryDto, FamilyDto } from '@/shared/api'
import CategoryChip from '@/shared/ui/CategoryChip.vue'
import StatusTag from '@/shared/ui/StatusTag.vue'
import { useCatalogLookups } from '../queries'
import CategoryEditor from './CategoryEditor.vue'
import FamilyEditor from './FamilyEditor.vue'

const { t, locale } = useI18n()
const { isEditor } = useSession()
const { categories, families, familyById, categoryById } = useCatalogLookups()
const categoryEditor = ref<InstanceType<typeof CategoryEditor>>()
const familyEditor = ref<InstanceType<typeof FamilyEditor>>()

const familyName = (f: FamilyDto) => (locale.value === 'en' ? f.nameEn : f.nameNl)
</script>

<template>
  <div class="grid-2">
    <section class="card">
      <div class="row head">
        <h2>{{ t('catalog.tabs.categoriesOnly') }}</h2>
        <Button v-if="isEditor" :label="t('catalog.category.new')" icon="pi pi-plus" size="small" @click="categoryEditor?.open()" />
      </div>
      <DataTable
        :value="categories.data.value ?? []"
        :loading="categories.isLoading.value"
        data-key="id"
        size="small"
        row-hover
        class="clickable"
        @row-click="(e) => categoryEditor?.open(e.data as CategoryDto)"
      >
        <Column :header="t('catalog.category.name')">
          <template #body="{ data }">
            <CategoryChip :category="data" />
            <div class="muted small">{{ familyById.get(data.defaultFamilyId)?.latinName }}</div>
          </template>
        </Column>
        <Column :header="t('catalog.category.neighbours')">
          <template #body="{ data }">
            <div class="chips">
              <span v-for="id in data.goodNeighbourIds" :key="id" class="small">👍 {{ categoryById.get(id)?.icon }}</span>
              <span v-for="id in data.badNeighbourIds" :key="id" class="small">👎 {{ categoryById.get(id)?.icon }}</span>
            </div>
          </template>
        </Column>
        <Column class="num">
          <template #body="{ data }">
            <StatusTag v-if="data.rotationExempt" tone="info">{{ t('catalog.category.exempt') }}</StatusTag>
            <span class="muted small"> {{ data.varietyCount }}</span>
          </template>
        </Column>
      </DataTable>
    </section>

    <section class="card">
      <div class="row head">
        <h2>{{ t('catalog.family.title') }}</h2>
        <Button v-if="isEditor" :label="t('catalog.family.new')" icon="pi pi-plus" size="small" @click="familyEditor?.open()" />
      </div>
      <p class="muted small">{{ t('catalog.family.explain') }}</p>
      <DataTable
        :value="families.data.value ?? []"
        :loading="families.isLoading.value"
        data-key="id"
        size="small"
        row-hover
        class="clickable"
        @row-click="(e) => familyEditor?.open(e.data as FamilyDto)"
      >
        <Column :header="t('catalog.family.latinName')">
          <template #body="{ data }"
            ><i>{{ data.latinName }}</i> <span class="muted small">{{ familyName(data) }}</span></template
          >
        </Column>
        <Column :header="t('catalog.family.rotationRestYears')" class="num">
          <template #body="{ data }">{{ data.rotationRestYears ? t('common.years', { n: data.rotationRestYears }) : '—' }}</template>
        </Column>
      </DataTable>
    </section>

    <CategoryEditor ref="categoryEditor" />
    <FamilyEditor ref="familyEditor" />
  </div>
</template>

<style scoped>
.head {
  justify-content: space-between;
  margin-bottom: 8px;
}
.head h2 {
  margin: 0;
}
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 8px;
}
.clickable :deep(tbody tr) {
  cursor: pointer;
}
</style>
