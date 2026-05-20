import type { ClientStatus } from "@/types"

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ""

export async function fetchClients(signal?: AbortSignal): Promise<ClientStatus[]> {
  const res = await fetch(`${API_BASE_URL}/clients`, { signal })
  if (!res.ok) {
    throw new Error(`HTTP ${res.status} - ${res.statusText}`)
  }
  return res.json()
}
