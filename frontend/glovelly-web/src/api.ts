import type { AppMetadata } from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

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

export async function parseProblemDetails(response: Response) {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) {
    return null
  }

  try {
    return (await response.json()) as {
      title?: string
      detail?: string
      errors?: Record<string, string[]>
    }
  } catch {
    return null
  }
}
