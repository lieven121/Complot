<script setup lang="ts">
import { computed } from 'vue'
import SelectButton from 'primevue/selectbutton'
import { useI18n } from 'vue-i18n'
import type { SiteRole, SiteRoleDto } from '@/shared/api'
import { useSites } from '@/features/garden/queries'

/** Per site: no access, view or edit. Used for users and access links. */
type Access = SiteRole | 'none'

const model = defineModel<SiteRoleDto[]>({ required: true })
const { t } = useI18n()
const sites = useSites(true)

const options = computed(() => (['none', 'view', 'edit'] as const).map((value) => ({ value, label: t(`admin.user.access.${value}`) })))
const accessOn = (siteId: string): Access => model.value.find((s) => s.siteId === siteId)?.role ?? 'none'
function setAccess(siteId: string, access: Access) {
  const others = model.value.filter((s) => s.siteId !== siteId)
  model.value = access === 'none' ? others : [...others, { siteId, role: access }]
}
</script>

<template>
  <div v-for="site in sites.data.value ?? []" :key="site.id" class="site-row">
    <span>
      {{ site.name }} <span v-if="site.archived" class="muted small">({{ t('admin.site.isArchived') }})</span>
    </span>
    <SelectButton
      :model-value="accessOn(site.id)"
      :options="options"
      option-label="label"
      option-value="value"
      :allow-empty="false"
      size="small"
      :aria-label="site.name"
      @update:model-value="setAccess(site.id, $event)"
    />
  </div>
</template>

<style scoped>
.site-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex-wrap: wrap;
  padding: 6px 0;
  border-bottom: 1px solid var(--c-line);
}
</style>
