import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { api, type SiteDto } from '@/shared/api'

export const ME_KEY = ['me'] as const

/** Who is signed in and what they may do. The API is the authority; the UI only hides what would be refused. */
export const useMe = () => useQuery({ queryKey: ME_KEY, queryFn: ({ signal }) => api.auth.getMe(signal), staleTime: 5 * 60_000 })

/** May change the plan of this site (design, plant, log, check off). */
export const canEditSite = (site: Pick<SiteDto, 'myRole' | 'archived'> | undefined) => site?.myRole === 'edit' && !site.archived

export function useSession() {
  const me = useMe()
  return {
    me,
    isAdmin: computed(() => me.data.value?.isAdmin ?? false),
    /** May change shared data: the catalog and the people list. */
    isEditor: computed(() => me.data.value?.isEditor ?? false),
    isPending: computed(() => me.data.value?.isPending ?? false),
    signInEnabled: computed(() => me.data.value?.authMode === 'oidc'),
  }
}
