<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const route = useRoute()
const title = computed(() => t(`nav.${String(route.meta.navKey ?? 'map')}`))
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>{{ title }}</h1>
    </div>
    <div class="card muted">{{ t('common.comingSoon') }}</div>
  </div>
</template>
