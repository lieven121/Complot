<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import SelectButton from 'primevue/selectbutton'
import { useI18n } from 'vue-i18n'
import type { ToolDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useQueryParam } from '@/shared/composables/useQueryParam'
import { todayIso } from '@/shared/dates'
import { useSession } from '@/shared/session'
import { useAllBeds } from '@/features/garden/queries'
import ToolDialog from './components/ToolDialog.vue'
import ToolEditor from './components/ToolEditor.vue'
import { useToolDemand, useTools } from './queries'
import { byKind, conditionShares, maintenanceDue, quantityOf, replacementDue, shortage, totalValue } from './tools'

const { t } = useI18n()
const { currency, date, month } = useFormat()
const { isEditor } = useSession()
const today = todayIso()
const thisYear = Number(today.slice(0, 4))

const tools = useTools()
const beds = useAllBeds()
const yearParam = useQueryParam('year')
const year = computed({
  get: () => Number(yearParam.value ?? thisYear),
  set: (y) => (yearParam.value = y === thisYear ? undefined : String(y)),
})
const years = [thisYear - 1, thisYear, thisYear + 1].map((y) => ({ value: y, label: String(y) }))
const demand = useToolDemand(year)

const list = computed(() => tools.data.value ?? [])
const demandOf = (tool: ToolDto) => demand.data.value?.find((d) => d.toolId === tool.id)
const bedLabel = (id: string) => beds.value.find((b) => b.id === id)?.code

const shortages = computed(() => list.value.filter((x) => shortage(x, demandOf(x)) > 0))
const maintenance = computed(() => list.value.filter((x) => maintenanceDue(x, today)))
const replacements = computed(() => list.value.filter((x) => replacementDue(x, today)))
const reused = computed(() => list.value.filter((x) => x.isReused))
const names = (items: ToolDto[]) => items.map((x) => x.name).join(', ')

const editor = ref<InstanceType<typeof ToolEditor>>()
const openId = ref<string>()
const opened = computed(() => list.value.find((x) => x.id === openId.value))
function edit(tool: ToolDto) {
  openId.value = undefined
  editor.value?.open(tool)
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('nav.tools') }}</h1>
      <span class="muted">{{ t('tools.summary', { n: list.length, value: currency(totalValue(list)) }) }}</span>
      <div class="actions">
        <span class="muted small">{{ t('tools.demandFor') }}</span>
        <SelectButton v-model="year" :options="years" option-label="label" option-value="value" :allow-empty="false" size="small" />
        <Button v-if="isEditor" :label="t('tools.new')" icon="pi pi-plus" @click="editor?.open()" />
      </div>
    </div>

    <div class="stats">
      <div class="card stat">
        <span class="label">{{ t('tools.shortages', { year }) }}</span>
        <span class="value" :class="shortages.length ? 'bad' : 'good'">{{ shortages.length }}</span>
        <span class="muted small">{{ names(shortages) || t('tools.covered') }}</span>
      </div>
      <div class="card stat">
        <span class="label">{{ t('tools.maintenanceDue') }}</span>
        <span class="value">{{ maintenance.length }}</span>
        <span class="muted small">{{ names(maintenance) || t('tools.nothingDue') }}</span>
      </div>
      <div class="card stat">
        <span class="label">{{ t('tools.replaceSoon') }}</span>
        <span class="value">{{ replacements.length }}</span>
        <span class="muted small">{{ names(replacements) || '—' }}</span>
      </div>
      <div class="card stat">
        <span class="label">{{ t('tools.reusedCount') }}</span>
        <span class="value">♻️ {{ reused.length }}</span>
        <span class="muted small">{{ names(reused) || '—' }}</span>
      </div>
    </div>

    <section v-for="[kind, items] in byKind(list)" :key="kind">
      <h3>{{ kind }}</h3>
      <div class="cards">
        <button v-for="tool in items" :key="tool.id" type="button" class="card tool" @click="openId = tool.id">
          <span class="top">
            <span class="icon" aria-hidden="true">{{ tool.icon }}</span>
            <span>
              <b>{{ tool.name }}</b>
              <span class="muted small block">
                {{ quantityOf(tool) }} {{ tool.unit }}<template v-if="tool.isReused"> · ♻️ {{ t('tools.reusedTag') }}</template>
              </span>
            </span>
          </span>
          <span class="bar" :aria-label="t('tools.conditionValue', { good: tool.qtyGood, repair: tool.qtyRepair, worn: tool.qtyWorn })">
            <i class="good" :style="{ width: `${conditionShares(tool).good}%` }" />
            <i class="repair" :style="{ width: `${conditionShares(tool).repair}%` }" />
            <i class="worn" :style="{ width: `${conditionShares(tool).worn}%` }" />
          </span>
          <span class="small cond">
            <span>{{ t('tools.goodN', { n: tool.qtyGood }) }}</span>
            <span v-if="tool.qtyRepair">{{ t('tools.repairN', { n: tool.qtyRepair }) }}</span>
            <span v-if="tool.qtyWorn">{{ t('tools.wornN', { n: tool.qtyWorn }) }}</span>
          </span>
          <span v-if="demandOf(tool)" class="need small" :class="{ short: shortage(tool, demandOf(tool)) }">
            {{ shortage(tool, demandOf(tool)) ? '⚠️' : '✅' }}
            {{ t('tools.peak', { n: demandOf(tool)!.quantity, month: month(demandOf(tool)!.month) }) }}
            <template v-if="shortage(tool, demandOf(tool))"> — {{ t('tools.shortBy', { n: shortage(tool, demandOf(tool)) }) }}</template>
          </span>
          <span class="muted small">
            <template v-if="tool.bedIds.length">📍 {{ tool.bedIds.map(bedLabel).filter(Boolean).join(', ') }} · </template>
            <span :class="{ due: maintenanceDue(tool, today) }">{{
              tool.nextMaintenance ? t('tools.nextCheck', { date: date(tool.nextMaintenance) }) : t('tools.noCare')
            }}</span>
          </span>
        </button>
      </div>
    </section>
    <p v-if="tools.isSuccess.value && !list.length" class="card muted">{{ t('tools.empty') }}</p>

    <ToolEditor ref="editor" />
    <ToolDialog
      v-if="opened"
      :tool="opened"
      :demand="demandOf(opened)"
      :bed-label="bedLabel"
      @close="openId = undefined"
      @edit="edit(opened)"
    />
  </div>
</template>

<style scoped>
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 12px;
  margin-bottom: 8px;
}
.stat {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.stat .label {
  font-size: 11.5px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--c-muted);
  font-weight: 700;
}
.stat .value {
  font-size: 26px;
  font-weight: 800;
}
.value.bad {
  color: var(--c-warn);
}
.value.good {
  color: var(--c-accent);
}
.cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 10px;
}
.tool {
  all: unset;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  gap: 7px;
  cursor: pointer;
  background: var(--c-panel);
  border: 1px solid var(--c-line);
  border-radius: var(--radius);
  padding: 12px 14px;
  box-shadow: var(--shadow);
}
.tool:hover,
.tool:focus-visible {
  border-color: var(--c-accent);
}
.top {
  display: flex;
  gap: 10px;
  align-items: center;
}
.icon {
  font-size: 26px;
}
.block {
  display: block;
}
.bar {
  display: flex;
  height: 8px;
  border-radius: 4px;
  overflow: hidden;
  background: var(--c-panel-2);
}
.bar .good {
  background: #5d9d6a;
}
.bar .repair {
  background: #e0a341;
}
.bar .worn {
  background: #c0603d;
}
.cond {
  display: flex;
  flex-wrap: wrap;
  gap: 2px 10px;
}
.need {
  padding: 4px 8px;
  border-radius: 8px;
  background: var(--c-accent-soft);
}
.need.short {
  background: var(--c-warn-soft);
}
.due {
  color: var(--c-warn);
  font-weight: 700;
}
</style>
