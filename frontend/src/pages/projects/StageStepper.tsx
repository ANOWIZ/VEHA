import { Check } from "lucide-react";

import { STAGE_LABELS, STAGE_ORDER, type Stage } from "@/entities/project/model";
import { cn } from "@/shared/lib/cn";

/** Горизонтальный степпер стадий жизненного цикла проекта. */
export function StageStepper({ current }: { current: Stage }) {
  const currentIdx = STAGE_ORDER.indexOf(current);
  return (
    <div className="flex items-center overflow-x-auto pb-2">
      {STAGE_ORDER.map((stage, i) => {
        const done = i < currentIdx;
        const active = i === currentIdx;
        return (
          <div key={stage} className="flex items-center">
            <div className="flex flex-col items-center gap-1.5">
              <div
                className={cn(
                  "flex h-8 w-8 items-center justify-center rounded-full border-2 text-xs font-semibold",
                  done && "border-success bg-success text-success-foreground",
                  active && "border-primary bg-primary text-primary-foreground",
                  !done && !active && "border-border bg-card text-muted-foreground",
                )}
              >
                {done ? <Check className="h-4 w-4" /> : i + 1}
              </div>
              <span
                className={cn(
                  "max-w-[80px] text-center text-[11px] leading-tight",
                  active ? "font-medium text-foreground" : "text-muted-foreground",
                )}
              >
                {STAGE_LABELS[stage]}
              </span>
            </div>
            {i < STAGE_ORDER.length - 1 && (
              <div
                className={cn(
                  "mx-1 h-0.5 w-8 sm:w-12",
                  i < currentIdx ? "bg-success" : "bg-border",
                )}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}
