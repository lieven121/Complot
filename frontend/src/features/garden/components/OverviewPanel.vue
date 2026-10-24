<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PlantingDto, PlantingWarningDto, SiteLayoutDto, TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import TaskList from '@/features/tasks/components/TaskList.vue'
import { addDays, IN_BED, phaseAt } from '../map'
import type { Crop } from './GardenMap.vue'
import WarningList from './WarningList.vue'

/** What happens on the site: beds in use, harvest now, the next two weeks of work and this year's checks. */
const props = defineProps<{
  siteName: string
  layout: SiteLayoutDto
  plantings: PlantingDto[]
  warnings: PlantingWarningDto[]
  tasks: TaskDto[]
  date: string
  today: string
  cropOf: (varietyId: string) => Crop | undefined
  whereOf: (task: TaskDto) => string | undefined
  harvested: string
}>()
const emit = defineEmits<{ select: [id: string] }>()

const { t } = useI18n()
const { date: formatDate, decimal } = useFormat()

const active = computed(() => props.plantings.map((p) => ({ planting: p, phase: phaseAt(p, props.date) })))
const bedsInUse = computed(() => new Set(active.value.filter((x) => x.phase && IN_BED.has(x.phase)).map((x) => x.planting.bedId)).size)
const area = computed(() => props.layout.beds.reduce((sum, b) => sum + b.area, 0))
const harvesting = computed(() => active.value.filter((x) => x.phase === 'harvest'))
const bedCode = (id: string) => props.layout.beds.find((b) => b.id === id)?.code
const year = computed(() => props.date.slice(0, 4))
const yearWarnings = computed(() => props.warnings.filter((w) => w.date.slice(0, 4) === year.value))
const upcoming = computed(() => props.tasks.filter((task) => !task.done && task.date <= addDays(props.today, 14)).slice(0, 8))
</script>

<template>
  <section>
    <div class="panel-head">
      <div>
        <h2>{{ t('map.overview.title', { site: siteName }) }}</h2>
        <div class="muted small">{{ t('map.overview.subtitle', { date: formatDate(date) }) }}</div>
      </div>
    </div>
    <div class="stats">
      <div class="card stat">
        <span class="l">{{ t('map.overview.bedsInUse') }}</span
        ><span class="v">{{ bedsInUse }}/{{ layout.beds.length }}</span>
      </div>
      <div class="card stat">
        <span class="l">{{ t('map.overview.area') }}</span
        ><span class="v">{{ decimal(area) }} m²</span>
      </div>
      <div class="card stat">
        <span class="l">{{ t('map.overview.warnings', { year }) }}</span>
        <span class="v" :class="{ warn: yearWarnings.some((w) => w.level === 'warning') }">{{
          yearWarnings.filter((w) => w.level === 'warning').length
        }}</span>
      </div>
    </div>

    <h3 class="with-link">
      {{ t('map.overview.tasks') }} <RouterLink :to="{ name: 'tasks' }">{{ t('map.overview.allTasks') }}</RouterLink>
    </h3>
    <TaskList :tasks="upcoming" :today="today" :where-of="whereOf" :empty-text="t('map.overview.noTasks')" />

    <h3>{{ t('map.overview.harvestNow') }}</h3>
    <div class="stack">
      <button v-for="x in harvesting" :key="x.planting.id" type="button" class="item link" @click="emit('select', x.planting.bedId)">
        {{ cropOf(x.planting.varietyId)?.icon }} {{ cropOf(x.planting.varietyId)?.name
        }}<span class="end">{{ bedCode(x.planting.bedId) }}</span>
      </button>
      <span v-if="!harvesting.length" class="muted small">{{ t('map.overview.nothingToHarvest') }}</span>
    </div>

    <h3>{{ t('map.overview.harvested', { year }) }}</h3>
    <p class="small">
      <b>{{ harvested || t('map.overview.nothingHarvested') }}</b>
    </p>

    <h3>{{ t('map.overview.checks', { year }) }}</h3>
    <WarningList :warnings="yearWarnings" :empty-text="t('map.overview.noIssues')" clickable @select="emit('select', $event)" />
  </section>
</template>

<style scoped>
.stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin: 12px 0;
}
.stat {
  display: flex;
  flex-direction: column;
  padding: 10px 12px;
}
.stat .l {
  font-size: 10.5px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--c-muted);
  font-weight: 700;
}
.stat .v {
  font-size: 20px;
  font-weight: 750;
}
.stat .v.warn {
  color: var(--c-warn);
}
.with-link {
  display: flex;
}
.with-link a {
  margin-left: auto;
  text-transform: none;
  letter-spacing: 0;
  color: var(--c-accent);
}
.link {
  font: inherit;
  cursor: pointer;
  width: 100%;
  text-align: left;
}
</style>
