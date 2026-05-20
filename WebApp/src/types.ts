export const Status = {
  Okay: 0,
  Desactualizada: 1,
  Sobrantes: 2,
  DesactualizadaSobrantes: 3,
  MultipleInstalaciones: 4,
  NoInstalado: 5,
} as const;

export type Status = (typeof Status)[keyof typeof Status];

export const TipoDiff = {
  Desactualizada: 0,
  Sobrante: 1,
} as const;

export type TipoDiff = (typeof TipoDiff)[keyof typeof TipoDiff];

export interface EtiquetaCliente {
  id: number;
  estadoClienteId: number;
  nombre: string;
  tipo: TipoDiff;
}

export interface ClientStatus {
  id: number;
  cliente: string;
  estado: Status;
  ultimaConexion: string;
  etiquetas: EtiquetaCliente[];
}

export const StatusLabel: Record<Status, string> = {
  [Status.Okay]: "Okay",
  [Status.Desactualizada]: "Desactualizado",
  [Status.Sobrantes]: "Sobrantes",
  [Status.DesactualizadaSobrantes]: "Desactualizado + Sobrantes",
  [Status.MultipleInstalaciones]: "Pi4 - Múltiples Instalaciones",
  [Status.NoInstalado]: "Pi4 - No Instalado",
};
