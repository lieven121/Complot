<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import { useI18n } from 'vue-i18n'
import { api } from '@/shared/api'
import { useNotify } from '@/shared/composables/useNotify'
import { useSession } from '@/shared/session'
import AiPromptDialog from './components/AiPromptDialog.vue'
import CatalogTab from './components/CatalogTab.vue'
import CategoriesTab from './components/CategoriesTab.vue'
import ImportDialog from './components/ImportDialog.vue'
import InventoryTab from './components/InventoryTab.vue'
import SuppliersTab from './components/SuppliersTab.vue'
import { useCategories, useVarieties } from './queries'

const TABS = ['catalog', 'inventory', 'categories', 'suppliers'] as const
type TabName = (typeof TABS)[number]

const { t } = useI18n()
const { isEditor } = useSession()
const route = useRoute()
const router = useRouter()
const notify = useNotify()
const categories = useCategories()
const varieties = useVarieties()

/** The active tab lives in the URL (?tab=…) so it survives reloads and can be linked. */
const tab = computed<TabName>({
  get: () => (TABS.includes(route.query.tab as TabName) ? (route.query.tab as TabName) : 'catalog'),
  set: (value) => router.replace({ query: { ...route.query, tab: value === 'catalog' ? undefined : value } }),
})

const aiPromptVisible = ref(false)
const importVisible = ref(false)

async function exportCatalog() {
  try {
    const data = await api.catalogExchange.exportCatalog()
    const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }))
    Object.assign(document.createElement('a'), { href: url, download: 'complot-catalogus.json' }).click()
    URL.revokeObjectURL(url)
    notify.success('catalog.exchange.exported')
  } catch (error) {
    notify.error(error)
  }
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('nav.plants') }}</h1>
      <span class="muted">{{
        t('catalog.summary', { categories: categories.data.value?.length ?? 0, varieties: varieties.data.value?.length ?? 0 })
      }}</span>
      <div class="actions">
        <Button
          v-if="isEditor"
          :label="t('catalog.exchange.aiPrompt')"
          icon="pi pi-sparkles"
          severity="secondary"
          outlined
          @click="aiPromptVisible = true"
        />
        <Button
          v-if="isEditor"
          :label="t('catalog.exchange.import')"
          icon="pi pi-upload"
          severity="secondary"
          outlined
          @click="importVisible = true"
        />
        <Button :label="t('catalog.exchange.export')" icon="pi pi-download" severity="secondary" outlined @click="exportCatalog" />
      </div>
    </div>

    <Tabs v-model:value="tab">
      <TabList>
        <Tab value="catalog">{{ t('catalog.tabs.catalog') }}</Tab>
        <Tab value="inventory">{{ t('catalog.tabs.inventory') }}</Tab>
        <Tab value="categories">{{ t('catalog.tabs.categories') }}</Tab>
        <Tab value="suppliers">{{ t('catalog.tabs.suppliers') }}</Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="catalog"><CatalogTab /></TabPanel>
        <TabPanel value="inventory"><InventoryTab /></TabPanel>
        <TabPanel value="categories"><CategoriesTab /></TabPanel>
        <TabPanel value="suppliers"><SuppliersTab /></TabPanel>
      </TabPanels>
    </Tabs>

    <AiPromptDialog v-model:visible="aiPromptVisible" @import="((aiPromptVisible = false), (importVisible = true))" />
    <ImportDialog v-model:visible="importVisible" />
  </div>
</template>

<style scoped>
:deep(.p-tabpanels) {
  background: transparent;
  padding: 14px 0 0;
}
:deep(.p-tablist-tab-list) {
  background: transparent;
}
</style>
