<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import SelectButton from 'primevue/selectbutton'
import { useI18n } from 'vue-i18n'
import type { CostPeriodDto, InvoiceDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useQueryParam } from '@/shared/composables/useQueryParam'
import { useSession } from '@/shared/session'
import CostCategoryEditor from './components/CostCategoryEditor.vue'
import CostChart from './components/CostChart.vue'
import InvoiceEditor from './components/InvoiceEditor.vue'
import { type Grouping, periodLabel } from './costs'
import { useCostCategories, useCostReport, useInvoices } from './queries'

const { t } = useI18n()
const { currency, date, decimal, monthNames } = useFormat()
const { isEditor, me } = useSession()

// Year ("all" = every year) and grouping live in the URL.
const yearParam = useQueryParam('year')
const year = computed(() => (yearParam.value && yearParam.value !== 'all' ? Number(yearParam.value) : undefined))
const groupingParam = useQueryParam('by')
const grouping = computed<Grouping>({
  get: () => (groupingParam.value === 'year' || groupingParam.value === 'quarter' ? groupingParam.value : year.value ? 'month' : 'year'),
  set: (g) => (groupingParam.value = g),
})

const categories = useCostCategories()
const report = useCostReport(year, grouping)
const invoices = useInvoices(year)
const r = computed(() => report.data.value)
const categoryById = computed(() => new Map((categories.data.value ?? []).map((c) => [c.id, c])))

const yearOptions = computed(() => [
  { value: 'all', label: t('costs.allYears') },
  ...(r.value?.years ?? []).map((y) => ({ value: String(y), label: String(y) })),
])
const groupingOptions = computed(() => (['year', 'quarter', 'month'] as const).map((value) => ({ value, label: t(`costs.by.${value}`) })))

const labelOf = (p: CostPeriodDto) =>
  periodLabel(
    p,
    grouping.value,
    (q, y) => t('costs.quarter', { q, y }),
    (m) => monthNames.value[m - 1] ?? String(m),
  )
const share = (value: number) => (r.value?.total ? (value / r.value.total) * 100 : 0)
const thisYear = new Date().getFullYear()
const ytdChange = computed(() =>
  r.value?.yearToDatePreviousYear ? Math.round((r.value.yearToDate / r.value.yearToDatePreviousYear - 1) * 100) : undefined,
)
const perSquareMeter = computed(() => (r.value?.bedArea ? r.value.total / r.value.bedArea : undefined))

const invoiceEditor = ref<InstanceType<typeof InvoiceEditor>>()
const categoryEditor = ref<InstanceType<typeof CostCategoryEditor>>()
const openInvoice = (invoice?: InvoiceDto) => invoiceEditor.value?.open(invoice)
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ t('nav.costs') }}</h1>
      <div class="actions">
        <SelectButton
          :model-value="yearParam ?? 'all'"
          :options="yearOptions"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          size="small"
          @update:model-value="(v: string) => ((yearParam = v === 'all' ? undefined : v), (groupingParam = undefined))"
        />
        <Button v-if="isEditor" :label="t('costs.invoice.new')" icon="pi pi-plus" @click="openInvoice()" />
      </div>
    </div>

    <p v-if="me.isSuccess.value && !isEditor" class="card muted">{{ t('costs.editorsOnly') }}</p>
    <template v-else-if="r">
      <div class="stats">
        <div class="card stat">
          <span class="label">{{ t('costs.total', { period: year ?? t('costs.allYearsLower') }) }}</span>
          <span class="value">{{ currency(r.total) }}</span>
          <span class="muted small">{{ t('costs.invoices', { n: r.invoiceCount }) }}</span>
        </div>
        <div class="card stat">
          <span class="label">{{ t('costs.yearToDate', { year: thisYear }) }}</span>
          <span class="value">{{ currency(r.yearToDate) }}</span>
          <span v-if="ytdChange !== undefined" class="muted small">
            {{ t('costs.vsLastYear', { arrow: ytdChange >= 0 ? '▲' : '▼', pct: Math.abs(ytdChange), year: thisYear - 1 }) }}
          </span>
        </div>
        <div class="card stat">
          <span class="label">{{ t('costs.perMonth') }}</span>
          <span class="value">{{ currency(r.averagePerMonth) }}</span>
          <span class="muted small">{{ t('costs.overMonths', { n: r.months }) }}</span>
        </div>
        <div class="card stat">
          <span class="label">{{ t('costs.perSquareMeter') }}</span>
          <span class="value">{{ perSquareMeter !== undefined ? currency(perSquareMeter) : '—' }}</span>
          <span class="muted small">{{ t('costs.bedArea', { area: decimal(r.bedArea) }) }}</span>
        </div>
      </div>

      <section class="card chart-card">
        <div class="row">
          <h2>{{ t('costs.overTime') }}</h2>
          <SelectButton
            v-model="grouping"
            :options="groupingOptions"
            option-label="label"
            option-value="value"
            :allow-empty="false"
            size="small"
            class="push"
          />
        </div>
        <CostChart :periods="r.periods" :categories="categories.data.value ?? []" :label-of="labelOf" />
        <div class="legend small">
          <span v-for="c in categories.data.value ?? []" :key="c.id"
            ><span class="dot" :style="{ background: c.color }" /> {{ c.name }}</span
          >
        </div>
      </section>

      <div class="grid-2">
        <section class="card">
          <div class="row">
            <h2>{{ t('costs.byCategory') }}</h2>
            <Button
              v-if="isEditor"
              icon="pi pi-plus"
              text
              rounded
              size="small"
              class="push"
              :aria-label="t('costs.category.new')"
              @click="categoryEditor?.open()"
            />
          </div>
          <div class="stack">
            <button v-for="c in categories.data.value ?? []" :key="c.id" type="button" class="cat" @click="categoryEditor?.open(c)">
              <span class="cat-row">
                <span><span class="dot" :style="{ background: c.color }" /> {{ c.name }}</span>
                <span class="num"
                  ><b>{{ currency(r.byCategory[c.id] ?? 0) }}</b>
                  <span class="muted">{{ Math.round(share(r.byCategory[c.id] ?? 0)) }}%</span></span
                >
              </span>
              <span class="bar"><i :style="{ width: `${share(r.byCategory[c.id] ?? 0)}%`, background: c.color }" /></span>
            </button>
          </div>
        </section>
        <section class="card table-card">
          <h2>{{ t('costs.categoryByYear') }}</h2>
          <table>
            <thead>
              <tr>
                <th>{{ t('costs.category.one') }}</th>
                <th v-for="y in r.years" :key="y" class="num">{{ y }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="c in categories.data.value ?? []" :key="c.id">
                <td><span class="dot" :style="{ background: c.color }" /> {{ c.name }}</td>
                <td v-for="y in r.years" :key="y" class="num">
                  {{ r.byCategoryAndYear[c.id]?.[y] ? currency(r.byCategoryAndYear[c.id]![y]!) : '—' }}
                </td>
              </tr>
              <tr class="total">
                <td>{{ t('costs.totalRow') }}</td>
                <td v-for="y in r.years" :key="y" class="num">
                  {{ currency(Object.values(r.byCategoryAndYear).reduce((sum, perYear) => sum + (perYear[y] ?? 0), 0)) }}
                </td>
              </tr>
            </tbody>
          </table>
        </section>
      </div>

      <h3>{{ t('costs.invoiceList') }}</h3>
      <div class="card table-card">
        <table class="invoices">
          <thead>
            <tr>
              <th>{{ t('costs.invoice.date') }}</th>
              <th>{{ t('costs.invoice.supplier') }}</th>
              <th class="hide-sm">{{ t('costs.invoice.number') }}</th>
              <th class="hide-sm">{{ t('costs.invoice.split') }}</th>
              <th aria-label="📎" />
              <th class="num">{{ t('costs.invoice.total') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="i in invoices.data.value ?? []"
              :key="i.id"
              class="click"
              tabindex="0"
              @click="openInvoice(i)"
              @keydown.enter="openInvoice(i)"
            >
              <td>{{ date(i.date) }}</td>
              <td>{{ i.supplier }}</td>
              <td class="muted small hide-sm">{{ i.number }}</td>
              <td class="hide-sm">
                <span class="bar split">
                  <i
                    v-for="(l, k) in i.lines"
                    :key="k"
                    :style="{ width: `${(l.amount / i.total) * 100}%`, background: categoryById.get(l.categoryId)?.color }"
                    :title="`${categoryById.get(l.categoryId)?.name}: ${currency(l.amount)}`"
                  />
                </span>
              </td>
              <td>{{ i.hasAttachment ? '📎' : '' }}</td>
              <td class="num">
                <b>{{ currency(i.total) }}</b>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-if="invoices.isSuccess.value && !invoices.data.value?.length" class="muted small">{{ t('costs.noInvoices') }}</p>
      </div>
    </template>

    <InvoiceEditor ref="invoiceEditor" />
    <CostCategoryEditor ref="categoryEditor" />
  </div>
</template>

<style scoped>
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 12px;
  margin-bottom: 14px;
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
  font-size: 24px;
  font-weight: 800;
}
.chart-card {
  margin-bottom: 14px;
}
.row h2 {
  margin: 0;
}
.push {
  margin-left: auto;
}
.legend {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 14px;
  margin-top: 6px;
}
.dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  vertical-align: -1px;
}
.cat {
  all: unset;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.cat:focus-visible {
  outline: 2px solid var(--c-accent);
  border-radius: 4px;
}
.cat-row {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  font-size: 13px;
}
.bar {
  display: flex;
  height: 8px;
  border-radius: 4px;
  overflow: hidden;
  background: var(--c-panel-2);
}
.bar.split {
  min-width: 140px;
}
.table-card {
  overflow-x: auto;
}
table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}
th,
td {
  padding: 6px 6px;
  border-bottom: 1px solid var(--c-line);
  text-align: left;
}
th {
  font-size: 11.5px;
  color: var(--c-muted);
  font-weight: 600;
}
th.num,
td.num {
  text-align: right;
  white-space: nowrap;
}
.total td {
  font-weight: 700;
}
.click {
  cursor: pointer;
}
.click:hover,
.click:focus-visible {
  background: var(--c-panel-2);
}
@media (max-width: 700px) {
  .hide-sm {
    display: none;
  }
}
</style>
