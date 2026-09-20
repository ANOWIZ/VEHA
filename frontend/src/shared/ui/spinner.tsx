import { Loader2 } from "lucide-react";

import { cn } from "@/shared/lib/cn";

export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn("h-4 w-4 animate-spin", className)} />;
}

export function PageLoader({ label = "Загрузка…" }: { label?: string }) {
  return (
    <div className="flex h-64 flex-col items-center justify-center gap-3 text-muted-foreground">
      <Loader2 className="h-7 w-7 animate-spin" />
      <span className="text-sm">{label}</span>
    </div>
  );
}
