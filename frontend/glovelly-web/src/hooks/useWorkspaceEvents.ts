import { useEffect, useRef } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { buildApiUrl } from '../api'

export type WorkspaceEvent = {
  scope: string
  action: string
  entityId: string | null
  occurredAtUtc: string
}

type UseWorkspaceEventsOptions = {
  enabled: boolean
  onWorkspaceChanged: (event: WorkspaceEvent) => void
}

export function useWorkspaceEvents({
  enabled,
  onWorkspaceChanged,
}: UseWorkspaceEventsOptions) {
  const onWorkspaceChangedRef = useRef(onWorkspaceChanged)

  useEffect(() => {
    onWorkspaceChangedRef.current = onWorkspaceChanged
  }, [onWorkspaceChanged])

  useEffect(() => {
    if (!enabled) {
      return
    }

    const connection = new HubConnectionBuilder()
      .withUrl(buildApiUrl('/workspace-events'), {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('workspaceChanged', (event: WorkspaceEvent) => {
      onWorkspaceChangedRef.current(event)
    })

    void connection.start().catch(() => {
      // Focus refresh remains the fallback if the real-time connection cannot start.
    })

    return () => {
      connection.off('workspaceChanged')
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop()
      }
    }
  }, [enabled])
}
