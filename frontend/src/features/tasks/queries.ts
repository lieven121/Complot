import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api, type CompleteTaskInput, type ManualTaskInput, type PersonInput, type TaskStateInput } from '@/shared/api'

const WORK = ['work'] as const

export interface TaskPeriod {
  from?: string
  to?: string
  siteId?: string
}

export const useTasks = (period: MaybeRefOrGetter<TaskPeriod> = {}) =>
  useQuery({
    queryKey: computed(() => [...WORK, 'tasks', toValue(period)]),
    queryFn: ({ signal }) => {
      const p = toValue(period)
      return api.tasks.listTasks(p.from, p.to, p.siteId, signal)
    },
  })

export const useEstimates = () =>
  useQuery({ queryKey: [...WORK, 'estimates'], queryFn: ({ signal }) => api.tasks.getTaskEstimates(signal) })
export const usePeople = () => useQuery({ queryKey: [...WORK, 'people'], queryFn: ({ signal }) => api.people.listPeople(signal) })

export const usePersonById = () => {
  const people = usePeople()
  return computed(() => new Map((people.data.value ?? []).map((p) => [p.id, p])))
}

function useWorkMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const client = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => client.invalidateQueries({ queryKey: WORK }) })
}

export const useCompleteTask = () =>
  useWorkMutation(({ key, input }: { key: string; input: CompleteTaskInput }) => api.tasks.completeTask(key, input))
export const useReopenTask = () => useWorkMutation((key: string) => api.tasks.reopenTask(key))
export const useSetTaskState = () =>
  useWorkMutation(({ key, input }: { key: string; input: TaskStateInput }) => api.tasks.setTaskState(key, input))

export const useSaveManualTask = () =>
  useWorkMutation(({ id, input }: { id?: string; input: ManualTaskInput }) =>
    id ? api.tasks.updateManualTask(id, input) : api.tasks.createManualTask(input),
  )
export const useDeleteManualTask = () => useWorkMutation((id: string) => api.tasks.deleteManualTask(id))

export const useSavePerson = () =>
  useWorkMutation(({ id, input }: { id?: string; input: PersonInput }) =>
    id ? api.people.updatePerson(id, input) : api.people.createPerson(input),
  )
export const useDeletePerson = () => useWorkMutation((id: string) => api.people.deletePerson(id))

/** Manual task keys are "manual:{id without dashes}"; the id is needed to edit or delete the task. */
export function manualTaskId(key: string): string | undefined {
  const match = /^manual:([0-9a-f]{32})$/.exec(key)
  if (!match) return undefined
  const h = match[1]!
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`
}
