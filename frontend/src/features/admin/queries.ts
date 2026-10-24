import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api, type UserInput } from '@/shared/api'

const USERS = ['admin', 'users'] as const

export const useUsers = () => useQuery({ queryKey: USERS, queryFn: ({ signal }) => api.admin.listUsers(signal) })

/** Access changes touch nearly everything (site lists, roles, tasks), so every query is refreshed. */
function useAdminMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const client = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => client.invalidateQueries() })
}

export const useSaveUser = () =>
  useAdminMutation(({ id, input }: { id?: string; input: UserInput }) =>
    id ? api.admin.updateUser(id, input) : api.admin.inviteUser(input),
  )
export const useDeleteUser = () => useAdminMutation((id: string) => api.admin.deleteUser(id))

export const useArchiveSite = () => useAdminMutation((id: string) => api.sites.archiveSite(id))
export const useRestoreSite = () => useAdminMutation((id: string) => api.sites.restoreSite(id))
export const useDeleteSite = () =>
  useAdminMutation(({ id, confirmName }: { id: string; confirmName: string }) => api.sites.deleteSite(id, confirmName))
