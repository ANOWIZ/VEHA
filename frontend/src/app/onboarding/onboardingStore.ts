import { create } from "zustand";

/** Состояние обучающего тура (активность + текущий шаг). Завершённость хранится
 * в localStorage по пользователю — тур показывается при первом входе. */
interface OnboardingState {
  active: boolean;
  step: number;
  start: () => void;
  stop: () => void;
  setStep: (n: number) => void;
}

export const useOnboarding = create<OnboardingState>((set) => ({
  active: false,
  step: 0,
  start: () => set({ active: true, step: 0 }),
  stop: () => set({ active: false, step: 0 }),
  setStep: (n) => set({ step: n }),
}));

const doneKey = (username: string) => `veha-onboarding-done:${username}`;

export function isOnboardingDone(username?: string): boolean {
  return !!username && localStorage.getItem(doneKey(username)) === "1";
}

export function markOnboardingDone(username?: string): void {
  if (username) localStorage.setItem(doneKey(username), "1");
}
