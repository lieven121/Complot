<script setup lang="ts">
import { computed } from 'vue'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import { useI18n } from 'vue-i18n'
import { useQueryParam } from '@/shared/composables/useQueryParam'
import { useSession } from '@/shared/session'
import SitesTab from './components/SitesTab.vue'
import UsersTab from './components/UsersTab.vue'

const { t } = useI18n()
const { me, isAdmin, signInEnabled } = useSession()
const tabParam = useQueryParam('tab')
const tab = computed({
  get: () => (tabParam.value === 'users' ? 'users' : 'sites'),
  set: (v) => (tabParam.value = v === 'sites' ? undefined : v),
})
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('nav.admin') }}</h1>
    </div>
    <p v-if="me.isSuccess.value && !isAdmin" class="card muted">{{ t('access.adminOnly') }}</p>
    <Tabs v-else v-model:value="tab">
      <TabList>
        <Tab value="sites">{{ t('admin.tabs.sites') }}</Tab>
        <Tab value="users">{{ t('admin.tabs.users') }}</Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="sites"><SitesTab /></TabPanel>
        <TabPanel value="users">
          <UsersTab v-if="signInEnabled" />
          <div v-else class="card">
            <h2>{{ t('admin.user.openModeTitle') }}</h2>
            <p class="muted">{{ t('admin.user.openMode') }}</p>
          </div>
        </TabPanel>
      </TabPanels>
    </Tabs>
  </div>
</template>

<style scoped>
.page {
  max-width: 1100px;
}
:deep(.p-tabpanels) {
  background: transparent;
  padding: 14px 0 0;
}
:deep(.p-tablist-tab-list) {
  background: transparent;
}
</style>
