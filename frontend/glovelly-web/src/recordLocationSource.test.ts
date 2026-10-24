import { describe, expect, it } from 'vitest'
import vectors from '../../../tests/fixtures/record-location-sources.json'
import { clientLocationQuery, gigLocationQuery, googleMapsSearchUrl, locationFingerprint } from './recordLocationSource'

describe('record location sources', () => {
  for (const vector of vectors) {
    it(vector.name, async () => {
      const query = vector.address ? clientLocationQuery(vector.address) : gigLocationQuery(vector.venue)
      expect(query).toBe(vector.query)
      expect(await locationFingerprint(query)).toBe(vector.fingerprint)
    })
  }

  it('encodes links without looking up or approving the location', () => {
    expect(googleMapsSearchUrl('Hall & Café? #1')).toBe('https://www.google.com/maps/search/?api=1&query=Hall%20%26%20Caf%C3%A9%3F%20%231')
    expect(googleMapsSearchUrl('')).toBeNull()
  })
})
