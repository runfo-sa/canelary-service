import { useCallback, useEffect, useRef, useState } from "react"
import { fetchClients } from "@/lib/api"
import type { ClientStatus } from "@/types"

export type PollingInterval = 30 | 60 | 300 | 0

interface State {
  data: ClientStatus[]
  loading: boolean
  error: string | null
  lastFetch: Date | null
}

export function usePollingClients(intervalSeconds: PollingInterval) {
  const [state, setState] = useState<State>({
    data: [],
    loading: true,
    error: null,
    lastFetch: null,
  })

  const abortRef = useRef<AbortController | null>(null)

  const refresh = useCallback(async () => {
    abortRef.current?.abort()
    const controller = new AbortController()
    abortRef.current = controller

    setState((s) => ({ ...s, loading: true }))
    try {
      const data = await fetchClients(controller.signal)
      if (controller.signal.aborted) return
      setState({ data, loading: false, error: null, lastFetch: new Date() })
    } catch (err) {
      if (controller.signal.aborted) return
      const message = err instanceof Error ? err.message : "Error desconocido"
      setState((s) => ({ ...s, loading: false, error: message }))
    }
  }, [])

  useEffect(() => {
    refresh()
    return () => abortRef.current?.abort()
  }, [refresh])

  useEffect(() => {
    if (intervalSeconds === 0) return
    const id = setInterval(refresh, intervalSeconds * 1000)
    return () => clearInterval(id)
  }, [intervalSeconds, refresh])

  return { ...state, refresh }
}
