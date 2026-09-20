import { useLayoutEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";

import { useMe } from "@/shared/auth/useAuth";
import { Button } from "@/shared/ui/button";
import { buildStepsForRoles } from "./steps";
import { markOnboardingDone, useOnboarding } from "./onboardingStore";

/** Интерактивный обучающий тур: затемнение + «прожектор» на целевом элементе и
 * подсказка с шагами. Адаптируется под роль пользователя (шаги из меню). */
export function OnboardingTour() {
  const { data: me } = useMe();
  const { active, step, stop, setStep } = useOnboarding();
  const steps = useMemo(() => (me ? buildStepsForRoles(me.roles) : []), [me]);
  const [rect, setRect] = useState<DOMRect | null>(null);

  const current = steps[step];

  useLayoutEffect(() => {
    if (!active || !current) {
      setRect(null);
      return;
    }
    const update = () => {
      if (!current.target) {
        setRect(null);
        return;
      }
      const el = document.querySelector(`[data-tour="${current.target}"]`);
      setRect(el ? el.getBoundingClientRect() : null);
    };
    update();
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, true);
    return () => {
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
    };
  }, [active, current]);

  if (!active || !me || !current) return null;

  const isLast = step === steps.length - 1;
  const finish = () => {
    markOnboardingDone(me.username);
    stop();
  };

  // Позиция карточки: под подсвеченным элементом либо по центру (шаги без цели).
  const tipStyle: React.CSSProperties = rect
    ? {
        top: Math.min(rect.bottom + 10, window.innerHeight - 230),
        left: Math.min(Math.max(rect.left, 12), window.innerWidth - 360),
      }
    : { top: "50%", left: "50%", transform: "translate(-50%, -50%)" };

  return createPortal(
    <div className="fixed inset-0 z-[100]">
      {rect ? (
        // «Прожектор»: рамка вокруг цели + затемнение остального через box-shadow.
        <div
          style={{
            position: "fixed",
            top: rect.top - 6,
            left: rect.left - 6,
            width: rect.width + 12,
            height: rect.height + 12,
            borderRadius: 8,
            boxShadow: "0 0 0 9999px rgba(15, 23, 42, 0.62)",
            border: "2px solid #ffffff",
            transition: "all .2s ease",
            pointerEvents: "none",
          }}
        />
      ) : (
        <div className="fixed inset-0 bg-slate-900/60" />
      )}

      <div
        className="fixed w-[340px] rounded-lg border bg-card p-4 shadow-xl"
        style={tipStyle}
      >
        <div className="mb-1 text-xs text-muted-foreground">
          Шаг {step + 1} из {steps.length}
        </div>
        <h3 className="text-base font-semibold">{current.title}</h3>
        <p className="mt-1.5 text-sm text-muted-foreground">{current.body}</p>
        <div className="mt-4 flex items-center justify-between gap-2">
          <button
            type="button"
            onClick={finish}
            className="text-xs text-muted-foreground hover:underline"
          >
            Пропустить
          </button>
          <div className="flex gap-2">
            {step > 0 && (
              <Button variant="outline" size="sm" onClick={() => setStep(step - 1)}>
                Назад
              </Button>
            )}
            {isLast ? (
              <Button size="sm" onClick={finish}>
                Готово
              </Button>
            ) : (
              <Button size="sm" onClick={() => setStep(step + 1)}>
                Далее
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>,
    document.body,
  );
}
