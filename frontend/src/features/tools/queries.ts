import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api, type ToolInput, type ToolLogInput } from '@/shared/api'

const TOOLS = ['tools'] as const

export const useTools = () => useQuery({ queryKey: TOOLS, queryFn: ({ signal }) => api.tools.listTools(signal) })

export const useToolDemand = (year: MaybeRefOrGetter<number>) =>
  useQuery({
    queryKey: computed(() => [...TOOLS, 'demand', toValue(year)]),
    queryFn: ({ signal }) => api.tools.getToolDemand(toValue(year), signal),
  })

/** Tool changes move maintenance tasks too, so the task list is refreshed as well. */
function useToolMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: TOOLS })
      await client.invalidateQueries({ queryKey: ['work'] })
      await client.invalidateQueries({ queryKey: ['catalog'] })
    },
  })
}

export const useSaveTool = () =>
  useToolMutation(({ id, input }: { id?: string; input: ToolInput }) =>
    id ? api.tools.updateTool(id, input) : api.tools.createTool(input),
  )
export const useDeleteTool = () => useToolMutation((id: string) => api.tools.deleteTool(id))
export const useAddToolLog = () => useToolMutation(({ id, input }: { id: string; input: ToolLogInput }) => api.tools.addToolLog(id, input))
export const useDeleteToolLog = () =>
  useToolMutation(({ id, entryId }: { id: string; entryId: string }) => api.tools.deleteToolLog(id, entryId))
