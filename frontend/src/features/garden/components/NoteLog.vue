<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import { useI18n } from 'vue-i18n'
import type { NoteDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { usePersonById } from '@/features/tasks/queries'
import { harvestSummary, TAG_ICON } from '../notes'
import { notePhotoUrl, useNotes } from '../queries'
import NoteEditor from './NoteEditor.vue'

/** Garden log of one bed or tree: notes newest first, harvest total of the year, and quick add buttons. */
const props = defineProps<{
  siteId: string
  bedId?: string
  treeId?: string
  plantings?: { id: string; name: string }[]
  date: string
}>()

const { t } = useI18n()
const { date: formatDate, decimal } = useFormat()
const personById = usePersonById()
const notes = useNotes(computed(() => (props.bedId ? { bedId: props.bedId } : { treeId: props.treeId })))
const editor = ref<InstanceType<typeof NoteEditor>>()
const showAll = ref(false)

const year = computed(() => props.date.slice(0, 4))
const harvested = computed(() =>
  harvestSummary(
    (notes.data.value ?? []).filter((n) => n.date.startsWith(year.value)),
    decimal,
  ),
)
const shown = computed(() => (showAll.value ? notes.data.value : notes.data.value?.slice(0, 5)) ?? [])
const cropName = (n: NoteDto) => props.plantings?.find((p) => p.id === n.plantingId)?.name
</script>

<template>
  <h3>{{ t('notes.title') }}</h3>
  <div class="actions">
    <Button :label="t('notes.add')" icon="pi pi-plus" size="small" severity="secondary" @click="editor?.open()" />
    <Button size="small" severity="secondary" outlined @click="editor?.open(undefined, 'harvest')">
      {{ TAG_ICON.harvest }} {{ t('notes.addHarvest') }}
    </Button>
  </div>
  <p v-if="harvested" class="small">
    {{ t('notes.harvested', { year }) }} <b>{{ harvested }}</b>
  </p>
  <div class="stack">
    <button v-for="n in shown" :key="n.id" type="button" class="item note" :class="`tag-${n.tag}`" @click="editor?.open(n)">
      <span class="icon" :title="t(`notes.tag.${n.tag}`)">{{ TAG_ICON[n.tag] }}</span>
      <span class="body">
        <span class="meta muted small">
          {{ formatDate(n.date) }}
          <template v-if="cropName(n)"> · {{ cropName(n) }}</template>
          <template v-if="n.personId && personById.get(n.personId)"> · {{ personById.get(n.personId)!.name }}</template>
        </span>
        <span>
          <b v-if="n.harvestQuantity">{{ decimal(n.harvestQuantity) }} {{ n.harvestUnit }} · </b>{{ n.text }}
        </span>
        <img v-if="n.hasPhoto" :src="notePhotoUrl(n.id)" :alt="t('notes.photo')" loading="lazy" />
      </span>
    </button>
    <span v-if="notes.isSuccess.value && !notes.data.value?.length" class="muted small">{{ t('notes.empty') }}</span>
    <Button
      v-if="!showAll && (notes.data.value?.length ?? 0) > shown.length"
      :label="t('notes.showAll', { n: notes.data.value!.length })"
      text
      size="small"
      @click="showAll = true"
    />
  </div>
  <NoteEditor ref="editor" :site-id="siteId" :bed-id="bedId" :tree-id="treeId" :plantings="plantings" :date="date" />
</template>

<style scoped>
.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 8px;
}
.note {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  width: 100%;
  text-align: left;
  font: inherit;
  color: inherit;
  cursor: pointer;
}
.note.tag-disease,
.note.tag-pest {
  border-left: 3px solid var(--p-red-400);
}
.note.tag-harvest {
  border-left: 3px solid var(--p-green-500);
}
.icon {
  font-size: 1.1rem;
}
.body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
  white-space: pre-line;
}
img {
  margin-top: 4px;
  max-width: 100%;
  max-height: 180px;
  border-radius: 6px;
  object-fit: cover;
}
</style>
