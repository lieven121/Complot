import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api } from '@/shared/api'
import { addDays } from '@/shared/dates'

const PLANNER = ['planner'] as const

export const usePlannerSettings = () =>
  useQuery({ queryKey: [...PLANNER, 'settings'], queryFn: ({ signal }) => api.planner.getPlannerSettings(signal), staleTime: Infinity })

export const useCalendars = () =>
  useQuery({ queryKey: [...PLANNER, 'calendars'], queryFn: ({ signal }) => api.planner.listPersonCalendars(signal) })

/** Busy times of one person in the week starting on `monday` (only when their Google Calendar is linked). */
export const useBusy = (
  personId: MaybeRefOrGetter<string | undefined>,
  monday: MaybeRefOrGetter<string>,
  enabled: MaybeRefOrGetter<boolean>,
) =>
  useQuery({
    queryKey: computed(() => [...PLANNER, 'busy', toValue(personId), toValue(monday)]),
    queryFn: ({ signal }) => api.planner.getBusyTimes(toValue(personId)!, toValue(monday), addDays(toValue(monday), 6), signal),
    enabled: computed(() => !!toValue(personId) && toValue(enabled)),
    retry: false,
  })

function usePlannerMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const client = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => client.invalidateQueries({ queryKey: PLANNER }) })
}

export const useRenewFeed = () => usePlannerMutation((personId: string) => api.planner.renewCalendarFeed(personId))
export const useDeleteFeed = () => usePlannerMutation((personId: string) => api.planner.deleteCalendarFeed(personId))
export const useDisconnectGoogle = () => usePlannerMutation((personId: string) => api.planner.disconnectGoogleCalendar(personId))

/** Linking Google is a browser trip to Google's consent screen and back to `returnUrl`. */
export const connectGoogleUrl = (personId: string, returnUrl: string) =>
  `/api/planner/google/connect?personId=${encodeURIComponent(personId)}&returnUrl=${encodeURIComponent(returnUrl)}`

export const feedUrl = (token: string) => `${window.location.origin}/ical/${token}.ics`
