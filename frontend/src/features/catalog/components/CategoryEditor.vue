<script setup lang="ts">
import { computed } from 'vue'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import { useI18n } from 'vue-i18n'
import type { CategoryDto, CategoryInput } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import ColorInput from '@/shared/ui/ColorInput.vue'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import { useCategories, useDeleteCategory, useFamilies, useSaveCategory } from '../queries'

const { t } = useI18n()
const families = useFamilies()
const categories = useCategories()

const editor = useEditor({
  toInput: (c?: CategoryDto): CategoryInput => ({
    slug: c?.slug ?? '',
    name: c?.name ?? '',
    icon: c?.icon ?? '🌱',
    color: c?.color ?? '#3f7d4e',
    defaultFamilyId: c?.defaultFamilyId,
    rotationExempt: c?.rotationExempt ?? false,
    goodNeighbourIds: [...(c?.goodNeighbourIds ?? [])],
    badNeighbourIds: [...(c?.badNeighbourIds ?? [])],
  }),
  save: useSaveCategory(),
  remove: useDeleteCategory(),
  nameOf: (c) => c.name,
})
const { form, errors } = editor
defineExpose({ open: editor.open })

const otherCategories = computed(() => (categories.data.value ?? []).filter((c) => c.id !== editor.editing.value?.id))
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('catalog.category.new') : t('catalog.category.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    @save="editor.submit"
    @delete="editor.remove"
  >
    <FormField :label="t('catalog.category.name')" for="c-name" :error="errors.field('name')">
      <InputText id="c-name" v-model="form.name" autofocus :invalid="!!errors.field('name')" />
    </FormField>
    <FormField :label="t('catalog.category.slug')" for="c-slug" :error="errors.field('slug')" :hint="t('catalog.category.slugHint')">
      <InputText id="c-slug" v-model="form.slug" :invalid="!!errors.field('slug')" />
    </FormField>
    <FormField :label="t('catalog.category.icon')" for="c-icon" :error="errors.field('icon')">
      <InputText id="c-icon" v-model="form.icon" maxlength="16" />
    </FormField>
    <FormField :label="t('catalog.category.color')" :error="errors.field('color')">
      <ColorInput v-model="form.color" />
    </FormField>
    <FormField :label="t('catalog.category.defaultFamily')" for="c-family" :error="errors.field('defaultFamilyId')">
      <Select
        v-model="form.defaultFamilyId"
        input-id="c-family"
        :options="families.data.value ?? []"
        option-label="latinName"
        option-value="id"
        show-clear
        filter
      />
    </FormField>
    <FormField :label="t('catalog.category.rotationExempt')" :hint="t('catalog.category.rotationExemptHint')">
      <ToggleSwitch v-model="form.rotationExempt" />
    </FormField>
    <FormField :label="t('catalog.category.goodNeighbours')" :error="errors.field('neighbourIds')" full>
      <MultiSelect
        v-model="form.goodNeighbourIds"
        :options="otherCategories"
        :option-label="(c) => `${c.icon} ${c.name}`"
        option-value="id"
        display="chip"
        filter
      />
    </FormField>
    <FormField :label="t('catalog.category.badNeighbours')" :error="errors.field('badNeighbourIds')" full>
      <MultiSelect
        v-model="form.badNeighbourIds"
        :options="otherCategories"
        :option-label="(c) => `${c.icon} ${c.name}`"
        option-value="id"
        display="chip"
        filter
      />
    </FormField>
  </EditDialog>
</template>
