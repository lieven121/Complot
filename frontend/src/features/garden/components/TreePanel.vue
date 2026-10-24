<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import { useI18n } from 'vue-i18n'
import type { TaskDto, TreeDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import TaskList from '@/features/tasks/components/TaskList.vue'

const props = defineProps<{ tree: TreeDto; tasks: TaskDto[]; today: string; whereOf: (task: TaskDto) => string | undefined }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const { date, decimal, month, duration } = useFormat()
const age = computed(() => (props.tree.plantedOn ? Number(props.today.slice(0, 4)) - Number(props.tree.plantedOn.slice(0, 4)) : undefined))
const openTasks = computed(() => props.tasks.filter((task) => !task.done).slice(0, 6))
</script>

<template>
  <section :aria-label="tree.name">
    <div class="panel-head">
      <div>
        <h2>{{ tree.kind === 'shrub' ? '🌿' : '🌳' }} {{ tree.name }}</h2>
        <div class="muted small">{{ tree.cultivar }} · {{ t(`map.tree.kind.${tree.kind}`) }}</div>
      </div>
      <Button icon="pi pi-times" text rounded severity="secondary" class="close" :aria-label="t('common.close')" @click="emit('close')" />
    </div>
    <div class="kv">
      <span
        >{{ t('map.tree.crown') }} <b>⌀ {{ decimal(tree.crownDiameter) }} m</b></span
      >
      <span v-if="tree.plantedOn"
        >{{ t('map.tree.planted') }} <b>{{ date(tree.plantedOn) }}</b></span
      >
      <span v-if="age !== undefined"
        ><b>{{ t('common.years', { n: age }) }}</b></span
      >
    </div>

    <h3>{{ t('map.tree.actions') }}</h3>
    <div class="stack">
      <div v-for="(a, i) in tree.actions" :key="i" class="item">
        ✂️ {{ a.title }}<span class="end">{{ month(a.month) }} · {{ duration(a.estimatedMinutes) }}</span>
      </div>
      <span v-if="!tree.actions.length" class="muted small">—</span>
    </div>

    <h3>{{ t('map.bed.tasks') }}</h3>
    <TaskList :tasks="openTasks" :today="today" :where-of="whereOf" :empty-text="t('map.bed.noTasks')" />

    <slot name="log" />
  </section>
</template>
