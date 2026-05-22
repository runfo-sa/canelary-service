import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export const STALE_THRESHOLD_MS = 5 * 60 * 1000

export function formatRelativeEs(date: Date, now: Date = new Date()): string {
  const diffMs = Math.max(0, now.getTime() - date.getTime())
  const sec = Math.floor(diffMs / 1000)
  if (sec < 60) return `hace ${sec}s`
  const min = Math.floor(sec / 60)
  if (min < 60) return `hace ${min} min`
  const hr = Math.floor(min / 60)
  const remMin = min % 60
  if (hr < 24) return remMin === 0 ? `hace ${hr} h` : `hace ${hr} h ${remMin} min`
  const days = Math.floor(hr / 24)
  return `hace ${days} d`
}

export function isStale(date: Date, now: Date = new Date()): boolean {
  return now.getTime() - date.getTime() > STALE_THRESHOLD_MS
}
