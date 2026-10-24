<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import { useI18n } from 'vue-i18n'
import type { UserDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import StatusTag from '@/shared/ui/StatusTag.vue'
import { useSites } from '@/features/garden/queries'
import { useUsers } from '../queries'
import UserEditor from './UserEditor.vue'

const { t } = useI18n()
const { dateTime } = useFormat()
const users = useUsers()
const sites = useSites(true)
const editor = ref<InstanceType<typeof UserEditor>>()

const siteName = computed(() => new Map((sites.data.value ?? []).map((s) => [s.id, s.name])))
const waiting = computed(() => (users.data.value ?? []).filter((u) => u.isPending).length)
const status = (u: UserDto) =>
  u.isPending
    ? { tone: 'warn' as const, key: 'admin.user.status.pending' }
    : u.isAdmin
      ? { tone: 'ok' as const, key: u.isConfigAdmin ? 'admin.user.status.configAdmin' : 'admin.user.status.admin' }
      : u.invited
        ? { tone: 'info' as const, key: 'admin.user.status.invited' }
        : { tone: 'neutral' as const, key: 'admin.user.status.active' }
</script>

<template>
  <div class="stack">
    <div class="row">
      <span v-if="waiting" class="waiting">⏳ {{ t('admin.user.waiting', { n: waiting }) }}</span>
      <Button :label="t('admin.user.invite')" icon="pi pi-user-plus" class="invite" @click="editor?.open()" />
    </div>
    <DataTable
      :value="users.data.value ?? []"
      :loading="users.isLoading.value"
      data-key="id"
      size="small"
      row-hover
      class="clickable"
      @row-click="(e) => editor?.open(e.data as UserDto)"
    >
      <Column :header="t('admin.user.who')">
        <template #body="{ data }">
          <b>{{ data.name ?? data.email }}</b>
          <div v-if="data.name" class="muted small">{{ data.email }}</div>
        </template>
      </Column>
      <Column :header="t('admin.status')">
        <template #body="{ data }">
          <StatusTag :tone="status(data).tone">{{ t(status(data).key) }}</StatusTag>
        </template>
      </Column>
      <Column :header="t('admin.user.sites')">
        <template #body="{ data }">
          <span v-if="data.isAdmin" class="muted small">{{ t('admin.user.allSites') }}</span>
          <span v-for="s in data.sites" :key="s.siteId" class="site">
            {{ siteName.get(s.siteId) }} <span class="muted">· {{ t(`admin.user.access.${s.role}`) }}</span>
          </span>
        </template>
      </Column>
      <Column :header="t('admin.user.lastSeen')" class="hide-sm">
        <template #body="{ data }">
          <span class="muted small">{{ data.lastSeenAt ? dateTime(data.lastSeenAt) : '—' }}</span>
        </template>
      </Column>
    </DataTable>
    <UserEditor ref="editor" />
  </div>
</template>

<style scoped>
.invite {
  margin-left: auto;
}
.waiting {
  color: #8a5b00;
  font-weight: 600;
}
.site {
  display: inline-block;
  margin-right: 10px;
  white-space: nowrap;
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
