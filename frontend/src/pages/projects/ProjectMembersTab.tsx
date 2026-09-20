import { Plus, Trash2 } from "lucide-react";
import { useState } from "react";

import {
  MEMBER_ROLE_LABELS,
  type ProjectDetail,
} from "@/entities/project/model";
import { useAddMember, useRemoveMember } from "@/entities/project/api";
import { useUsers, useUserMap } from "@/entities/user/api";
import { canSeeFinancials } from "@/entities/user/model";
import { useMe } from "@/shared/auth/useAuth";
import { ApiError } from "@/shared/api/types";
import { formatMoney } from "@/shared/lib/format";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { EmptyState } from "@/shared/ui/page";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";
import { toast } from "@/shared/ui/toast";

export function ProjectMembersTab({
  project,
  canManage,
}: {
  project: ProjectDetail;
  canManage: boolean;
}) {
  const { data: me } = useMe();
  const { data: users } = useUsers();
  const userMap = useUserMap();
  const addMember = useAddMember(project.id);
  const removeMember = useRemoveMember(project.id);
  const showFin = canSeeFinancials(me);

  const [userId, setUserId] = useState("");
  const [role, setRole] = useState("engineer");
  const [billRate, setBillRate] = useState("0");

  const add = async () => {
    if (!userId) {
      toast.error("Выберите сотрудника");
      return;
    }
    try {
      await addMember.mutateAsync({ user_id: userId, role, bill_rate: billRate || "0" });
      setUserId("");
      setBillRate("0");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const onRemove = (memberId: string, name: string) => {
    if (!window.confirm(`Удалить участника «${name}» из проекта?`)) return;
    removeMember.mutate(memberId, {
      onSuccess: () => toast.success("Участник удалён"),
      onError: (e) => toast.error(e instanceof ApiError ? e.message : "Не удалось удалить"),
    });
  };

  return (
    <div className="space-y-4">
      {canManage && (
        <div className="flex flex-wrap items-end gap-2">
          <div className="min-w-[220px] flex-1">
            <Select value={userId} onValueChange={setUserId}>
              <SelectTrigger>
                <SelectValue placeholder="Сотрудник…" />
              </SelectTrigger>
              <SelectContent>
                {users?.items.map((u) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.full_name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Select value={role} onValueChange={setRole}>
            <SelectTrigger className="w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {Object.entries(MEMBER_ROLE_LABELS).map(([v, l]) => (
                <SelectItem key={v} value={v}>
                  {l}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {showFin && (
            <Input
              type="number"
              placeholder="Ставка продажи, ₽/ч"
              value={billRate}
              onChange={(e) => setBillRate(e.target.value)}
              className="w-44 tabular"
            />
          )}
          <Button onClick={add} disabled={addMember.isPending}>
            <Plus className="h-4 w-4" /> Добавить
          </Button>
        </div>
      )}

      {project.members.length === 0 ? (
        <EmptyState title="Участников нет" />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Сотрудник</TableHead>
                <TableHead>Роль на проекте</TableHead>
                {showFin && <TableHead className="text-right">Ставка продажи</TableHead>}
                {canManage && <TableHead className="w-12" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {project.members.map((m) => (
                <TableRow key={m.id}>
                  <TableCell className="font-medium">
                    {userMap.get(m.user_id)?.full_name ?? "—"}
                  </TableCell>
                  <TableCell>{MEMBER_ROLE_LABELS[m.role] ?? m.role}</TableCell>
                  {showFin && (
                    <TableCell className="text-right tabular">
                      {formatMoney(m.bill_rate)}
                    </TableCell>
                  )}
                  {canManage && (
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => onRemove(m.id, userMap.get(m.user_id)?.full_name ?? "—")}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
