<script setup lang="ts">
import InputNumber from 'primevue/inputnumber'
import { useI18n } from 'vue-i18n'

/**
 * PrimeVue InputNumber in the app's language (decimal comma in nl-BE). Without this it follows the operating
 * system's locale, so typing "2,5" could be read as 25. Other props (min, suffix, …) fall through.
 */
const model = defineModel<number | null | undefined>()
const { locale } = useI18n()
</script>

<template>
  <InputNumber v-model="model" :locale="locale" />
</template>
