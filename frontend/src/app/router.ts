import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

/** One entry per main section; `navKey` is the i18n key under `nav.` and drives the side/bottom navigation. */
export interface NavItem {
  name: string
  path: string
  icon: string
  navKey: string
  /** Only shown to admins. */
  adminOnly?: boolean
}

export const navItems: NavItem[] = [
  { name: 'map', path: '/kaart', icon: '🗺️', navKey: 'map' },
  { name: 'tasks', path: '/taken', icon: '✅', navKey: 'tasks' },
  { name: 'planner', path: '/planner', icon: '🗓️', navKey: 'planner' },
  { name: 'plants', path: '/planten', icon: '🌿', navKey: 'plants' },
  { name: 'tools', path: '/gereedschap', icon: '🧰', navKey: 'tools' },
  { name: 'costs', path: '/kosten', icon: '💶', navKey: 'costs' },
  { name: 'admin', path: '/beheer', icon: '⚙️', navKey: 'admin', adminOnly: true },
]

const comingSoon = () => import('@/shared/ui/ComingSoonPage.vue')

const routes: RouteRecordRaw[] = [
  { path: '/', redirect: '/kaart' },
  { path: '/kaart', name: 'map', component: () => import('@/features/garden/MapPage.vue') },
  { path: '/beheer', name: 'admin', component: () => import('@/features/admin/AdminPage.vue') },
  { path: '/gereedschap', name: 'tools', component: () => import('@/features/tools/ToolsPage.vue') },
  { path: '/planner', name: 'planner', component: () => import('@/features/planner/PlannerPage.vue') },
  { path: '/taken', name: 'tasks', component: () => import('@/features/tasks/TasksPage.vue') },
  { path: '/planten', name: 'plants', component: () => import('@/features/catalog/PlantsPage.vue') },
  { path: '/planten/:id', name: 'variety', component: () => import('@/features/catalog/VarietyDetailPage.vue'), props: true },
  ...navItems
    .filter((item) => !['plants', 'map', 'tasks', 'admin', 'planner', 'tools'].includes(item.name))
    .map((item) => ({ path: item.path, name: item.name, component: comingSoon, meta: { navKey: item.navKey } })),
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

export const router = createRouter({ history: createWebHistory(), routes })
