<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { LotStatus } from '@/shared/api'
import StatusTag from '@/shared/ui/StatusTag.vue'

defineProps<{ status: LotStatus }>()
const { t } = useI18n()
const tones = { ok: 'ok', useSoon: 'warn', expired: 'danger' } as const
</script>

<template>
  <StatusTag :tone="tones[status]">{{ t(`lotStatus.${status}`) }}</StatusTag>
</template>
