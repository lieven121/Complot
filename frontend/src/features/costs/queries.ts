import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api, type CostCategoryInput, type InvoiceInput } from '@/shared/api'
import type { Grouping } from './costs'

const COSTS = ['costs'] as const

export const useCostCategories = () =>
  useQuery({ queryKey: [...COSTS, 'categories'], queryFn: ({ signal }) => api.costs.listCostCategories(signal) })

export const useInvoices = (year: MaybeRefOrGetter<number | undefined>) =>
  useQuery({
    queryKey: computed(() => [...COSTS, 'invoices', toValue(year)]),
    queryFn: ({ signal }) => api.costs.listInvoices(toValue(year), signal),
  })

export const useInvoiceSuppliers = () =>
  useQuery({ queryKey: [...COSTS, 'suppliers'], queryFn: ({ signal }) => api.costs.listInvoiceSuppliers(signal) })

export const useCostReport = (year: MaybeRefOrGetter<number | undefined>, grouping: MaybeRefOrGetter<Grouping>) =>
  useQuery({
    queryKey: computed(() => [...COSTS, 'report', toValue(year), toValue(grouping)]),
    queryFn: ({ signal }) => api.costs.getCostReport(toValue(year), toValue(grouping), signal),
  })

function useCostMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const client = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => client.invalidateQueries({ queryKey: COSTS }) })
}

/** Saves the invoice, then uploads a newly chosen attachment (PDF or photo). */
export const useSaveInvoice = () =>
  useCostMutation(async ({ id, input, file }: { id?: string; input: InvoiceInput; file?: File }) => {
    const invoice = id ? await api.costs.updateInvoice(id, input) : await api.costs.createInvoice(input)
    if (file) await api.costs.uploadInvoiceAttachment(invoice.id, { data: file, fileName: file.name })
    return invoice
  })
export const useDeleteInvoice = () => useCostMutation((id: string) => api.costs.deleteInvoice(id))
export const useDeleteAttachment = () => useCostMutation((id: string) => api.costs.deleteInvoiceAttachment(id))

export const useSaveCostCategory = () =>
  useCostMutation(({ id, input }: { id?: string; input: CostCategoryInput }) =>
    id ? api.costs.updateCostCategory(id, input) : api.costs.createCostCategory(input),
  )
export const useDeleteCostCategory = () => useCostMutation((id: string) => api.costs.deleteCostCategory(id))

export const attachmentUrl = (invoiceId: string) => `/api/invoices/${invoiceId}/attachment`
