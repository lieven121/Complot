<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import { useConfirm } from 'primevue/useconfirm'
import { useI18n } from 'vue-i18n'
import type { PersonCalendarDto, PersonDto } from '@/shared/api'
import { useNotify } from '@/shared/composables/useNotify'
import { connectGoogleUrl, feedUrl, useDeleteFeed, useDisconnectGoogle, useRenewFeed } from '../queries'

/** One person's calendars: their Google Calendar (written to by the planner) and a read-only iCal link. */
const props = defineProps<{ person: PersonDto; calendar?: PersonCalendarDto; googleEnabled: boolean }>()

const { t } = useI18n()
const route = useRoute()
const confirm = useConfirm()
const notify = useNotify()
const renewFeed = useRenewFeed()
const deleteFeed = useDeleteFeed()
const disconnect = useDisconnectGoogle()

const canManage = computed(() => props.calendar?.canManage ?? false)
const feed = computed(() => (props.calendar?.feedToken ? feedUrl(props.calendar.feedToken) : undefined))

const connect = () => window.location.assign(connectGoogleUrl(props.person.id, route.fullPath))

async function run(action: () => Promise<unknown>, success?: string) {
  try {
    await action()
    if (success) notify.success(success)
  } catch (error) {
    notify.error(error)
  }
}

function confirmDisconnect() {
  confirm.require({
    header: t('planner.calendar.disconnect'),
    message: t('planner.calendar.disconnectWarning', { name: props.person.name }),
    acceptProps: { label: t('planner.calendar.disconnect'), severity: 'danger' },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', outlined: true },
    accept: () => run(() => disconnect.mutateAsync(props.person.id), 'planner.calendar.disconnected'),
  })
}

async function copy() {
  if (!feed.value) return
  await navigator.clipboard.writeText(feed.value)
  notify.success('common.copied')
}
</script>

<template>
  <section class="card calendars">
    <h2>{{ t('planner.calendar.title', { name: person.name }) }}</h2>

    <div class="block">
      <h3>{{ t('planner.calendar.google') }}</h3>
      <p v-if="!googleEnabled" class="muted small">{{ t('planner.calendar.notConfigured') }}</p>
      <template v-else-if="calendar?.googleAccount">
        <p class="linked">✓ {{ t('planner.calendar.linked', { account: calendar.googleAccount }) }}</p>
        <Button
          v-if="canManage"
          :label="t('planner.calendar.disconnect')"
          icon="pi pi-times"
          text
          size="small"
          severity="secondary"
          @click="confirmDisconnect"
        />
      </template>
      <template v-else>
        <p class="muted small">{{ t('planner.calendar.notLinked') }}</p>
        <Button v-if="canManage" :label="t('planner.calendar.connect')" icon="pi pi-google" size="small" @click="connect" />
        <p v-else class="muted small">{{ t('planner.calendar.notYours', { name: person.name }) }}</p>
      </template>
    </div>

    <div v-if="canManage" class="block">
      <h3>{{ t('planner.calendar.feed') }}</h3>
      <p class="muted small">{{ t('planner.calendar.feedHelp') }}</p>
      <template v-if="feed">
        <div class="url">
          <InputText :model-value="feed" readonly aria-label="iCal URL" @focus="($event.target as HTMLInputElement).select()" />
          <Button icon="pi pi-copy" :aria-label="t('common.copy')" :title="t('common.copy')" @click="copy" />
        </div>
        <div class="row">
          <Button
            :label="t('planner.calendar.feedRenew')"
            icon="pi pi-refresh"
            text
            size="small"
            @click="run(() => renewFeed.mutateAsync(person.id))"
          />
          <Button
            :label="t('common.delete')"
            icon="pi pi-trash"
            text
            size="small"
            severity="secondary"
            @click="run(() => deleteFeed.mutateAsync(person.id))"
          />
        </div>
      </template>
      <Button
        v-else
        :label="t('planner.calendar.feedCreate')"
        icon="pi pi-link"
        size="small"
        severity="secondary"
        outlined
        @click="run(() => renewFeed.mutateAsync(person.id))"
      />
    </div>
  </section>
</template>

<style scoped>
.calendars h2 {
  font-size: 15px;
}
.block h3 {
  margin-top: 10px;
}
.linked {
  color: var(--c-accent);
  font-weight: 600;
  margin: 0 0 4px;
  word-break: break-all;
}
.url {
  display: flex;
  gap: 6px;
}
.url :deep(input) {
  flex: 1;
  min-width: 0;
  font-family: ui-monospace, monospace;
  font-size: 11px;
}
</style>
