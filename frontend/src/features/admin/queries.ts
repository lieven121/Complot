import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api, type AccessLinkCreatedDto, type AccessLinkDto, type AccessLinkInput, type UserInput } from '@/shared/api'

const USERS = ['admin', 'users'] as const
const LINKS = ['admin', 'links'] as const

export const useUsers = () => useQuery({ queryKey: USERS, queryFn: ({ signal }) => api.admin.listUsers(signal) })
export const useLinks = () => useQuery({ queryKey: LINKS, queryFn: ({ signal }) => api.admin.listAccessLinks(signal) })

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

/** Creating a link returns its secret token once ({ link, token }); updating returns the link. */
export const useSaveLink = () =>
  useAdminMutation(({ id, input }: { id?: string; input: AccessLinkInput }): Promise<AccessLinkDto | AccessLinkCreatedDto> =>
    id ? api.admin.updateAccessLink(id, input) : api.admin.createAccessLink(input),
  )
export const useRenewLink = () => useAdminMutation((id: string) => api.admin.renewAccessLinkToken(id))
export const useDeleteLink = () => useAdminMutation((id: string) => api.admin.deleteAccessLink(id))

/** The address a volunteer opens; the token is only known right after creating or renewing the link. */
export const linkUrl = (token: string) => `${window.location.origin}/auth/link/${token}`
