import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api, type NoteInput, type PlanPlantingInput, type SiteInput, type SiteLayoutDto, type UpdatePlantingInput } from '@/shared/api'
import { toIsoDate } from '@/shared/dates'

/**
 * Garden data. Anything that changes the plan also changes the tasks and checks, so mutations invalidate the whole
 * `garden` and `work` key spaces.
 */
const GARDEN = ['garden'] as const
const WORK = ['work'] as const

/** The period the map shows: from the start of last year to the end of next year. */
export function mapPeriod(today = new Date()) {
  const year = today.getFullYear()
  return { from: `${year - 1}-01-01`, to: `${year + 1}-12-31`, today: toIsoDate(today)! }
}

/** Sites the user can see; `includeArchived` (admins only) also lists archived ones, for the admin page. */
export const useSites = (includeArchived = false) =>
  useQuery({
    queryKey: [...GARDEN, 'sites', { includeArchived }],
    queryFn: ({ signal }) => api.sites.listSites(includeArchived || undefined, signal),
  })

export const useLayout = (siteId: MaybeRefOrGetter<string | undefined>) =>
  useQuery({
    queryKey: computed(() => [...GARDEN, 'layout', toValue(siteId)]),
    queryFn: ({ signal }) => api.sites.getSiteLayout(toValue(siteId)!, signal),
    enabled: computed(() => !!toValue(siteId)),
  })

/** Every bed the user can see, labelled "Thuis · K1" (for things shared across sites, such as tools). */
export function useAllBeds() {
  const sites = useSites()
  const layouts = useQueries({
    queries: computed(() =>
      (sites.data.value ?? []).map((site) => ({
        queryKey: [...GARDEN, 'layout', site.id],
        queryFn: ({ signal }: { signal: AbortSignal }) => api.sites.getSiteLayout(site.id, signal),
      })),
    ),
  })
  return computed(() =>
    (sites.data.value ?? []).flatMap((site, i) =>
      (layouts.value[i]?.data?.beds ?? []).map((bed) => ({ id: bed.id, code: bed.code, label: `${site.name} · ${bed.code}` })),
    ),
  )
}

export const usePlantings = (siteId: MaybeRefOrGetter<string | undefined>, period = mapPeriod()) =>
  useQuery({
    queryKey: computed(() => [...GARDEN, 'plantings', toValue(siteId), period.from, period.to]),
    queryFn: ({ signal }) => api.plantings.listPlantings(toValue(siteId)!, period.from, period.to, signal),
    enabled: computed(() => !!toValue(siteId)),
  })

export const useSiteChecks = (siteId: MaybeRefOrGetter<string | undefined>) =>
  useQuery({
    queryKey: computed(() => [...GARDEN, 'checks', toValue(siteId)]),
    queryFn: ({ signal }) => api.plantings.getSiteChecks(toValue(siteId)!, signal),
    enabled: computed(() => !!toValue(siteId)),
  })

export const useNotes = (filter: MaybeRefOrGetter<{ siteId?: string; bedId?: string; treeId?: string; varietyId?: string }>) =>
  useQuery({
    queryKey: computed(() => [...GARDEN, 'notes', toValue(filter)]),
    queryFn: ({ signal }) => {
      const f = toValue(filter)
      return api.notes.listNotes(f.siteId, f.bedId, f.treeId, f.varietyId, signal)
    },
  })

function useGardenMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: GARDEN })
      await client.invalidateQueries({ queryKey: WORK })
    },
  })
}

export const useSaveSite = () =>
  useGardenMutation(({ id, input }: { id?: string; input: SiteInput }) =>
    id ? api.sites.updateSite(id, input) : api.sites.createSite(input),
  )
export const useSaveLayout = () =>
  useGardenMutation(({ siteId, layout }: { siteId: string; layout: SiteLayoutDto }) => api.sites.saveSiteLayout(siteId, layout))

export const usePreviewPlanting = () => useMutation({ mutationFn: (input: PlanPlantingInput) => api.plantings.previewPlanting(input) })
export const useCreatePlanting = () => useGardenMutation((input: PlanPlantingInput) => api.plantings.createPlanting(input))
export const useUpdatePlanting = () =>
  useGardenMutation(({ id, input }: { id: string; input: UpdatePlantingInput }) => api.plantings.updatePlanting(id, input))
export const useDeletePlanting = () => useGardenMutation((id: string) => api.plantings.deletePlanting(id))

export const useSaveNote = () =>
  useGardenMutation(async ({ id, input, photo }: { id?: string; input: NoteInput; photo?: File }) => {
    const note = id ? await api.notes.updateNote(id, input) : await api.notes.createNote(input)
    if (photo) await api.notes.uploadNotePhoto(note.id, { data: photo, fileName: photo.name })
    return note
  })
export const useDeleteNote = () => useGardenMutation((id: string) => api.notes.deleteNote(id))

/** URL of a note's photo (served by the API, same origin). */
export const notePhotoUrl = (noteId: string) => `/api/notes/${noteId}/photo`
