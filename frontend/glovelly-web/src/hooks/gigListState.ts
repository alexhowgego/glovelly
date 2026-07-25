import type { Gig, GigQuickFilter, GigSort, GigType } from '../types'

export type GigListFilters = {
  searchQuery: string
  quickFilter: GigQuickFilter
  showPastGigs: boolean
  sort: GigSort
  typeFilter: GigType | 'all'
}

export function getLocalDate(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function isNormallyHiddenPastGig(gig: Gig, today: string) {
  return gig.date < today && (gig.status === 'Completed' || gig.status === 'Cancelled')
}

export function getVisibleGigs(
  gigs: Gig[],
  clientNamesById: ReadonlyMap<string, string>,
  filters: GigListFilters,
  today: string
) {
  const query = filters.searchQuery.trim().toLowerCase()
  const sortDirection = filters.sort.direction === 'asc' ? 1 : -1
  const compareText = (left: string, right: string) => left.localeCompare(right)
  const compareNumber = (left: number, right: number) => left - right
  const getClientName = (gig: Gig) => clientNamesById.get(gig.clientId) ?? ''
  const getPriorityBucket = (gig: Gig) => {
    if (gig.status === 'Cancelled') return 5
    if (gig.status === 'Confirmed' && gig.date >= today) return 0
    if (gig.status === 'Completed' && !gig.isInvoiced && gig.date <= today) return 1
    if (gig.status === 'Confirmed' && !gig.isInvoiced && gig.date < today) return 2
    if (gig.status === 'Draft') return 3
    return 4
  }
  const comparePriority = (left: Gig, right: Gig) => {
    const bucketComparison = getPriorityBucket(left) - getPriorityBucket(right)
    if (bucketComparison !== 0) return bucketComparison
    return getPriorityBucket(left) === 0
      ? compareText(left.date, right.date)
      : compareText(right.date, left.date)
  }
  const compareByKey = (left: Gig, right: Gig) => {
    switch (filters.sort.key) {
      case 'client': return compareText(getClientName(left), getClientName(right))
      case 'fee': return compareNumber(left.fee, right.fee)
      case 'status': return compareText(left.status, right.status)
      case 'title': return compareText(left.title, right.title)
      case 'venue': return compareText(left.venue, right.venue)
      case 'priority': return comparePriority(left, right)
      case 'date':
      default: return compareText(left.date, right.date)
    }
  }

  return gigs
    .filter((gig) => filters.showPastGigs || !isNormallyHiddenPastGig(gig, today))
    .filter((gig) => filters.typeFilter === 'all' || gig.type === filters.typeFilter)
    .filter((gig) => {
      switch (filters.quickFilter) {
        case 'completed': return gig.status === 'Completed'
        case 'drafts': return gig.status === 'Draft'
        case 'uninvoiced': return !gig.isInvoiced && gig.status !== 'Cancelled'
        case 'upcoming': return gig.status !== 'Cancelled' && gig.date >= today
        case 'all':
        default: return true
      }
    })
    .filter((gig) => !query || [gig.title, gig.venue, gig.date, gig.status, gig.type, getClientName(gig)]
      .join(' ')
      .toLowerCase()
      .includes(query))
    .sort((left, right) => {
      const primaryComparison = compareByKey(left, right)
      if (primaryComparison !== 0) return primaryComparison * sortDirection
      return left.date.localeCompare(right.date)
        || left.title.localeCompare(right.title)
        || left.id.localeCompare(right.id)
    })
}

export function reconcileSelectedGigId(selectedGigId: string, visibleGigs: Gig[]) {
  return visibleGigs.some((gig) => gig.id === selectedGigId)
    ? selectedGigId
    : (visibleGigs[0]?.id ?? '')
}

export function getGigReveal(target: Gig, visibleGigs: Gig[], today: string) {
  if (visibleGigs.some((gig) => gig.id === target.id)) {
    return { clearFilters: false, showPastGigs: false }
  }

  return {
    clearFilters: true,
    showPastGigs: isNormallyHiddenPastGig(target, today),
  }
}
