import type { Address } from './types'

// Keep the exact whitespace/field order contract in sync with RecordLocationSource.cs.
const normalize = (value: string | null | undefined) =>
  (value ?? '').replace(/[ \t\r\n\f\v]+/g, ' ').replace(/^ +| +$/g, '')

export function clientLocationQuery(address: Address | null | undefined): string {
  return address
    ? [address.line1, address.line2, address.city, address.stateOrCounty, address.postalCode, address.country]
      .map(normalize).filter(Boolean).join(', ')
    : ''
}

export const gigLocationQuery = (venue: string | null | undefined): string => normalize(venue)

export async function locationFingerprint(query: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(query))
  return `v1:${Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('')}`
}

export function googleMapsSearchUrl(query: string): string | null {
  return query ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}` : null
}
