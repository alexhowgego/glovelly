import type { AppMetadata } from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''
const sessionExpiredErrorMessage = 'SESSION_EXPIRED'

export type SessionExpiredHandler = (message: string) => void

export type ProblemDetails = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class SessionExpiredError extends Error {
  constructor() {
    super(sessionExpiredErrorMessage)
    this.name = 'SessionExpiredError'
  }
}

export function buildApiUrl(path: string) {
  return `${apiBaseUrl}${path}`
}

export function buildReturnUrl() {
  return window.location.href
}

export async function loadAppMetadata(): Promise<AppMetadata> {
  try {
    const response = await fetch(buildApiUrl('/app/metadata'), {
      cache: 'no-store',
      credentials: 'same-origin',
    })

    if (!response.ok) {
      throw new Error('Unable to load app metadata.')
    }

    const metadata = (await response.json()) as Partial<AppMetadata>
    return {
      title: metadata.title?.trim() || 'Glovelly',
      deploymentName: metadata.deploymentName?.trim() || null,
      commitId: metadata.commitId?.trim() || null,
      buildTimestamp: metadata.buildTimestamp?.trim() || null,
    }
  } catch {
    return {
      title: 'Glovelly',
      deploymentName: null,
      commitId: null,
      buildTimestamp: null,
    }
  }
}

export async function fetchWithSession(input: string, init?: RequestInit) {
  return fetch(input, {
    ...init,
    credentials: 'include',
    cache: 'no-store',
  })
}

export function jsonRequestInit(method: string, body?: unknown): RequestInit {
  return {
    method,
    headers: {
      'Content-Type': 'application/json',
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }
}

export function isSessionExpiredResponse(response: Response) {
  return response.status === 401
}

export function throwIfSessionExpired(response: Response) {
  if (isSessionExpiredResponse(response)) {
    throw new SessionExpiredError()
  }
}

export function isSessionExpiredError(error: unknown) {
  return error instanceof SessionExpiredError
}

export function handleSessionExpired(
  response: Response,
  onSessionExpired: SessionExpiredHandler,
  message: string
) {
  if (!isSessionExpiredResponse(response)) {
    return false
  }

  onSessionExpired(message)
  return true
}

export async function parseProblemDetails(response: Response) {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) {
    return null
  }

  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

export function getProblemDetailsMessage(
  problem: ProblemDetails | null,
  fallback?: string
) {
  const validationMessages = problem?.errors
    ? Object.values(problem.errors).flat().join(' ')
    : null

  return validationMessages || problem?.detail || problem?.title || fallback
}

export async function getResponseErrorMessage(
  response: Response,
  fallback: string
) {
  return getProblemDetailsMessage(await parseProblemDetails(response), fallback)
}

export function extractDownloadFilename(contentDisposition: string | null) {
  if (!contentDisposition) {
    return null
  }

  const encodedMatch = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i)
  if (encodedMatch?.[1]) {
    try {
      return decodeURIComponent(encodedMatch[1])
    } catch {
      return encodedMatch[1]
    }
  }

  const quotedMatch = contentDisposition.match(/filename="([^"]+)"/i)
  if (quotedMatch?.[1]) {
    return quotedMatch[1]
  }

  const plainMatch = contentDisposition.match(/filename=([^;]+)/i)
  return plainMatch?.[1]?.trim() ?? null
}

export async function createBlobObjectUrl(response: Response) {
  return window.URL.createObjectURL(await response.blob())
}

export async function downloadResponseBlob(
  response: Response,
  fallbackFilename: string
) {
  const downloadUrl = await createBlobObjectUrl(response)
  const link = document.createElement('a')
  link.href = downloadUrl
  link.download =
    extractDownloadFilename(response.headers.get('Content-Disposition')) ??
    fallbackFilename
  document.body.append(link)
  link.click()
  link.remove()
  window.URL.revokeObjectURL(downloadUrl)

  return link.download
}
