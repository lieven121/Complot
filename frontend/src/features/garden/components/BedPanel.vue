<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import { useConfirm } from 'primevue/useconfirm'
import { useI18n } from 'vue-i18n'
import type { BedDto, PlantingDto, PlantingWarningDto, TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import TaskList from '@/features/tasks/components/TaskList.vue'
import { phaseAt } from '../map'
import { useDeletePlanting } from '../queries'
import BedGantt from './BedGantt.vue'
import type { Crop } from './GardenMap.vue'
import PlantingForm from './PlantingForm.vue'
import WarningList from './WarningList.vue'

const props = defineProps<{
  bed: BedDto
  plotName?: string
  plantings: PlantingDto[]
  warnings: PlantingWarningDto[]
  tasks: TaskDto[]
  period: { from: string; to: string; today: string }
  date: string
  cropOf: (varietyId: string) => Crop | undefined
  whereOf: (task: TaskDto) => string | undefined
  readonly?: boolean
}>()
const emit = defineEmits<{ close: []; date: [iso: string] }>()

const { t } = useI18n()
const { date: formatDate, decimal } = useFormat()
const confirm = useConfirm()
const notify = useNotify()
const remove = useDeletePlanting()
const planning = ref(false)

const now = computed(() =>
  props.plantings.map((p) => ({ planting: p, phase: phaseAt(p, props.date), crop: props.cropOf(p.varietyId) })).filter((x) => x.phase),
)
const warnedIds = computed(() => new Set(props.warnings.filter((w) => w.level === 'warning').map((w) => w.plantingId)))
const openTasks = computed(() => props.tasks.filter((task) => !task.done).slice(0, 6))

function deletePlanting(p: PlantingDto) {
  confirm.require({
    header: t('common.confirmDeleteTitle'),
    message: t('map.bed.confirmRemovePlanting', { name: props.cropOf(p.varietyId)?.name ?? '' }),
    acceptProps: { label: t('common.delete'), severity: 'danger' },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', outlined: true },
    accept: async () => {
      try {
        await remove.mutateAsync(p.id)
        notify.success('map.bed.plantingRemoved')
      } catch (error) {
        notify.error(error)
      }
    },
  })
}
</script>

<template>
  <section :aria-label="t('map.bed.title', { code: bed.code })">
    <div class="panel-head">
      <div>
        <h2>{{ t('map.bed.title', { code: bed.code }) }}</h2>
        <div class="muted small">{{ plotName }}</div>
      </div>
      <Button icon="pi pi-times" text rounded severity="secondary" class="close" :aria-label="t('common.close')" @click="emit('close')" />
    </div>
    <div class="kv">
      <span
        >{{ t('map.bed.size') }}
        <b>{{ decimal(bed.shape.width) }} × {{ decimal(bed.shape.height) }} m ({{ decimal(bed.area) }} m²)</b></span
      >
      <span
        ><b>{{ t(`sun.${bed.sun}`) }}</b></span
      >
      <span v-if="bed.soil"
        >{{ t('map.bed.soil') }} <b>{{ bed.soil }}</b></span
      >
    </div>

    <h3>{{ t('map.bed.on', { date: formatDate(date) }) }}</h3>
    <div class="stack">
      <RouterLink v-for="x in now" :key="x.planting.id" :to="{ name: 'variety', params: { id: x.planting.varietyId } }" class="item crop">
        {{ x.crop?.icon }} <b>{{ x.crop?.name }}</b>
        <span v-if="x.planting.share < 1" class="muted">· {{ Math.round(x.planting.share * 100) }}%</span>
        <span class="end"
          ><span class="phase" :class="x.phase">{{ t(`phase.${x.phase}`) }}</span></span
        >
      </RouterLink>
      <span v-if="!now.length" class="muted small">{{ t('map.bed.empty') }}</span>
    </div>

    <h3>{{ t('map.bed.timeline') }}</h3>
    <BedGantt
      :plantings="plantings"
      :from="period.from"
      :to="period.to"
      :today="period.today"
      :date="date"
      :crop-of="cropOf"
      :warned-ids="warnedIds"
      @date="emit('date', $event)"
    />
    <p class="muted small">{{ t('map.bed.ganttHelp') }}</p>

    <PlantingForm v-if="planning" :bed-id="bed.id" :bed-code="bed.code" :date="date" @done="planning = false" />
    <Button v-else-if="!readonly" :label="t('map.plan.open')" icon="pi pi-plus" @click="planning = true" />

    <h3>{{ t('map.bed.tasks') }}</h3>
    <TaskList :tasks="openTasks" :today="period.today" :where-of="whereOf" :empty-text="t('map.bed.noTasks')" :readonly="readonly" />

    <slot name="log" />

    <h3>{{ t('map.bed.checks') }}</h3>
    <WarningList :warnings="warnings" :empty-text="t('map.bed.noChecks')" />

    <h3>{{ t('map.bed.plantings') }}</h3>
    <div class="stack">
      <div v-for="p in plantings" :key="p.id" class="item">
        {{ cropOf(p.varietyId)?.icon }} {{ cropOf(p.varietyId)?.name }}
        <span class="muted small">{{ formatDate(p.sowOn) }} → {{ p.clearOn ? formatDate(p.clearOn) : '∞' }}</span>
        <span class="end">
          <template v-if="p.sowOn > period.today && !readonly">
            <span class="planned">{{ t('map.bed.planned') }}</span>
            <Button
              icon="pi pi-trash"
              text
              rounded
              size="small"
              severity="secondary"
              :aria-label="t('common.delete')"
              @click="deletePlanting(p)"
            />
          </template>
        </span>
      </div>
    </div>
  </section>
</template>

<style scoped>
.crop {
  color: inherit;
  text-decoration: none;
}
.phase {
  font-size: 11px;
  padding: 1px 7px;
  border-radius: 999px;
  font-weight: 700;
  background: #eee;
}
.phase.young {
  background: #e7f2df;
  color: #4c7a33;
}
.phase.growing {
  background: #d7ead0;
  color: #2f6a2f;
}
.phase.harvest {
  background: #f6e2b4;
  color: #8a5b00;
}
.phase.clearing {
  background: #ece4d6;
  color: #6d5d40;
}
.phase.indoor {
  background: var(--c-info-soft);
  color: var(--c-info);
}
.phase.dormant {
  background: #e5e8ec;
  color: #56606b;
}
.planned {
  font-size: 11px;
  font-weight: 700;
  color: var(--c-info);
}
</style>
