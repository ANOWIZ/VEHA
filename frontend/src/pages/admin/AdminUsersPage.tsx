import { useState } from "react";

import { ROLE_LABELS, type Role, canSeeFinancials } from "@/entities/user/model";
import { useSetCostRate, useUser, useUsers } from "@/entities/user/api";
import { ApiError } from "@/shared/api/types";
import { useMe } from "@/shared/auth/useAuth";
import { formatMoney } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { PageHeader } from "@/shared/ui/page";
import { PageLoader } from "@/shared/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";
import { toast } from "@/shared/ui/toast";

export function AdminUsersPage() {
  const { data: me } = useMe();
  const { data: users, isLoading } = useUsers();
  const showFin = canSeeFinancials(me);
  const [rateFor, setRateFor] = useState<string | null>(null);

  return (
    <div>
      <PageHeader
        title="Пользователи и ставки"
        description="Сотрудники из Keycloak/AD, роли и версионируемые ставки себестоимости"
      />
      {isLoading ? (
        <PageLoader />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ФИО</TableHead>
                <TableHead>Логин</TableHead>
                <TableHead>Подразделение</TableHead>
                <TableHead>Роли</TableHead>
                {showFin && <TableHead className="w-32" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {users?.items.map((u) => (
                <TableRow key={u.id}>
                  <TableCell className="font-medium">{u.full_name}</TableCell>
                  <TableCell className="font-mono text-xs text-muted-foreground">
                    {u.username}
                  </TableCell>
                  <TableCell className="text-sm">{u.department ?? "—"}</TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1">
                      {u.roles.map((r) => (
                        <Badge key={r} variant="secondary">
                          {ROLE_LABELS[r as Role] ?? r}
                        </Badge>
                      ))}
                    </div>
                  </TableCell>
                  {showFin && (
                    <TableCell>
                      <Button variant="outline" size="sm" onClick={() => setRateFor(u.id)}>
                        Ставка
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
      {rateFor && <CostRateDialog userId={rateFor} onClose={() => setRateFor(null)} />}
    </div>
  );
}

function CostRateDialog({ userId, onClose }: { userId: string; onClose: () => void }) {
  const { data: user } = useUser(userId);
  const setRate = useSetCostRate(userId);
  const [rate, setRate_] = useState("");
  const [validFrom, setValidFrom] = useState(new Date().toISOString().slice(0, 10));

  const save = async () => {
    if (!rate) {
      toast.error("Укажите ставку");
      return;
    }
    try {
      await setRate.mutateAsync({ cost_rate: rate, valid_from: validFrom });
      toast.success("Ставка назначена");
      onClose();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Ставка себестоимости · {user?.full_name ?? "…"}</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          Текущая действующая ставка:{" "}
          <span className="tabular font-medium text-foreground">
            {user?.current_cost_rate ? formatMoney(user.current_cost_rate) : "не задана"}
          </span>{" "}
          / час. Новая версия не редактирует прошлые записи (они нужны для расчёта
          себестоимости на дату).
        </p>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label>Ставка, ₽/час</Label>
            <Input
              type="number"
              value={rate}
              onChange={(e) => setRate_(e.target.value)}
              className="tabular"
            />
          </div>
          <div className="space-y-1.5">
            <Label>Действует с</Label>
            <Input type="date" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} />
          </div>
        </div>
        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button onClick={save} disabled={setRate.isPending}>
            Назначить
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
