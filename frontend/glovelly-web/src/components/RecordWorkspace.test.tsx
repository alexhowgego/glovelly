// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, expect, it } from 'vitest'
import { RecordWorkspace } from './RecordWorkspace'

afterEach(cleanup)

it.each(['workspace', 'gig-workspace', 'admin-workspace'] as const)(
  'shares pane geometry while preserving %s domain styles and section semantics', className => {
    const { container } = render(<RecordWorkspace className={className} sectionClassName="test-zone" label="Records">
      <div className="panel">Directory</div>
      <div className="panel record-overview">Overview</div>
      <div className="editor-slot">Editor</div>
    </RecordWorkspace>)
    expect(screen.getByRole('region', { name: 'Records' }).classList.contains('section-layout')).toBe(true)
    expect(container.querySelector(`section.test-zone > .record-workspace.${className}`)?.children).toHaveLength(3)
  },
)
