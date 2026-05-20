import { RefreshCw } from "lucide-react"
import { Button } from "@/components/ui/button"
import type { PollingInterval } from "@/hooks/usePollingClients"

interface Props {
  interval: PollingInterval
  onIntervalChange: (value: PollingInterval) => void
  onRefresh: () => void
  loading: boolean
  lastFetch: Date | null
}

const OPTIONS: { value: PollingInterval; label: string }[] = [
  { value: 30, label: "30 s" },
  { value: 60, label: "60 s" },
  { value: 300, label: "5 min" },
  { value: 0, label: "Off" },
]

const timeFmt = new Intl.DateTimeFormat("es-AR", { timeStyle: "medium" })

export function RefreshControl({
  interval,
  onIntervalChange,
  onRefresh,
  loading,
  lastFetch,
}: Props) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <label className="flex items-center gap-2 text-sm">
        <span className="text-slate-500 dark:text-slate-400">Refresh:</span>
        <select
          value={interval}
          onChange={(e) => onIntervalChange(Number(e.target.value) as PollingInterval)}
          className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-800"
        >
          {OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </label>

      <Button variant="outline" onClick={onRefresh} disabled={loading}>
        <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
        Actualizar
      </Button>

      {lastFetch && (
        <span className="text-xs text-slate-500 dark:text-slate-400">
          Última: {timeFmt.format(lastFetch)}
        </span>
      )}
    </div>
  )
}
