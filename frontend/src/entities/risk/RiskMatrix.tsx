import { cn } from "@/shared/lib/cn";
import {
  IMPACT_LABELS,
  PROBABILITY_LABELS,
  RISK_LEVEL_CELL,
  type RiskMatrixCell,
} from "./api";

/** Матрица вероятность×влияние 3×3.
 *
 * Вероятность — по вертикали (сверху «высокая», снизу «низкая»), влияние — по
 * горизонтали (слева «низкое», справа «высокое»). Цвет ячейки — зона критичности
 * (зелёная/жёлтая/красная), число — количество активных рисков в ячейке. */
export function RiskMatrix({
  cells,
  onCellClick,
  selected,
}: {
  cells: RiskMatrixCell[];
  onCellClick?: (cell: RiskMatrixCell) => void;
  selected?: { probability: number; impact: number } | null;
}) {
  const at = (probability: number, impact: number) =>
    cells.find((c) => c.probability === probability && c.impact === impact);
  const probs = [3, 2, 1]; // сверху вниз
  const impacts = [1, 2, 3]; // слева направо

  return (
    <div className="inline-block">
      <div className="flex">
        {/* Подпись оси Y */}
        <div className="flex items-center">
          <span className="rotate-180 whitespace-nowrap text-xs font-medium text-muted-foreground [writing-mode:vertical-rl]">
            Вероятность →
          </span>
        </div>
        <div>
          <div className="grid grid-cols-[auto_repeat(3,4rem)] gap-1">
            {probs.map((p) => (
              <div key={`row-${p}`} className="contents">
                <div className="flex w-20 items-center justify-end pr-2 text-xs text-muted-foreground">
                  {PROBABILITY_LABELS[p]}
                </div>
                {impacts.map((i) => {
                  const cell = at(p, i);
                  if (!cell) return <div key={`${p}-${i}`} />;
                  const isSel =
                    selected?.probability === p && selected?.impact === i;
                  return (
                    <button
                      key={`${p}-${i}`}
                      type="button"
                      disabled={!onCellClick}
                      onClick={() => onCellClick?.(cell)}
                      className={cn(
                        "flex h-16 flex-col items-center justify-center rounded-md border text-sm font-semibold transition-colors",
                        RISK_LEVEL_CELL[cell.level],
                        onCellClick && "cursor-pointer hover:ring-2 hover:ring-ring",
                        isSel && "ring-2 ring-ring",
                      )}
                      title={`Балл ${cell.score} · рисков: ${cell.count}`}
                    >
                      <span className="text-lg tabular">{cell.count}</span>
                      <span className="text-[10px] font-normal opacity-70">
                        балл {cell.score}
                      </span>
                    </button>
                  );
                })}
              </div>
            ))}
            {/* Подписи оси X */}
            <div />
            {impacts.map((i) => (
              <div
                key={`x-${i}`}
                className="w-16 pt-1 text-center text-xs text-muted-foreground"
              >
                {IMPACT_LABELS[i]}
              </div>
            ))}
          </div>
          <div className="pl-20 pt-1 text-center text-xs font-medium text-muted-foreground">
            Влияние →
          </div>
        </div>
      </div>
    </div>
  );
}
