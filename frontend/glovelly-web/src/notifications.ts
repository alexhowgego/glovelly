import { toast } from 'sonner'

type NotificationOptions = {
  dedupeKey?: string
}

const successDuration = 5000
const infoDuration = 6000

function getToastOptions(duration: number, options?: NotificationOptions) {
  return {
    duration,
    ...(options?.dedupeKey ? { id: options.dedupeKey } : {}),
  }
}

// Use notifications for terminal outcomes that outlive their initiating UI. Keep validation,
// progress, durable warnings, and terminal feedback in still-open modals inline.
export const notifications = {
  success(message: string, options?: NotificationOptions) {
    return toast.success(message, getToastOptions(successDuration, options))
  },
  info(message: string, options?: NotificationOptions) {
    return toast.info(message, getToastOptions(infoDuration, options))
  },
  error(message: string, options?: NotificationOptions) {
    return toast.error(message, getToastOptions(Infinity, options))
  },
  dismiss(dedupeKey?: string) {
    toast.dismiss(dedupeKey)
  },
  resetSession() {
    toast.dismiss()
  },
}
