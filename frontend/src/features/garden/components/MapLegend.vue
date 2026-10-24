<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PlantingPhase } from '@/shared/api'
import { PHASE_OPACITY } from '../map'
import type { Crop } from './GardenMap.vue'

/** Collapsible legend: phase intensity, crop groups on the map right now, symbols and features. */
defineProps<{ crops: Crop[] }>()
const { t } = useI18n()
const open = ref(!window.matchMedia('(max-width: 900px)').matches)

const example = '#3f8a68'
const mix = (phase: PlantingPhase) => `color-mix(in srgb, ${example} ${Math.round(PHASE_OPACITY[phase] * 100)}%, #d8c6a2)`
const phases: { phase: PlantingPhase; pattern?: string }[] = [
  { phase: 'young' },
  { phase: 'growing' },
  { phase: 'harvest', pattern: 'dots' },
  { phase: 'clearing', pattern: 'hatch' },
]
</script>

<template>
  <div class="legend">
    <button type="button" class="head" :aria-expanded="open" @click="open = !open">
      🗺️ {{ t('map.legend.title') }}<span>{{ open ? t('map.legend.hide') : t('map.legend.show') }}</span>
    </button>
    <div v-if="open" class="body">
      <section>
        <b>{{ t('map.legend.phase') }}</b>
        <small>{{ t('map.legend.phaseHelp') }}</small>
        <div class="lg-row">
          <span class="sw soil" /><span>{{ t('map.legend.empty') }}</span>
        </div>
        <div v-for="p in phases" :key="p.phase" class="lg-row">
          <span class="sw" :class="p.pattern" :style="{ backgroundColor: mix(p.phase) }" /><span>{{
            t(`map.legend.phases.${p.phase}`)
          }}</span>
        </div>
      </section>
      <section>
        <b>{{ t('map.legend.crops') }}</b>
        <div class="chips">
          <span v-for="c in crops" :key="c.name" class="chip" :style="{ '--c': c.color }">{{ c.icon }} {{ c.name }}</span>
          <span v-if="!crops.length" class="muted small">{{ t('map.legend.noCrops') }}</span>
        </div>
      </section>
      <section>
        <b>{{ t('map.legend.symbols') }}</b>
        <div class="lg-row">
          <span class="sw planned" /><span>{{ t('map.legend.planned') }}</span>
        </div>
        <div class="lg-row">
          <span class="sw none">🏠</span><span>{{ t('map.legend.indoor') }}</span>
        </div>
        <div class="lg-row">
          <span class="sw none"><span class="warn">!</span></span
          ><span>{{ t('map.legend.warning') }}</span>
        </div>
        <div class="lg-row">
          <span class="sw none">🦠</span><span>{{ t('map.legend.alert') }}</span>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.legend {
  position: absolute;
  left: 12px;
  bottom: 12px;
  width: 280px;
  max-height: calc(100% - 80px);
  overflow: auto;
  background: rgba(255, 253, 248, 0.97);
  border-radius: 12px;
  box-shadow: var(--shadow);
  font-size: 12px;
}
.head {
  all: unset;
  box-sizing: border-box;
  width: 100%;
  display: flex;
  gap: 8px;
  padding: 9px 12px;
  font-weight: 700;
  cursor: pointer;
}
.head:focus-visible {
  outline: 2px solid var(--c-accent);
}
.head span {
  margin-left: auto;
  color: var(--c-muted);
  font-weight: 500;
}
.body {
  padding: 0 12px 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
section > b {
  display: block;
  font-size: 10.5px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--c-muted);
}
section > small {
  display: block;
  color: var(--c-muted);
  margin-bottom: 4px;
}
.lg-row {
  display: flex;
  flex-wrap: nowrap;
  align-items: center;
  gap: 9px;
  margin: 4px 0;
}
.sw {
  width: 30px;
  height: 18px;
  border-radius: 4px;
  flex: none;
  border: 1px solid rgba(0, 0, 0, 0.2);
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.soil {
  background: #d8c6a2 radial-gradient(#b59d70 1px, transparent 1.6px) 0 0 / 6px 6px;
}
.dots {
  background-image: radial-gradient(rgba(255, 255, 255, 0.9) 1.5px, transparent 2px);
  background-size: 6px 6px;
}
.hatch {
  background-image: repeating-linear-gradient(45deg, rgba(80, 60, 30, 0.5) 0 1.3px, transparent 1.3px 5px);
}
.planned {
  background: #fff;
  border: 1.5px dashed #333;
}
.none {
  border: 0;
}
.warn {
  width: 17px;
  height: 17px;
  border-radius: 50%;
  background: var(--c-warn);
  color: #fff;
  font-weight: 800;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 4px;
}
.chip {
  font-size: 11px;
  padding: 1px 7px;
  border-radius: 999px;
  font-weight: 600;
  background: color-mix(in srgb, var(--c) 18%, #fff);
  border: 1px solid color-mix(in srgb, var(--c) 35%, #fff);
}
@media (max-width: 900px) {
  .legend {
    right: 12px;
    width: auto;
  }
}
</style>
