<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import { useI18n } from 'vue-i18n'
import type { TaskDto, TreeDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import TaskList from '@/features/tasks/components/TaskList.vue'
import { useCatalogLookups } from '@/features/catalog/queries'
import { treeCare } from '../map'

const props = defineProps<{
  tree: TreeDto
  tasks: TaskDto[]
  today: string
  whereOf: (task: TaskDto) => string | undefined
  readonly?: boolean
}>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const { date, decimal, month, duration } = useFormat()
const age = computed(() => (props.tree.plantedOn ? Number(props.today.slice(0, 4)) - Number(props.tree.plantedOn.slice(0, 4)) : undefined))
const openTasks = computed(() => props.tasks.filter((task) => !task.done).slice(0, 6))
const { varietyById } = useCatalogLookups()
const variety = computed(() => (props.tree.varietyId ? varietyById.value.get(props.tree.varietyId) : undefined))
const care = computed(() => treeCare(props.tree, variety.value))
/** The variety's pollination applies when it has one. */
const pollination = computed(() => variety.value?.pollination ?? props.tree.pollination)
</script>

<template>
  <section :aria-label="tree.name">
    <div class="panel-head">
      <div>
        <h2>{{ tree.kind === 'shrub' ? '🌿' : '🌳' }} {{ tree.name }}</h2>
        <div class="muted small">
          <template v-if="variety">{{ [variety.name, variety.cultivar].filter(Boolean).join(' ') }} · </template>
          <template v-if="tree.cultivar">{{ tree.cultivar }} · </template>{{ t(`map.tree.kind.${tree.kind}`) }}
        </div>
      </div>
      <Button icon="pi pi-times" text rounded severity="secondary" class="close" :aria-label="t('common.close')" @click="emit('close')" />
    </div>
    <div class="kv">
      <span
        >{{ t('map.tree.crown') }}
        <b v-if="tree.outline === 'rect'">{{ decimal(tree.crownWidth) }} × {{ decimal(tree.crownDepth) }} m</b>
        <b v-else>⌀ {{ decimal(tree.crownWidth) }} m</b></span
      >
      <span v-if="tree.plantedOn"
        >{{ t('map.tree.planted') }} <b>{{ date(tree.plantedOn) }}</b></span
      >
      <span v-if="age !== undefined"
        ><b>{{ t('common.years', { n: age }) }}</b></span
      >
    </div>

    <template v-if="pollination">
      <h3>{{ t('map.tree.pollination') }}</h3>
      <div class="muted small">
        {{
          t('pollination.summary', {
            need: t(`pollination.need.${pollination.need}`),
            group: pollination.group,
            range: decimal(pollination.rangeMeters),
          })
        }}
      </div>
      <div v-if="tree.pollinators?.length" class="stack">
        <span class="small">{{ t('map.tree.pollinatedBy') }}</span>
        <div v-for="p in tree.pollinators" :key="p.id" class="item">
          🐝 {{ t('map.tree.pollinatorAt', { name: p.name, distance: decimal(p.distance) }) }}
        </div>
      </div>
      <div v-else-if="tree.pollinators" class="warn small" role="alert">
        ❗
        {{
          t(pollination.need === 'female' ? 'map.tree.noMale' : 'map.tree.noPollinator', {
            group: pollination.group,
            range: decimal(pollination.rangeMeters),
          })
        }}
      </div>
    </template>

    <h3>{{ t('map.tree.actions') }}</h3>
    <div class="stack">
      <div v-for="({ action: a, inherited }, i) in care" :key="i" class="item">
        ✂️ {{ a.title }}<span v-if="inherited" class="muted small"> · {{ t('map.tree.fromVariety') }}</span
        ><span class="end">{{ month(a.month) }} · {{ duration(a.estimatedMinutes) }}</span>
      </div>
      <span v-if="!care.length" class="muted small">—</span>
    </div>

    <h3>{{ t('map.bed.tasks') }}</h3>
    <TaskList :tasks="openTasks" :today="today" :where-of="whereOf" :empty-text="t('map.bed.noTasks')" :readonly="readonly" />

    <slot name="log" />
  </section>
</template>

<style scoped>
.warn {
  color: var(--c-warn);
  font-weight: 600;
}
</style>
