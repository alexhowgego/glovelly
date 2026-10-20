import { describe, expect, it } from 'vitest'
import { mapFrameSize } from './mapFrameSize'

describe('map frame dimensions', () => {
  it('matches the measured frame, scaling both axes together to provider bounds', () => {
    expect(mapFrameSize(580, 400)).toBe('580x400')
    expect(mapFrameSize(1000, 500)).toBe('640x320')
    expect(mapFrameSize(400, 800)).toBe('320x640')
    expect(mapFrameSize(0, 500)).toBeNull()
  })
})
