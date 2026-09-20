import { Bell, CheckCheck } from "lucide-react";
import { useState } from "react";

import {
  useMarkAllRead,
  useNotifications,
  useUnreadCount,
} from "@/entities/notification/api";
import { cn } from "@/shared/lib/cn";
import { formatDateTime } from "@/shared/lib/format";
import { Button } from "@/shared/ui/button";

export function NotificationsBell() {
  const [open, setOpen] = useState(false);
  const { data: notifications } = useNotifications();
  const { data: unread } = useUnreadCount();
  const markAll = useMarkAllRead();
  const count = unread?.count ?? 0;

  return (
    <div className="relative">
      <Button variant="ghost" size="icon" onClick={() => setOpen((v) => !v)} title="Уведомления">
        <Bell className="h-4 w-4" />
        {count > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-accent px-1 text-[10px] font-bold text-accent-foreground tabular">
            {count}
          </span>
        )}
      </Button>

      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />
          <div className="absolute right-0 top-11 z-50 w-80 rounded-lg border bg-card shadow-lg">
            <div className="flex items-center justify-between border-b px-3 py-2">
              <span className="text-sm font-semibold">Уведомления</span>
              {count > 0 && (
                <button
                  className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
                  onClick={() => markAll.mutate()}
                >
                  <CheckCheck className="h-3.5 w-3.5" /> Прочитать все
                </button>
              )}
            </div>
            <div className="max-h-80 overflow-auto">
              {(notifications ?? []).length === 0 ? (
                <p className="px-3 py-6 text-center text-sm text-muted-foreground">
                  Нет уведомлений
                </p>
              ) : (
                notifications!.map((n) => (
                  <div
                    key={n.id}
                    className={cn(
                      "border-b px-3 py-2.5 text-sm last:border-0",
                      !n.is_read && "bg-accent/5",
                    )}
                  >
                    <div className="flex items-center gap-2">
                      {!n.is_read && <span className="h-1.5 w-1.5 rounded-full bg-accent" />}
                      <span className="font-medium">{n.title}</span>
                    </div>
                    {n.body && <p className="mt-0.5 text-muted-foreground">{n.body}</p>}
                    <p className="mt-1 text-[11px] text-muted-foreground tabular">
                      {formatDateTime(n.created_at)}
                    </p>
                  </div>
                ))
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
