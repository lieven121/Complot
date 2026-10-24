<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import { useI18n } from 'vue-i18n'
import type { SiteDto } from '@/shared/api'
import { useNotify } from '@/shared/composables/useNotify'
import StatusTag from '@/shared/ui/StatusTag.vue'
import { useSites } from '@/features/garden/queries'
import { useArchiveSite, useRestoreSite } from '../queries'
import DeleteSiteDialog from './DeleteSiteDialog.vue'
import SiteEditor from './SiteEditor.vue'

const { t } = useI18n()
const notify = useNotify()
const sites = useSites(true)
const archive = useArchiveSite()
const restore = useRestoreSite()
const editor = ref<InstanceType<typeof SiteEditor>>()
const deleting = ref<SiteDto>()

async function setArchived(site: SiteDto, archived: boolean) {
  try {
    await (archived ? archive : restore).mutateAsync(site.id)
    notify.success(archived ? 'admin.site.archivedToast' : 'admin.site.restored', { name: site.name })
  } catch (error) {
    notify.error(error)
  }
}
const rowClass = (site: SiteDto) => ({ archived: site.archived })
</script>

<template>
  <div class="stack">
    <div class="row end">
      <Button :label="t('admin.site.new')" icon="pi pi-plus" @click="editor?.open()" />
    </div>
    <DataTable :value="sites.data.value ?? []" :loading="sites.isLoading.value" data-key="id" size="small" :row-class="rowClass">
      <Column :header="t('common.name')">
        <template #body="{ data }">
          <b>{{ data.name }}</b>
          <div v-if="data.notes" class="muted small">{{ data.notes }}</div>
        </template>
      </Column>
      <Column :header="t('admin.site.beds')" class="num">
        <template #body="{ data }">{{ data.bedCount }}</template>
      </Column>
      <Column :header="t('admin.site.members')" class="num hide-sm">
        <template #body="{ data }">{{ data.memberCount }}</template>
      </Column>
      <Column :header="t('admin.status')">
        <template #body="{ data }">
          <StatusTag :tone="data.archived ? 'neutral' : 'ok'">{{
            t(data.archived ? 'admin.site.isArchived' : 'admin.site.active')
          }}</StatusTag>
        </template>
      </Column>
      <Column class="actions">
        <template #body="{ data }">
          <Button
            icon="pi pi-pencil"
            text
            rounded
            severity="secondary"
            :aria-label="t('common.edit')"
            :title="t('common.edit')"
            @click="editor?.open(data)"
          />
          <Button
            v-if="!data.archived"
            :label="t('admin.site.archive')"
            icon="pi pi-inbox"
            text
            size="small"
            severity="secondary"
            @click="setArchived(data, true)"
          />
          <template v-else>
            <Button :label="t('admin.site.restore')" icon="pi pi-replay" text size="small" @click="setArchived(data, false)" />
            <Button :label="t('common.delete')" icon="pi pi-trash" text size="small" severity="danger" @click="deleting = data" />
          </template>
        </template>
      </Column>
    </DataTable>
    <p class="muted small">{{ t('admin.site.archiveHelp') }}</p>
    <SiteEditor ref="editor" />
    <DeleteSiteDialog v-if="deleting" :site="deleting" @close="deleting = undefined" />
  </div>
</template>

<style scoped>
.end {
  justify-content: flex-end;
}
:deep(.archived) td {
  color: var(--c-muted);
}
:deep(td.actions) {
  text-align: right;
  white-space: nowrap;
}
@media (max-width: 900px) {
  :deep(.hide-sm) {
    display: none;
  }
}
</style>
