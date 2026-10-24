import { ApiException, type ErrorDetail } from './client'

/** The API's error contract: codes + params, never UI text (see backend Common/ApiProblems.cs). */
export interface ApiProblem {
  status: number
  /** Conflict code, e.g. "category.inUse". */
  code?: string
  /** Validation errors per camelCase field path, e.g. "harvest.to". */
  errors?: Record<string, ErrorDetail[]>
}

export function toApiProblem(error: unknown): ApiProblem | undefined {
  if (typeof error !== 'object' || error === null || !ApiException.isApiException(error)) return undefined
  let body: Partial<ApiProblem> = {}
  try {
    body = JSON.parse(error.response) as Partial<ApiProblem>
  } catch {
    // not a problem+json body
  }
  return { status: error.status, code: body.code, errors: body.errors }
}
