<script setup lang="ts">
import { computed } from 'vue'
import ColorPicker from 'primevue/colorpicker'

/** Colour as "#rrggbb" (the API format); PrimeVue's picker itself works without "#". */
const model = defineModel<string>({ required: true })
const hex = computed({ get: () => model.value.replace('#', ''), set: (value: string) => (model.value = `#${value}`) })
</script>

<template>
  <div class="row">
    <ColorPicker v-model="hex" /> <code>{{ model }}</code>
  </div>
</template>
