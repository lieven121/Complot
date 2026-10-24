<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import { useI18n } from 'vue-i18n'
import type { ToolDemandDto, ToolDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import { fromIsoDate, todayIso, toIsoDate } from '@/shared/dates'
import { useSession } from '@/shared/session'
import InputNumber from '@/shared/ui/NumberInput.vue'
import { useVarieties } from '@/features/catalog/queries'
import { useAddToolLog, useDeleteToolLog } from '../queries'
import { shortage } from '../tools'

/** A tool's details, who needs it, and its log (repairs, maintenance, costs). */
const props = defineProps<{ tool: ToolDto; demand?: ToolDemandDto; bedLabel: (id: string) => string | undefined }>()
const emit = defineEmits<{ close: []; edit: [] }>()

const { t } = useI18n()
const { date, currency, monthLong, duration } = useFormat()
const notify = useNotify()
const { isEditor } = useSession()
const varieties = useVarieties()
const addLog = useAddToolLog()
const deleteLog = useDeleteToolLog()
const visible = ref(true)

const neededBy = computed(() => (varieties.data.value ?? []).filter((v) => v.toolNeeds.some((n) => n.toolId === props.tool.id)))
const short = computed(() => shortage(props.tool, props.demand))

const entry = ref({
  date: todayIso(),
  text: '',
  cost: undefined as number | undefined,
  isMaintenance: !!props.tool.maintenanceIntervalMonths,
})
const entryDate = computed({ get: () => fromIsoDate(entry.value.date), set: (d) => (entry.value.date = toIsoDate(d) ?? todayIso()) })

async function saveEntry() {
  try {
    await addLog.mutateAsync({ id: props.tool.id, input: { ...entry.value, text: entry.value.text || undefined } })
    notify.success('common.saved')
    entry.value = { ...entry.value, text: '', cost: undefined }
  } catch (error) {
    notify.error(error)
  }
}

async function removeEntry(entryId: string) {
  try {
    await deleteLog.mutateAsync({ id: props.tool.id, entryId })
  } catch (error) {
    notify.error(error)
  }
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :style="{ width: '640px' }"
    :breakpoints="{ '700px': '96vw' }"
    :draggable="false"
    @after-hide="emit('close')"
  >
    <template #header>
      <div class="head">
        <span class="icon" aria-hidden="true">{{ tool.icon }}</span>
        <div>
          <h2>{{ tool.name }}</h2>
          <div class="muted small">{{ tool.kind }} · {{ tool.qtyGood + tool.qtyRepair + tool.qtyWorn }} {{ tool.unit }}</div>
        </div>
      </div>
    </template>

    <div class="kv">
      <span
        >{{ t('tools.condition') }}
        <b>{{ t('tools.conditionValue', { good: tool.qtyGood, repair: tool.qtyRepair, worn: tool.qtyWorn }) }}</b></span
      >
      <span v-if="tool.material"
        >{{ t('tools.material') }} <b>{{ tool.isReused ? '♻️ ' : '' }}{{ tool.material }}</b></span
      >
      <span v-if="tool.boughtOn"
        >{{ t('tools.boughtOn') }} <b>{{ date(tool.boughtOn) }}</b></span
      >
      <span v-if="tool.unitPrice != null"
        >{{ t('tools.unitPrice') }} <b>{{ tool.unitPrice ? currency(tool.unitPrice) : t('tools.free') }}</b></span
      >
      <span v-if="tool.supplierName"
        >{{ t('tools.supplier') }} <b>{{ tool.supplierName }}</b></span
      >
      <span v-if="tool.replaceBy"
        >{{ t('tools.replaceBy') }} <b>{{ date(tool.replaceBy) }}</b></span
      >
      <span v-if="tool.maintenanceIntervalMonths">
        {{ t('tools.care') }}
        <b>{{ t('tools.every', { n: tool.maintenanceIntervalMonths }) }} · {{ duration(tool.maintenanceMinutes) }}</b>
      </span>
      <span v-if="tool.nextMaintenance"
        >{{ t('tools.next') }} <b>{{ date(tool.nextMaintenance) }}</b></span
      >
      <span>
        {{ t('tools.beds') }}
        <b>{{ tool.bedIds.map(bedLabel).filter(Boolean).join(', ') || t('tools.inStorage') }}</b>
      </span>
    </div>
    <p v-if="tool.notes" class="notes">{{ tool.notes }}</p>

    <h3>{{ t('tools.neededBy') }}</h3>
    <p v-if="demand" class="item small" :class="{ warn: short }">
      {{ short ? '⚠️' : '✅' }}
      {{ t('tools.peak', { n: demand.quantity, month: monthLong(demand.month) }) }}
      <template v-if="short"> — {{ t('tools.shortBy', { n: short }) }}</template>
    </p>
    <div class="chips">
      <span v-for="v in neededBy" :key="v.id" class="chip">{{ v.name }} {{ v.cultivar }}</span>
      <span v-if="!neededBy.length" class="muted small">{{ t('tools.notNeeded') }}</span>
    </div>

    <h3>{{ t('tools.log') }}</h3>
    <form v-if="isEditor" class="log-form" @submit.prevent="saveEntry">
      <DatePicker v-model="entryDate" date-format="dd/mm/yy" :aria-label="t('notes.date')" />
      <InputText v-model="entry.text" :placeholder="t('tools.logText')" :aria-label="t('tools.logText')" />
      <InputNumber
        v-model="entry.cost"
        mode="currency"
        currency="EUR"
        :min="0"
        :placeholder="t('tools.cost')"
        :aria-label="t('tools.cost')"
      />
      <label class="check"><Checkbox v-model="entry.isMaintenance" binary /> {{ t('tools.isMaintenance') }}</label>
      <Button type="submit" :label="t('tools.addLog')" icon="pi pi-plus" size="small" :loading="addLog.isPending.value" />
    </form>
    <div class="stack">
      <div v-for="e in tool.log" :key="e.id" class="item">
        {{ e.isMaintenance ? '🛠️' : '📝' }}
        <span>{{ e.text ?? t(e.fromTask ? 'tools.maintenanceTask' : 'tools.maintenanceDone') }}</span>
        <span class="end">
          <template v-if="e.cost">{{ currency(e.cost) }} · </template>{{ date(e.date) }}
          <Button
            v-if="isEditor && !e.fromTask"
            icon="pi pi-trash"
            text
            rounded
            size="small"
            severity="secondary"
            :aria-label="t('common.delete')"
            @click="removeEntry(e.id)"
          />
        </span>
      </div>
      <span v-if="!tool.log.length" class="muted small">{{ t('tools.noLog') }}</span>
    </div>

    <template #footer>
      <Button v-if="isEditor" :label="t('common.edit')" icon="pi pi-pencil" severity="secondary" outlined @click="emit('edit')" />
      <Button :label="t('common.close')" @click="visible = false" />
    </template>
  </Dialog>
</template>

<style scoped>
.head {
  display: flex;
  gap: 12px;
  align-items: center;
}
.head h2 {
  margin: 0;
}
.icon {
  font-size: 34px;
  line-height: 1;
}
.notes {
  white-space: pre-line;
}
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
}
.chip {
  padding: 2px 9px;
  border-radius: 999px;
  background: var(--c-accent-soft);
  font-size: 12px;
}
.item.warn {
  border-color: var(--c-warn);
  background: var(--c-warn-soft);
}
.log-form {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
  margin-bottom: 8px;
}
.log-form > :nth-child(1) {
  width: 140px;
}
.log-form > :nth-child(2) {
  flex: 1 1 200px;
}
.log-form > :nth-child(3) {
  width: 120px;
}
.log-form :deep(input) {
  width: 100%;
}
.check {
  display: inline-flex;
  gap: 5px;
  align-items: center;
  font-size: 12px;
  white-space: nowrap;
}
</style>
