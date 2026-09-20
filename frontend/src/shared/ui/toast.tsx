import { CheckCircle2, Info, XCircle } from "lucide-react";
import { create } from "zustand";

import { cn } from "@/shared/lib/cn";

type ToastKind = "success" | "error" | "info";

interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

interface ToastState {
  toasts: Toast[];
  push: (kind: ToastKind, message: string) => void;
  remove: (id: number) => void;
}

let counter = 0;

const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  push: (kind, message) => {
    const id = ++counter;
    set((s) => ({ toasts: [...s.toasts, { id, kind, message }] }));
    setTimeout(() => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })), 4000);
  },
  remove: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));

/** Императивный API уведомлений: toast.success("Сохранено"). */
export const toast = {
  success: (m: string) => useToastStore.getState().push("success", m),
  error: (m: string) => useToastStore.getState().push("error", m),
  info: (m: string) => useToastStore.getState().push("info", m),
};

const icons = { success: CheckCircle2, error: XCircle, info: Info };
const tones = {
  success: "border-success/40 text-success",
  error: "border-destructive/40 text-destructive",
  info: "border-border text-foreground",
};

export function Toaster() {
  const toasts = useToastStore((s) => s.toasts);
  const remove = useToastStore((s) => s.remove);
  return (
    <div className="fixed bottom-4 right-4 z-[100] flex w-80 flex-col gap-2">
      {toasts.map((t) => {
        const Icon = icons[t.kind];
        return (
          <div
            key={t.id}
            onClick={() => remove(t.id)}
            className={cn(
              "flex cursor-pointer items-start gap-2 rounded-md border bg-card p-3 text-sm shadow-lg animate-in slide-in-from-right",
              tones[t.kind],
            )}
          >
            <Icon className="mt-0.5 h-4 w-4 shrink-0" />
            <span className="text-card-foreground">{t.message}</span>
          </div>
        );
      })}
    </div>
  );
}
