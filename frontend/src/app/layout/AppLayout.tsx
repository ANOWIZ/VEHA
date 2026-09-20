import { HelpCircle, LogOut } from "lucide-react";
import { useEffect } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";

import { ROLE_LABELS, hasAnyRole } from "@/entities/user/model";
import { useLogout, useMe } from "@/shared/auth/useAuth";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { OnboardingTour } from "@/app/onboarding/OnboardingTour";
import { isOnboardingDone, useOnboarding } from "@/app/onboarding/onboardingStore";
import { NAV } from "./nav";
import { NotificationsBell } from "./NotificationsBell";

export function AppLayout() {
  const { data: me } = useMe();
  const logout = useLogout();
  const navigate = useNavigate();
  const startTour = useOnboarding((s) => s.start);

  // Первый вход пользователя → автоматически показываем обучающий тур.
  useEffect(() => {
    if (me && !isOnboardingDone(me.username)) startTour();
  }, [me, startTour]);

  const onLogout = () => {
    logout();
    navigate("/login");
  };

  return (
    <div className="flex min-h-screen bg-background">
      <aside className="hidden w-64 shrink-0 flex-col bg-sidebar text-sidebar-foreground lg:flex">
        <div className="flex h-16 items-center gap-2.5 px-5">
          <div className="flex h-9 w-9 items-center justify-center rounded-md bg-sidebar-active font-display text-lg font-extrabold text-sidebar">
            В
          </div>
          <div className="leading-tight">
            <div className="font-display text-base font-bold text-white">Веха</div>
            <div className="text-[11px] text-sidebar-muted">управление проектами</div>
          </div>
        </div>
        <nav className="flex-1 overflow-y-auto px-3 py-2">
          {NAV.map((group) => {
            const items = group.items.filter(
              (i) => !i.roles || hasAnyRole(me, i.roles),
            );
            if (items.length === 0) return null;
            return (
              <div key={group.title} className="mb-5">
                <p className="px-3 pb-1.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-sidebar-muted">
                  {group.title}
                </p>
                {items.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.to === "/"}
                    data-tour={item.to}
                    className={({ isActive }) =>
                      cn(
                        "group relative flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                        isActive
                          ? "bg-white/5 text-white"
                          : "text-sidebar-foreground hover:bg-white/5 hover:text-white",
                      )
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <span
                          className={cn(
                            "absolute left-0 top-1/2 h-5 w-0.5 -translate-y-1/2 rounded-r bg-sidebar-active transition-opacity",
                            isActive ? "opacity-100" : "opacity-0",
                          )}
                        />
                        <item.icon
                          className={cn("h-4 w-4", isActive && "text-sidebar-active")}
                        />
                        {item.label}
                      </>
                    )}
                  </NavLink>
                ))}
              </div>
            );
          })}
        </nav>
        <div className="px-5 py-3 text-[10px] uppercase tracking-[0.1em] text-sidebar-muted">
          Веха · v0.1 · прототип
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center justify-between border-b bg-card px-6">
          <div className="lg:hidden font-display font-bold">Веха</div>
          <div className="ml-auto flex items-center gap-3">
            {me && (
              <div className="text-right">
                <p className="text-sm font-medium leading-tight">{me.full_name}</p>
                <p className="text-xs text-muted-foreground">
                  {me.roles.map((r) => ROLE_LABELS[r]).join(", ")}
                </p>
              </div>
            )}
            <Button
              variant="ghost"
              size="icon"
              data-tour="help"
              onClick={() => startTour()}
              title="Обучение по интерфейсу"
            >
              <HelpCircle className="h-4 w-4" />
            </Button>
            <span data-tour="notifications">
              <NotificationsBell />
            </span>
            <Button variant="ghost" size="icon" onClick={onLogout} title="Выйти">
              <LogOut className="h-4 w-4" />
            </Button>
          </div>
        </header>

        <OnboardingTour />

        <main className="flex-1 overflow-y-auto p-6">
          <div className="mx-auto max-w-7xl">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
