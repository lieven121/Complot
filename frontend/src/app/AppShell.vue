<script setup lang="ts">
import Select from 'primevue/select'
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'
import { useLocale } from '@/i18n/useLocale'
import { useSession } from '@/shared/session'
import NoAccessPage from './NoAccessPage.vue'
import { navItems } from './router'

const { t } = useI18n()
const { locale, locales } = useLocale()
const { me, isAdmin, isPending, signInEnabled } = useSession()
const visibleNav = computed(() => (isPending.value ? [] : navItems.filter((item) => !item.adminOnly || isAdmin.value)))
</script>

<template>
  <div class="shell">
    <nav class="side" :aria-label="t('nav.label')">
      <div class="brand">
        🌱 Complot<small>{{ t('app.subtitle') }}</small>
      </div>
      <RouterLink v-for="item in visibleNav" :key="item.name" :to="item.path" class="nav-link" active-class="active">
        <span class="icon" aria-hidden="true">{{ item.icon }}</span>
        <span class="label">{{ t(`nav.${item.navKey}`) }}</span>
      </RouterLink>
      <div class="side-foot">
        <form v-if="signInEnabled && me.data.value?.isAuthenticated" method="post" action="/auth/logout" class="who">
          <span class="name">{{ me.data.value?.name ?? me.data.value?.email }}</span>
          <button type="submit" class="logout">{{ t('access.logout') }}</button>
        </form>
        <label for="locale">{{ t('language.label') }}</label>
        <Select
          v-model="locale"
          input-id="locale"
          :options="[...locales]"
          :option-label="(l: string) => t(`language.${l}`)"
          size="small"
          class="locale"
        />
      </div>
    </nav>
    <main class="main">
      <NoAccessPage v-if="isPending" />
      <RouterView v-else />
    </main>
  </div>
</template>

<style scoped>
.shell {
  display: grid;
  grid-template-columns: 200px minmax(0, 1fr);
  height: 100vh;
}
.side {
  background: var(--c-side);
  color: #e8efe4;
  display: flex;
  flex-direction: column;
  padding: 16px 10px;
  gap: 2px;
}
.brand {
  font-weight: 800;
  font-size: 21px;
  padding: 2px 10px 18px;
}
.brand small {
  display: block;
  font-weight: 500;
  font-size: 11px;
  opacity: 0.6;
  letter-spacing: 0.05em;
  text-transform: uppercase;
}
.nav-link {
  padding: 9px 12px;
  border-radius: 8px;
  color: inherit;
  text-decoration: none;
  display: flex;
  gap: 10px;
  align-items: center;
}
.nav-link:hover {
  background: rgba(255, 255, 255, 0.07);
}
.nav-link.active {
  background: rgba(255, 255, 255, 0.15);
  font-weight: 600;
}
.side-foot {
  margin-top: auto;
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 10px;
  font-size: 11px;
  opacity: 0.85;
}
.who {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 6px;
  margin-bottom: 8px;
  font-size: 12px;
}
.who .name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.logout {
  all: unset;
  cursor: pointer;
  text-decoration: underline;
}
.main {
  overflow: auto;
  min-width: 0;
}

@media (max-width: 900px) {
  .shell {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: minmax(0, 1fr) auto;
    height: 100dvh;
  }
  .main {
    grid-row: 1;
  }
  .side {
    grid-row: 2;
    flex-direction: row;
    justify-content: space-around;
    padding: 4px;
    gap: 0;
  }
  .brand,
  .side-foot {
    display: none;
  }
  .nav-link {
    flex: 1;
    min-width: 0;
    flex-direction: column;
    gap: 0;
    padding: 6px 2px;
    font-size: 10px;
    text-align: center;
  }
  .nav-link .icon {
    font-size: 19px;
  }
  .nav-link .label {
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
}
</style>
