import { useState } from "react";
import { useNavigate } from "react-router-dom";

import { useClients } from "@/entities/client/api";
import { useCreateProject } from "@/entities/project/api";
import { PROJECT_TYPE_LABELS, type ProjectType } from "@/entities/project/model";
import { useUsers } from "@/entities/user/api";
import { ApiError } from "@/shared/api/types";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import { Spinner } from "@/shared/ui/spinner";
import { toast } from "@/shared/ui/toast";

export function CreateProjectDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const navigate = useNavigate();
  const { data: clients } = useClients();
  const { data: users } = useUsers();
  const create = useCreateProject();

  const [name, setName] = useState("");
  const [clientId, setClientId] = useState("");
  const [type, setType] = useState<ProjectType>("implementation");
  const [managerId, setManagerId] = useState("");
  const [budget, setBudget] = useState("0");

  const reset = () => {
    setName("");
    setClientId("");
    setManagerId("");
    setBudget("0");
    setType("implementation");
  };

  const submit = async () => {
    if (!name || !clientId || !managerId) {
      toast.error("Заполните название, клиента и руководителя");
      return;
    }
    try {
      const project = await create.mutateAsync({
        name,
        client_id: clientId,
        type,
        manager_id: managerId,
        budget_revenue: budget || "0",
      });
      toast.success(`Проект ${project.code} создан`);
      onOpenChange(false);
      reset();
      navigate(`/projects/${project.id}`);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Не удалось создать проект");
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Новый проект</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label>Название</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Внедрение СЭД в Демо-клиент 01" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Клиент</Label>
              <Select value={clientId} onValueChange={setClientId}>
                <SelectTrigger>
                  <SelectValue placeholder="Выберите…" />
                </SelectTrigger>
                <SelectContent>
                  {clients?.items.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Тип</Label>
              <Select value={type} onValueChange={(v) => setType(v as ProjectType)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(PROJECT_TYPE_LABELS).map(([v, l]) => (
                    <SelectItem key={v} value={v}>
                      {l}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Руководитель проекта</Label>
              <Select value={managerId} onValueChange={setManagerId}>
                <SelectTrigger>
                  <SelectValue placeholder="Выберите…" />
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
            <div className="space-y-1.5">
              <Label>Бюджет (выручка), ₽</Label>
              <Input
                type="number"
                value={budget}
                onChange={(e) => setBudget(e.target.value)}
                className="tabular"
              />
            </div>
          </div>
        </div>
        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Отмена
          </Button>
          <Button onClick={submit} disabled={create.isPending}>
            {create.isPending && <Spinner />} Создать
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
