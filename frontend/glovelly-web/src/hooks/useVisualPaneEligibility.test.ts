import { describe, expect, it } from 'vitest'
import { visualPaneBucket, visualPaneHeightBudget } from './useVisualPaneEligibility'

describe('visual pane dimensions', () => {
  it('reserves border/focus space inside the actual measured height', () => {
    expect(visualPaneHeightBudget(500.5)).toBe(492)
    expect(visualPaneHeightBudget(300)).toBe(292)
    expect(visualPaneHeightBudget(5)).toBe(0)
  })
  it('requires both useful width and height', () => {
    expect(visualPaneBucket(619, 500, false)).toBeNull()
    expect(visualPaneBucket(1000, 299, false)).toBeNull()
    expect(visualPaneBucket(620, 300, false)).toBe('small')
    expect(visualPaneBucket(840, 430, false)).toBe('medium')
  })
  it('uses hysteresis near the visibility threshold', () => {
    expect(visualPaneBucket(610, 290, true)).toBe('small')
    expect(visualPaneBucket(610, 290, false)).toBeNull()
    expect(visualPaneBucket(599, 500, true)).toBeNull()
    expect(visualPaneBucket(1000, 279, true)).toBeNull()
    expect(visualPaneBucket(840, 430, true, 'small')).toBe('small')
    expect(visualPaneBucket(840, 430, true, 'medium')).toBe('medium')
    expect(visualPaneBucket(860, 450, true, 'small')).toBe('medium')
  })

  it('keeps invoice thumbnails eligible when account setup notices reduce body height', () => {
    expect(visualPaneBucket(900, 260, false)).toBeNull()
    expect(visualPaneBucket(900, 260, false, null, 'invoice')).toBe('small')
    expect(visualPaneBucket(620, 180, false, null, 'invoice')).toBe('small')
    expect(visualPaneBucket(620, 179, false, null, 'invoice')).toBeNull()
    expect(visualPaneBucket(610, 170, true, 'small', 'invoice')).toBe('small')
    expect(visualPaneBucket(610, 159, true, 'small', 'invoice')).toBeNull()
    expect(visualPaneBucket(599, 500, true, 'small', 'invoice')).toBeNull()
  })
})
