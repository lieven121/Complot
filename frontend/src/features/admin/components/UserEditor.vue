<script setup lang="ts">
import { computed } from 'vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import { useI18n } from 'vue-i18n'
import type { SiteRole, UserDto, UserInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { useSites } from '@/features/garden/queries'
import { usePeople } from '@/features/tasks/queries'
import { useDeleteUser, useSaveUser } from '../queries'

/** Invite or edit a user: admin or not, and per site no access / view / edit. */
type Access = SiteRole | 'none'

const { t } = useI18n()
const sites = useSites(true)
const people = usePeople()
const editor = useEditor({
  toInput: (u?: UserDto): UserInput => ({
    email: u?.email ?? '',
    name: u?.name,
    isAdmin: u?.isAdmin ?? false,
    personId: u?.personId,
    sites: (u?.sites ?? []).map((s) => ({ ...s })),
  }),
  save: useSaveUser(),
  remove: useDeleteUser(),
  nameOf: (u) => u.name ?? u.email,
})
const { form, errors } = editor
defineExpose({ open: editor.open })

const configAdmin = computed(() => editor.editing.value?.isConfigAdmin ?? false)
const accessOptions = computed(() =>
  (['none', 'view', 'edit'] as const).map((value) => ({ value, label: t(`admin.user.access.${value}`) })),
)
const accessOn = (siteId: string): Access => form.value.sites.find((s) => s.siteId === siteId)?.role ?? 'none'
function setAccess(siteId: string, access: Access) {
  const others = form.value.sites.filter((s) => s.siteId !== siteId)
  form.value.sites = access === 'none' ? others : [...others, { siteId, role: access }]
}
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('admin.user.invite') : t('admin.user.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    :readonly="false"
    width="620px"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField
      :label="t('admin.user.email')"
      for="u-email"
      :error="errors.field('email')"
      :hint="editor.isNew.value ? t('admin.user.emailHint') : undefined"
    >
      <InputText id="u-email" v-model="form.email" type="email" autofocus :invalid="!!errors.field('email')" />
    </FormField>
    <FormField :label="t('common.name')" for="u-name" :error="errors.field('name')">
      <InputText id="u-name" v-model="form.name" />
    </FormField>
    <FormField :label="t('admin.user.person')" for="u-person" :hint="t('admin.user.personHint')">
      <Select
        v-model="form.personId"
        input-id="u-person"
        :options="people.data.value ?? []"
        option-label="name"
        option-value="id"
        show-clear
        :placeholder="t('common.none')"
      />
    </FormField>
    <FormField :label="t('admin.user.admin')" :hint="configAdmin ? t('admin.user.configAdminHint') : t('admin.user.adminHint')">
      <ToggleSwitch v-model="form.isAdmin" :disabled="configAdmin" />
    </FormField>
    <div class="full">
      <h3>{{ t('admin.user.sites') }}</h3>
      <p v-if="form.isAdmin || configAdmin" class="muted small">{{ t('admin.user.adminSeesAll') }}</p>
      <div v-for="site in sites.data.value ?? []" :key="site.id" class="site-row">
        <span>
          {{ site.name }} <span v-if="site.archived" class="muted small">({{ t('admin.site.isArchived') }})</span>
        </span>
        <SelectButton
          :model-value="accessOn(site.id)"
          :options="accessOptions"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          size="small"
          :aria-label="site.name"
          @update:model-value="setAccess(site.id, $event)"
        />
      </div>
    </div>
  </EditDialog>
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
h3 {
  margin-top: 4px;
}
</style>
