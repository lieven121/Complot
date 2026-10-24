import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  api,
  type CatalogExchange,
  type CategoryInput,
  type FamilyInput,
  type InventoryLotInput,
  type SupplierInput,
  type VarietyInput,
} from '@/shared/api'

/** All catalog data hangs under one key, so any catalog change refreshes every catalog view. */
const CATALOG = ['catalog'] as const
const keys = {
  families: [...CATALOG, 'families'],
  categories: [...CATALOG, 'categories'],
  suppliers: [...CATALOG, 'suppliers'],
  varieties: [...CATALOG, 'varieties'],
  variety: (id: string) => [...CATALOG, 'variety', id],
  lots: [...CATALOG, 'lots'],
}

export const useFamilies = () => useQuery({ queryKey: keys.families, queryFn: ({ signal }) => api.families.listFamilies(signal) })
export const useCategories = () => useQuery({ queryKey: keys.categories, queryFn: ({ signal }) => api.categories.listCategories(signal) })
export const useSuppliers = () => useQuery({ queryKey: keys.suppliers, queryFn: ({ signal }) => api.suppliers.listSuppliers(signal) })
export const useVarieties = () =>
  useQuery({ queryKey: keys.varieties, queryFn: ({ signal }) => api.varieties.listVarieties(undefined, undefined, signal) })
export const useInventoryLots = () =>
  useQuery({ queryKey: keys.lots, queryFn: ({ signal }) => api.inventoryLots.listInventoryLots(undefined, signal) })
export const useVariety = (id: MaybeRefOrGetter<string>) =>
  useQuery({
    queryKey: computed(() => keys.variety(toValue(id))),
    queryFn: ({ signal }) => api.varieties.getVariety(toValue(id), signal),
  })

/** Lookup maps for showing names next to ids. */
export function useCatalogLookups() {
  const families = useFamilies()
  const categories = useCategories()
  const suppliers = useSuppliers()
  const varieties = useVarieties()
  const byId = <T extends { id: string }>(list?: T[]) => new Map((list ?? []).map((x) => [x.id, x]))
  return {
    families,
    categories,
    suppliers,
    varieties,
    familyById: computed(() => byId(families.data.value)),
    categoryById: computed(() => byId(categories.data.value)),
    supplierById: computed(() => byId(suppliers.data.value)),
    varietyById: computed(() => byId(varieties.data.value)),
  }
}

function useCatalogMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const client = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => client.invalidateQueries({ queryKey: CATALOG }) })
}

type Save<T> = { id?: string; input: T }

export const useSaveFamily = () =>
  useCatalogMutation(({ id, input }: Save<FamilyInput>) => (id ? api.families.updateFamily(id, input) : api.families.createFamily(input)))
export const useDeleteFamily = () => useCatalogMutation((id: string) => api.families.deleteFamily(id))

export const useSaveCategory = () =>
  useCatalogMutation(({ id, input }: Save<CategoryInput>) =>
    id ? api.categories.updateCategory(id, input) : api.categories.createCategory(input),
  )
export const useDeleteCategory = () => useCatalogMutation((id: string) => api.categories.deleteCategory(id))

export const useSaveSupplier = () =>
  useCatalogMutation(({ id, input }: Save<SupplierInput>) =>
    id ? api.suppliers.updateSupplier(id, input) : api.suppliers.createSupplier(input),
  )
export const useDeleteSupplier = () => useCatalogMutation((id: string) => api.suppliers.deleteSupplier(id))

export const useSaveVariety = () =>
  useCatalogMutation(({ id, input }: Save<VarietyInput>) =>
    id ? api.varieties.updateVariety(id, input) : api.varieties.createVariety(input),
  )
export const useDeleteVariety = () => useCatalogMutation((id: string) => api.varieties.deleteVariety(id))

export const useSaveLot = () =>
  useCatalogMutation(({ id, input }: Save<InventoryLotInput>) =>
    id ? api.inventoryLots.updateInventoryLot(id, input) : api.inventoryLots.createInventoryLot(input),
  )
export const useDeleteLot = () => useCatalogMutation((id: string) => api.inventoryLots.deleteInventoryLot(id))

export const usePreviewImport = () => useMutation({ mutationFn: (data: CatalogExchange) => api.catalogExchange.previewCatalogImport(data) })
export const useImportCatalog = () => useCatalogMutation((data: CatalogExchange) => api.catalogExchange.importCatalog(data))
