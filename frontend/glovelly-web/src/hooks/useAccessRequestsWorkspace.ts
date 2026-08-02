import { useCallback, useState } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
  jsonRequestInit,
} from '../api'
import type {
  AccessRequest,
  AccessRequestApproval,
  AccessRequestApprovalResult,
  AccessRequestDeclineResult,
} from '../types'

type UseAccessRequestsWorkspaceOptions = {
  onSessionExpired: (message: string) => void
}

export function useAccessRequestsWorkspace({
  onSessionExpired,
}: UseAccessRequestsWorkspaceOptions) {
  const [accessRequests, setAccessRequests] = useState<AccessRequest[]>([])
  const [selectedAccessRequest, setSelectedAccessRequest] = useState<AccessRequest | null>(null)
  const [accessRequestStatus, setAccessRequestStatus] = useState('')
  const [isAccessRequestLoading, setIsAccessRequestLoading] = useState(false)

  const resetAccessRequestsWorkspace = useCallback(() => {
    setAccessRequests([])
    setSelectedAccessRequest(null)
    setAccessRequestStatus('')
    setIsAccessRequestLoading(false)
  }, [])

  const loadAccessRequests = useCallback(
    async (requestedId?: string) => {
      setIsAccessRequestLoading(true)

      try {
        const response = await fetchWithSession(buildApiUrl('/admin/access-requests/'))
        if (
          handleSessionExpired(
            response,
            onSessionExpired,
            'Your session expired. Sign in again to review access requests.'
          )
        ) {
          return
        }

        if (response.status === 403) {
          setAccessRequests([])
          setSelectedAccessRequest(null)
          setAccessRequestStatus('Administrator access is required to review access requests.')
          return
        }

        if (!response.ok) {
          throw new Error(await getResponseErrorMessage(response, 'Unable to load access requests.'))
        }

        const requests = (await response.json()) as AccessRequest[]
        setAccessRequests(requests)

        const selectedFromPending = requestedId
          ? requests.find((request) => request.id === requestedId)
          : null
        if (selectedFromPending) {
          setSelectedAccessRequest(selectedFromPending)
          setAccessRequestStatus('Review the selected access request.')
          return
        }

        if (requestedId) {
          const requestResponse = await fetchWithSession(
            buildApiUrl(`/admin/access-requests/${requestedId}`)
          )
          if (
            handleSessionExpired(
              requestResponse,
              onSessionExpired,
              'Your session expired. Sign in again to review access requests.'
            )
          ) {
            return
          }

          if (requestResponse.status === 403) {
            setSelectedAccessRequest(null)
            setAccessRequestStatus('Administrator access is required to review access requests.')
            return
          }

          if (requestResponse.status === 404) {
            setSelectedAccessRequest(null)
            setAccessRequestStatus('This access request was not found or is no longer available.')
            return
          }

          if (!requestResponse.ok) {
            throw new Error(
              await getResponseErrorMessage(requestResponse, 'Unable to load this access request.')
            )
          }

          const request = (await requestResponse.json()) as AccessRequest
          setSelectedAccessRequest(request)
          setAccessRequestStatus(
            request.status === 'Pending'
              ? 'Review the selected access request.'
              : `This access request has already been ${request.status.toLowerCase()}.`
          )
          return
        }

        setSelectedAccessRequest((current) =>
          current && requests.some((request) => request.id === current.id)
            ? requests.find((request) => request.id === current.id) ?? null
            : requests[0] ?? null
        )
        setAccessRequestStatus(
          requests.length > 0
            ? 'Review pending access requests.'
            : 'There are no pending access requests.'
        )
      } catch (error) {
        setAccessRequests([])
        setSelectedAccessRequest(null)
        setAccessRequestStatus(
          error instanceof Error ? error.message : 'Unable to load access requests right now.'
        )
      } finally {
        setIsAccessRequestLoading(false)
      }
    },
    [onSessionExpired]
  )

  const selectAccessRequest = (request: AccessRequest) => {
    setSelectedAccessRequest(request)
    setAccessRequestStatus('Review the selected access request.')
  }

  const reportAccessRequestStatus = useCallback((message: string) => {
    setAccessRequestStatus(message)
  }, [])

  const approveAccessRequest = async (approval: AccessRequestApproval) => {
    if (!selectedAccessRequest) {
      return
    }

    setIsAccessRequestLoading(true)
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/admin/access-requests/${selectedAccessRequest.id}/approve`),
        jsonRequestInit('POST', approval)
      )
      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to review access requests.'
        )
      ) {
        return
      }

      if (response.status === 403) {
        setAccessRequestStatus('Administrator access is required to approve access requests.')
        return
      }

      if (response.status === 404) {
        setAccessRequestStatus('This access request was not found or is no longer available.')
        return
      }

      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to approve access request.'))
      }

      const result = (await response.json()) as AccessRequestApprovalResult
      setSelectedAccessRequest(result.accessRequest)
      setAccessRequests((current) =>
        current.filter((request) => request.id !== result.accessRequest.id)
      )
      setAccessRequestStatus(
        result.decisionApplied
          ? result.userCreated
            ? result.invitationEmailSent === false
              ? 'Access approved, but the invitation email could not be sent.'
              : result.invitationEmailSent === true
                ? 'Access approved and invitation email sent.'
                : 'Access approved.'
            : result.existingUser
              ? 'This requester already has a user account.'
              : 'This access request has already been decided.'
          : 'This access request has already been decided.'
      )
    } catch (error) {
      setAccessRequestStatus(
        error instanceof Error ? error.message : 'Unable to approve access request right now.'
      )
    } finally {
      setIsAccessRequestLoading(false)
    }
  }

  const declineAccessRequest = async (decisionNote?: string) => {
    if (!selectedAccessRequest) {
      return
    }

    setIsAccessRequestLoading(true)
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/admin/access-requests/${selectedAccessRequest.id}/decline`),
        jsonRequestInit('POST', { decisionNote: decisionNote || null })
      )
      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to review access requests.'
        )
      ) {
        return
      }

      if (response.status === 403) {
        setAccessRequestStatus('Administrator access is required to decline access requests.')
        return
      }

      if (response.status === 404) {
        setAccessRequestStatus('This access request was not found or is no longer available.')
        return
      }

      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to decline access request.'))
      }

      const result = (await response.json()) as AccessRequestDeclineResult
      setSelectedAccessRequest(result.accessRequest)
      setAccessRequests((current) =>
        current.filter((request) => request.id !== result.accessRequest.id)
      )
      setAccessRequestStatus(
        result.decisionApplied
          ? 'Access request declined.'
          : 'This access request has already been decided.'
      )
    } catch (error) {
      setAccessRequestStatus(
        error instanceof Error ? error.message : 'Unable to decline access request right now.'
      )
    } finally {
      setIsAccessRequestLoading(false)
    }
  }

  return {
    accessRequestStatus,
    accessRequests,
    approveAccessRequest,
    declineAccessRequest,
    isAccessRequestLoading,
    loadAccessRequests,
    reportAccessRequestStatus,
    resetAccessRequestsWorkspace,
    selectAccessRequest,
    selectedAccessRequest,
  }
}
