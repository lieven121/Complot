<script setup lang="ts">
import { computed, ref } from 'vue'
import DatePicker from 'primevue/datepicker'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import Textarea from 'primevue/textarea'
import { useI18n } from 'vue-i18n'
import type { NoteDto, NoteInput, NoteTag } from '@/shared/api'
import { useEditor } from '@/shared/composables/useEditor'
import { fromIsoDate, todayIso, toIsoDate } from '@/shared/dates'
import EditDialog from '@/shared/ui/EditDialog.vue'
import FormField from '@/shared/ui/FormField.vue'
import InputNumber from '@/shared/ui/NumberInput.vue'
import { usePeople } from '@/features/tasks/queries'
import { NOTE_TAGS, TAG_ICON } from '../notes'
import { useDeleteNote, useSaveNote } from '../queries'

/** Log entry for a bed or tree: what was seen or harvested, optionally with a photo. */
const props = defineProps<{
  siteId: string
  bedId?: string
  treeId?: string
  /** Crops in the bed, so a note can be linked to one (shows up on the variety too). */
  plantings?: { id: string; name: string }[]
  /** Date of new notes: the date selected on the timeline. */
  date: string
}>()

const { t } = useI18n()
const people = usePeople()
const photo = ref<File>()
const saveNote = useSaveNote()
const editor = useEditor({
  toInput: (n?: NoteDto): NoteInput => ({
    siteId: props.siteId,
    bedId: props.bedId,
    treeId: props.treeId,
    plantingId: n?.plantingId,
    personId: n?.personId,
    date: n?.date ?? props.date,
    tag: n?.tag ?? 'observation',
    text: n?.text ?? '',
    harvestQuantity: n?.harvestQuantity,
    harvestUnit: n?.harvestUnit ?? 'kg',
  }),
  save: {
    mutateAsync: (v: { id?: string; input: NoteInput }) => saveNote.mutateAsync({ ...v, photo: photo.value }),
    isPending: saveNote.isPending,
  },
  remove: useDeleteNote(),
  nameOf: (n) => n.text.slice(0, 40),
})
const { form, errors } = editor

const tags = computed(() => NOTE_TAGS.map((tag) => ({ value: tag, label: `${TAG_ICON[tag]} ${t(`notes.tag.${tag}`)}` })))
const isHarvest = computed(() => form.value.tag === 'harvest')
const date = computed({ get: () => fromIsoDate(form.value.date), set: (d) => (form.value.date = toIsoDate(d) ?? todayIso()) })

/** Only a harvest note keeps a quantity. */
function setTag(tag: NoteTag) {
  form.value.tag = tag
  if (tag !== 'harvest') form.value.harvestQuantity = undefined
}
function submit() {
  if (!isHarvest.value) form.value.harvestUnit = undefined
  return editor.submit()
}
const open = (note?: NoteDto, tag?: NoteTag) => {
  photo.value = undefined
  editor.open(note)
  if (tag) setTag(tag)
}
defineExpose({ open })
</script>

<template>
  <EditDialog
    v-model:visible="editor.visible.value"
    :title="editor.isNew.value ? t('notes.new') : t('notes.edit')"
    :saving="editor.saving.value"
    :can-delete="!editor.isNew.value"
    width="560px"
    @save="submit"
    @delete="editor.remove"
  >
    <FormField :label="t('notes.kind')" full>
      <SelectButton
        :model-value="form.tag"
        :options="tags"
        option-label="label"
        option-value="value"
        :allow-empty="false"
        class="tags"
        @update:model-value="setTag"
      />
    </FormField>
    <FormField :label="t('notes.date')" for="n-date">
      <DatePicker v-model="date" input-id="n-date" show-icon date-format="dd/mm/yy" />
    </FormField>
    <FormField :label="t('notes.person')" for="n-person">
      <Select
        v-model="form.personId"
        input-id="n-person"
        :options="people.data.value ?? []"
        option-label="name"
        option-value="id"
        show-clear
        :placeholder="t('tasks.nobody')"
      />
    </FormField>
    <FormField v-if="plantings?.length" :label="t('notes.crop')" for="n-crop" :error="errors.field('plantingId')" full>
      <Select
        v-model="form.plantingId"
        input-id="n-crop"
        :options="plantings"
        option-label="name"
        option-value="id"
        show-clear
        :placeholder="t('notes.wholeBed')"
      />
    </FormField>
    <template v-if="isHarvest">
      <FormField :label="t('notes.quantity')" for="n-qty" :error="errors.field('harvestQuantity')">
        <InputNumber
          v-model="form.harvestQuantity"
          input-id="n-qty"
          :min="0"
          :max-fraction-digits="2"
          :invalid="!!errors.field('harvestQuantity')"
        />
      </FormField>
      <FormField :label="t('notes.unit')" for="n-unit" :hint="t('notes.unitHint')" :error="errors.field('harvestUnit')">
        <InputText id="n-unit" v-model="form.harvestUnit" :invalid="!!errors.field('harvestUnit')" />
      </FormField>
    </template>
    <FormField :label="t('notes.text')" for="n-text" :error="errors.field('text')" full>
      <Textarea id="n-text" v-model="form.text" rows="3" auto-resize autofocus :invalid="!!errors.field('text')" />
    </FormField>
    <FormField :label="t('notes.photo')" for="n-photo" :hint="t('notes.photoHint')" :error="errors.field('file')" full>
      <input
        id="n-photo"
        type="file"
        accept="image/jpeg,image/png,image/webp,image/heic"
        @change="photo = ($event.target as HTMLInputElement).files?.[0]"
      />
    </FormField>
  </EditDialog>
</template>

<style scoped>
.tags {
  flex-wrap: wrap;
}
</style>
