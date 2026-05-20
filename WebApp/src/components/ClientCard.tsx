import { useState } from "react"
import { ChevronDown } from "lucide-react"
import { Card, CardContent } from "@/components/ui/card"
import { statusColors } from "@/lib/statusColors"
import { StatusLabel, TipoDiff, type ClientStatus, type EtiquetaCliente } from "@/types"
import { cn } from "@/lib/utils"

const dateFmt = new Intl.DateTimeFormat("es-AR", {
  dateStyle: "short",
  timeStyle: "medium",
})

interface Props {
  client: ClientStatus
}

export function ClientCard({ client }: Props) {
  const style = statusColors[client.estado]
  const fecha = dateFmt.format(new Date(client.ultimaConexion))
  const etiquetas = client.etiquetas ?? []
  const desactualizadas = etiquetas.filter((e) => e.tipo === TipoDiff.Desactualizada)
  const sobrantes = etiquetas.filter((e) => e.tipo === TipoDiff.Sobrante)
  const [open, setOpen] = useState(false)

  return (
    <Card className={cn(style.bg, style.text, "flex flex-col")}>
      <CardContent className="flex flex-col gap-3 p-5 pt-5">
        <div className="font-mono text-2xl font-semibold tracking-tight break-all">
          {client.cliente}
        </div>

        <div className="flex items-center gap-2 text-sm font-medium uppercase tracking-wide opacity-95">
          <span className={cn("inline-block h-2 w-2 rounded-full", style.dot)} />
          {StatusLabel[client.estado]}
        </div>

        <div className="text-xs opacity-80 mt-auto pt-2 border-t border-white/20">
          Última conexión: {fecha}
        </div>
      </CardContent>

      {etiquetas.length > 0 && (
        <div className="border-t border-white/20">
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            aria-expanded={open}
            className="flex w-full items-center justify-between px-5 py-2 text-xs font-medium uppercase tracking-wide opacity-90 hover:opacity-100 transition"
          >
            <span>Etiquetas modificadas ({etiquetas.length})</span>
            <ChevronDown
              className={cn("h-4 w-4 transition-transform", open && "rotate-180")}
            />
          </button>

          {open && (
            <div className="px-5 pb-4 pt-1 text-xs space-y-3">
              {desactualizadas.length > 0 && (
                <DiffList title="Desactualizadas" items={desactualizadas} />
              )}
              {sobrantes.length > 0 && (
                <DiffList title="Sobrantes" items={sobrantes} />
              )}
            </div>
          )}
        </div>
      )}
    </Card>
  )
}

function DiffList({ title, items }: { title: string; items: EtiquetaCliente[] }) {
  return (
    <div>
      <div className="font-semibold uppercase tracking-wide opacity-80 mb-1">
        {title}
      </div>
      <ul className="font-mono space-y-0.5 break-all">
        {items.map((e) => (
          <li key={e.id ?? e.nombre} className="opacity-95">
            {e.nombre}
          </li>
        ))}
      </ul>
    </div>
  )
}
