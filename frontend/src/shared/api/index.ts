import {
  AuthClient,
  CatalogExchangeClient,
  CategoriesClient,
  FamiliesClient,
  InventoryLotsClient,
  NotesClient,
  PeopleClient,
  PlantingsClient,
  SitesClient,
  SuppliersClient,
  TasksClient,
  VarietiesClient,
} from './client'

/**
 * fetch used by every generated NSwag client: same-origin cookies, and a 401 (OIDC session expired or missing)
 * sends the browser through the login flow and back to the current page.
 */
const http = {
  async fetch(url: RequestInfo, init?: RequestInit): Promise<Response> {
    const response = await fetch(url, { ...init, credentials: 'same-origin' })
    if (response.status === 401) {
      const returnUrl = window.location.pathname + window.location.search
      window.location.assign(`/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`)
    }
    return response
  },
}

const baseUrl = ''

export const api = {
  auth: new AuthClient(baseUrl, http),
  families: new FamiliesClient(baseUrl, http),
  categories: new CategoriesClient(baseUrl, http),
  suppliers: new SuppliersClient(baseUrl, http),
  varieties: new VarietiesClient(baseUrl, http),
  inventoryLots: new InventoryLotsClient(baseUrl, http),
  catalogExchange: new CatalogExchangeClient(baseUrl, http),
  sites: new SitesClient(baseUrl, http),
  plantings: new PlantingsClient(baseUrl, http),
  people: new PeopleClient(baseUrl, http),
  tasks: new TasksClient(baseUrl, http),
  notes: new NotesClient(baseUrl, http),
}

export type * from './client'
