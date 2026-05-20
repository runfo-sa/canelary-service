import { Status } from "@/types";

export interface StatusStyle {
  bg: string;
  text: string;
  dot: string;
}

export const statusColors: Record<Status, StatusStyle> = {
  [Status.Okay]: {
    bg: "bg-emerald-700",
    text: "text-white",
    dot: "bg-green-400",
  },
  [Status.Desactualizada]: {
    bg: "bg-rose-700",
    text: "text-white",
    dot: "bg-red-400",
  },
  [Status.Sobrantes]: {
    bg: "bg-amber-700",
    text: "text-white",
    dot: "bg-yellow-400",
  },
  [Status.DesactualizadaSobrantes]: {
    bg: "bg-rose-700",
    text: "text-white",
    dot: "bg-red-400",
  },
  [Status.MultipleInstalaciones]: {
    bg: "bg-indigo-800",
    text: "text-white",
    dot: "bg-purple-400",
  },
  [Status.NoInstalado]: {
    bg: "bg-gray-700",
    text: "text-white",
    dot: "bg-slate-400",
  },
};
