<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import { useConfirm } from 'primevue/useconfirm'
import { useI18n } from 'vue-i18n'
import type { AccessLinkDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import { addDays, todayIso } from '@/shared/dates'
import StatusTag from '@/shared/ui/StatusTag.vue'
import { useSites } from '@/features/garden/queries'
import { usePersonById } from '@/features/tasks/queries'
import { useLinks, useRenewLink } from '../queries'
import LinkCreatedDialog from './LinkCreatedDialog.vue'
import LinkEditor from './LinkEditor.vue'

/** Access links: access without an account (volunteers), each with its own end date. */
const { t } = useI18n()
const { date, dateTime } = useFormat()
const confirm = useConfirm()
const notify = useNotify()
const links = useLinks()
const sites = useSites(true)
const personById = usePersonById()
const renew = useRenewLink()
const editor = ref<InstanceType<typeof LinkEditor>>()
const created = ref<{ token: string; link: AccessLinkDto }>()

const siteName = computed(() => new Map((sites.data.value ?? []).map((s) => [s.id, s.name])))
const soon = addDays(todayIso(), 7)
const status = (l: AccessLinkDto) =>
  l.expired
    ? { tone: 'neutral' as const, key: 'admin.link.expired' }
    : l.validUntil <= soon
      ? { tone: 'warn' as const, key: 'admin.link.expiresSoon' }
      : { tone: 'ok' as const, key: 'admin.link.valid' }

function renewToken(link: AccessLinkDto) {
  confirm.require({
    header: t('admin.link.renewTitle'),
    message: t('admin.link.renewWarning', { label: link.label }),
    acceptProps: { label: t('admin.link.renew') },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', outlined: true },
    accept: async () => {
      try {
        created.value = await renew.mutateAsync(link.id)
      } catch (error) {
        notify.error(error)
      }
    },
  })
}
</script>

<template>
  <div class="stack">
    <div class="row">
      <span class="muted small help">{{ t('admin.link.help') }}</span>
      <Button :label="t('admin.link.new')" icon="pi pi-link" @click="editor?.open()" />
    </div>
    <DataTable
      :value="links.data.value ?? []"
      :loading="links.isLoading.value"
      data-key="id"
      size="small"
      row-hover
      class="clickable"
      @row-click="(e) => editor?.open(e.data as AccessLinkDto)"
    >
      <Column :header="t('admin.link.label')">
        <template #body="{ data }">
          <b>{{ data.label }}</b>
          <div v-if="data.personId && personById.get(data.personId)" class="muted small">👤 {{ personById.get(data.personId)!.name }}</div>
        </template>
      </Column>
      <Column :header="t('admin.status')">
        <template #body="{ data }">
          <StatusTag :tone="status(data).tone">{{ t(status(data).key) }}</StatusTag>
          <div class="muted small">{{ t('admin.link.until', { date: date(data.validUntil) }) }}</div>
        </template>
      </Column>
      <Column :header="t('admin.user.sites')" class="hide-sm">
        <template #body="{ data }">
          <span v-for="s in data.sites" :key="s.siteId" class="site">
            {{ siteName.get(s.siteId) }} <span class="muted">· {{ t(`admin.user.access.${s.role}`) }}</span>
          </span>
        </template>
      </Column>
      <Column :header="t('admin.link.lastUsed')" class="hide-sm">
        <template #body="{ data }">
          <span class="muted small">{{ data.lastUsedAt ? dateTime(data.lastUsedAt) : '—' }}</span>
        </template>
      </Column>
      <Column class="actions">
        <template #body="{ data }">
          <Button
            v-if="!data.expired"
            :label="t('admin.link.renew')"
            icon="pi pi-refresh"
            text
            size="small"
            severity="secondary"
            @click.stop="renewToken(data)"
          />
        </template>
      </Column>
    </DataTable>
    <LinkEditor ref="editor" @created="(token, link) => (created = { token, link })" />
    <LinkCreatedDialog v-if="created" :token="created.token" :link="created.link" @close="created = undefined" />
  </div>
</template>

<style scoped>
.help {
  flex: 1;
  min-width: 220px;
}
.site {
  display: inline-block;
  margin-right: 10px;
  white-space: nowrap;
}
.clickable :deep(tbody tr) {
  cursor: pointer;
}
:deep(td.actions) {
  text-align: right;
}
@media (max-width: 900px) {
  :deep(.hide-sm) {
    display: none;
  }
}
</style>
