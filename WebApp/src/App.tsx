import { useState } from "react"
import { ClientCard } from "@/components/ClientCard"
import { RefreshControl } from "@/components/RefreshControl"
import { usePollingClients, type PollingInterval } from "@/hooks/usePollingClients"
import { useNow } from "@/hooks/useNow"

function App() {
  const [interval, setInterval] = useState<PollingInterval>(60)
  const { data, loading, error, lastFetch, refresh } = usePollingClients(interval)
  const now = useNow()

  return (
    <div className="min-h-screen px-4 py-6 sm:px-6 lg:px-8">
      <header className="mx-auto mb-6 flex max-w-7xl flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">
            Canelary Dashboard
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Estado de los puestos reportados
          </p>
        </div>
        <RefreshControl
          interval={interval}
          onIntervalChange={setInterval}
          onRefresh={refresh}
          loading={loading}
          lastFetch={lastFetch}
        />
      </header>

      <main className="mx-auto max-w-7xl">
        {error && (
          <div className="mb-4 rounded-md border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800 dark:border-red-700 dark:bg-red-950 dark:text-red-200">
            Error al cargar: {error}
          </div>
        )}

        {!error && data.length === 0 && !loading && (
          <div className="rounded-md border border-dashed border-slate-300 px-6 py-12 text-center text-slate-500 dark:border-slate-700">
            Sin clientes registrados.
          </div>
        )}

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {data.map((client) => (
            <ClientCard key={client.id} client={client} now={now} />
          ))}
        </div>
      </main>
    </div>
  )
}

export default App
