<script setup lang="ts">
import Button from 'primevue/button'
import { useI18n } from 'vue-i18n'
import { useSession } from '@/shared/session'

/** Signed in, but no admin has given access to a site yet. */
const { t } = useI18n()
const { me } = useSession()
</script>

<template>
  <div class="page">
    <div class="card waiting">
      <div class="icon" aria-hidden="true">🌱</div>
      <h1>{{ t('access.waitingTitle') }}</h1>
      <p>{{ t('access.waiting', { email: me.data.value?.email ?? '' }) }}</p>
      <form method="post" action="/auth/logout">
        <Button type="submit" :label="t('access.logout')" icon="pi pi-sign-out" severity="secondary" outlined />
      </form>
    </div>
  </div>
</template>

<style scoped>
.waiting {
  max-width: 520px;
  margin: 10vh auto 0;
  text-align: center;
  padding: 32px 24px;
}
.icon {
  font-size: 44px;
}
h1 {
  margin: 8px 0 10px;
}
</style>
