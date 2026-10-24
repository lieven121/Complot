<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import { useI18n } from 'vue-i18n'
import type { TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { flag, useQueryParam } from '@/shared/composables/useQueryParam'
import { addDays, todayIso } from '@/shared/dates'
import { useLayout, useSites } from '@/features/garden/queries'
import ManualTaskEditor from './components/ManualTaskEditor.vue'
import PersonEditor from './components/PersonEditor.vue'
import TaskList from './components/TaskList.vue'
import { groupTasks, workload } from './grouping'
import { useEstimates, usePeople, useTasks } from './queries'

const NOBODY = 'none'

const { t } = useI18n()
const { duration, decimal } = useFormat()
const today = todayIso()

const sites = useSites()
const people = usePeople()
const estimates = useEstimates()

// Filters live in the URL (?site=…&person=…&done=1), so a link or reload keeps them.
const siteParam = useQueryParam('site')
const siteId = computed({ get: () => siteParam.value ?? sites.data.value?.[0]?.id, set: (id) => (siteParam.value = id) })
const person = useQueryParam('person')
const showDone = useQueryParam('done', flag)

const tasks = useTasks(computed(() => ({ siteId: siteId.value })))
const layout = useLayout(siteId)

const whereOf = (task: TaskDto) =>
  layout.data.value?.beds.find((b) => b.id === task.bedId)?.code ?? layout.data.value?.trees.find((x) => x.id === task.treeId)?.name

const personOptions = computed(() => [
  ...(people.data.value ?? []).map((p) => ({ value: p.id, label: p.name })),
  { value: NOBODY, label: t('tasks.nobody') },
])
const filtered = computed(() =>
  (tasks.data.value ?? []).filter(
    (task) =>
      (showDone.value || !task.done) && (!person.value || (person.value === NOBODY ? !task.assigneeId : task.assigneeId === person.value)),
  ),
)
const groups = computed(() => groupTasks(filtered.value, today))
const openCount = computed(() => (tasks.data.value ?? []).filter((task) => !task.done && task.date <= addDays(today, 6)).length)

/** Planned work for the coming 7 days, per person. */
const load = computed(() => workload(tasks.data.value ?? [], addDays(today, 6)))
const maxLoad = computed(() => Math.max(60, ...load.value.values()))

const taskEditor = ref<InstanceType<typeof ManualTaskEditor>>()
const personEditor = ref<InstanceType<typeof PersonEditor>>()
</script>

<template>
  <div class="page tasks-page">
    <div class="page-head">
      <h1>{{ t('nav.tasks') }}</h1>
      <span class="muted">{{ t('tasks.page.summary', { n: openCount }) }}</span>
      <div class="actions">
        <Button :label="t('tasks.manual.new')" icon="pi pi-plus" @click="taskEditor?.open()" />
      </div>
    </div>

    <div class="filters">
      <Select
        v-if="(sites.data.value?.length ?? 0) > 1"
        v-model="siteId"
        :options="sites.data.value ?? []"
        option-label="name"
        option-value="id"
        :aria-label="t('map.site')"
      />
      <Select
        v-model="person"
        :options="personOptions"
        option-label="label"
        option-value="value"
        show-clear
        :placeholder="t('tasks.page.everyone')"
        :aria-label="t('tasks.assignee')"
      />
      <label class="toggle">
        <ToggleSwitch v-model="showDone" input-id="show-done" />
        {{ t('tasks.page.showDone') }}
      </label>
    </div>

    <div class="layout">
      <div class="groups">
        <section v-for="g in groups" :key="g.group" :class="`group-${g.group}`">
          <h3>
            {{ t(`tasks.group.${g.group}`) }} <span class="count">{{ g.tasks.length }}</span>
          </h3>
          <TaskList :tasks="g.tasks" :today="today" :where-of="whereOf" />
        </section>
        <p v-if="tasks.isSuccess.value && !groups.length" class="card muted">{{ t('tasks.page.empty') }}</p>
      </div>

      <aside class="side">
        <section class="card">
          <h2>{{ t('tasks.page.workload') }}</h2>
          <div class="stack">
            <div v-for="p in people.data.value ?? []" :key="p.id" class="load">
              <button type="button" class="who" :title="t('tasks.people.edit')" @click="personEditor?.open(p)">
                <span class="dot" :style="{ background: p.color }" />{{ p.name }}
              </button>
              <span class="bar"><span :style="{ width: `${((load.get(p.id) ?? 0) / maxLoad) * 100}%`, background: p.color }" /></span>
              <span class="num small">{{ duration(load.get(p.id) ?? 0) }}</span>
            </div>
            <div v-if="load.get(undefined)" class="load">
              <span class="who muted">{{ t('tasks.nobody') }}</span>
              <span class="bar"><span :style="{ width: `${(load.get(undefined)! / maxLoad) * 100}%` }" /></span>
              <span class="num small">{{ duration(load.get(undefined)!) }}</span>
            </div>
          </div>
          <Button :label="t('tasks.people.new')" icon="pi pi-user-plus" text size="small" @click="personEditor?.open()" />
        </section>

        <section class="card">
          <h2>{{ t('tasks.page.estimates') }}</h2>
          <p class="muted small">{{ t('tasks.page.estimatesHelp') }}</p>
          <table>
            <thead>
              <tr>
                <th>{{ t('tasks.page.kind') }}</th>
                <th class="num">{{ t('tasks.page.base') }}</th>
                <th class="num">{{ t('tasks.page.factor') }}</th>
                <th class="num">{{ t('tasks.page.samples') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="f in estimates.data.value ?? []" :key="f.kind">
                <td>{{ t(`tasks.kindName.${f.kind}`) }}</td>
                <td class="num">{{ duration(f.baseMinutes) }}<span v-if="f.perSquareMeter" class="muted">/m²</span></td>
                <td class="num" :class="{ muted: f.samples < 2 }">× {{ decimal(f.factor) }}</td>
                <td class="num">{{ f.samples }}</td>
              </tr>
            </tbody>
          </table>
        </section>
      </aside>
    </div>

    <ManualTaskEditor ref="taskEditor" :site-id="siteId" :beds="layout.data.value?.beds" />
    <PersonEditor ref="personEditor" />
  </div>
</template>

<style scoped>
.filters {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 14px;
  align-items: center;
  margin-bottom: 6px;
}
.toggle {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}
.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 340px;
  gap: 24px;
  align-items: start;
}
.groups {
  max-width: 760px;
}
.count {
  margin-left: 4px;
  padding: 0 7px;
  border-radius: 10px;
  background: var(--c-line);
  color: var(--c-ink);
  font-weight: 600;
}
.group-overdue h3 {
  color: var(--c-warn);
}
.group-overdue .count {
  background: var(--c-warn-soft);
  color: var(--c-warn);
}
.group-today h3 {
  color: var(--c-accent);
}
.side {
  display: flex;
  flex-direction: column;
  gap: 14px;
  margin-top: 18px;
}
.load {
  display: grid;
  grid-template-columns: 90px 1fr 56px;
  gap: 8px;
  align-items: center;
}
.who {
  all: unset;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.who:hover,
.who:focus-visible {
  text-decoration: underline;
}
.dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex: none;
}
.bar {
  height: 8px;
  border-radius: 4px;
  background: var(--c-panel-2);
  border: 1px solid var(--c-line);
  overflow: hidden;
}
.bar > span {
  display: block;
  height: 100%;
  background: var(--c-muted);
}
table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}
th,
td {
  padding: 5px 4px;
  border-bottom: 1px solid var(--c-line);
  text-align: left;
}
th.num,
td.num {
  text-align: right;
}
th {
  font-size: 11.5px;
  color: var(--c-muted);
  font-weight: 600;
}
@media (max-width: 1000px) {
  .layout {
    grid-template-columns: 1fr;
  }
  .side {
    margin-top: 0;
  }
}
</style>
