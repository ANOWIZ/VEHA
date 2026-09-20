import { Pencil, Plus, Search, ShieldAlert, Trash2 } from "lucide-react";
import { useState } from "react";

import {
  type Client,
  useClients,
  useCreateClient,
  useDeleteClient,
  useUpdateClient,
} from "@/entities/client/api";
import { useCreateVendor, useVendors } from "@/entities/catalog/api";
import { ApiError } from "@/shared/api/types";
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
import { EmptyState, PageHeader } from "@/shared/ui/page";
import { PageLoader } from "@/shared/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/shared/ui/tabs";
import { toast } from "@/shared/ui/toast";

export function DirectoriesPage() {
  return (
    <div>
      <PageHeader
        title="Справочники"
        description="Мастер-данные: заказчики и вендоры ПО/оборудования"
      />
      <Tabs defaultValue="clients">
        <TabsList>
          <TabsTrigger value="clients">Клиенты</TabsTrigger>
          <TabsTrigger value="vendors">Вендоры</TabsTrigger>
        </TabsList>
        <TabsContent value="clients">
          <ClientsTab />
        </TabsContent>
        <TabsContent value="vendors">
          <VendorsTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function ClientsTab() {
  const [q, setQ] = useState("");
  const { data, isLoading } = useClients(q || undefined);
  const del = useDeleteClient();
  const [edit, setEdit] = useState<Client | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const remove = async (c: Client) => {
    if (!confirm(`Удалить клиента «${c.name}»?`)) return;
    try {
      await del.mutateAsync(c.id);
      toast.success("Клиент удалён");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <div className="relative max-w-xs flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Поиск по названию или ИНН…"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            className="pl-8"
          />
        </div>
        <Button className="ml-auto" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4" /> Новый клиент
        </Button>
      </div>

      {isLoading ? (
        <PageLoader />
      ) : !data || data.items.length === 0 ? (
        <EmptyState title="Клиентов не найдено" />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Наименование</TableHead>
                <TableHead>ИНН</TableHead>
                <TableHead>Отрасль</TableHead>
                <TableHead>КИИ</TableHead>
                <TableHead className="w-24" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="font-medium">{c.name}</TableCell>
                  <TableCell className="tabular text-muted-foreground">{c.inn ?? "—"}</TableCell>
                  <TableCell>{c.industry ?? "—"}</TableCell>
                  <TableCell>
                    {c.is_kii && (
                      <Badge variant="warning" className="gap-1">
                        <ShieldAlert className="h-3 w-3" /> КИИ
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="icon" onClick={() => setEdit(c)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button variant="ghost" size="icon" onClick={() => remove(c)}>
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {createOpen && <ClientDialog onClose={() => setCreateOpen(false)} />}
      {edit && <ClientDialog client={edit} onClose={() => setEdit(null)} />}
    </div>
  );
}

function ClientDialog({ client, onClose }: { client?: Client; onClose: () => void }) {
  const create = useCreateClient();
  const update = useUpdateClient();
  const [name, setName] = useState(client?.name ?? "");
  const [inn, setInn] = useState(client?.inn ?? "");
  const [industry, setIndustry] = useState(client?.industry ?? "");
  const [isKii, setIsKii] = useState(client?.is_kii ?? false);

  const save = async () => {
    if (!name.trim()) {
      toast.error("Укажите наименование");
      return;
    }
    const payload = { name, inn: inn || null, industry: industry || null, is_kii: isKii };
    try {
      if (client) {
        await update.mutateAsync({ id: client.id, ...payload });
      } else {
        await create.mutateAsync(payload);
      }
      toast.success(client ? "Клиент обновлён" : "Клиент создан");
      onClose();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{client ? "Редактирование клиента" : "Новый клиент"}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label>Наименование</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Демо-клиент" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>ИНН</Label>
              <Input value={inn} onChange={(e) => setInn(e.target.value)} className="tabular" />
            </div>
            <div className="space-y-1.5">
              <Label>Отрасль</Label>
              <Input value={industry} onChange={(e) => setIndustry(e.target.value)} />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={isKii} onChange={(e) => setIsKii(e.target.checked)} />
            Объект КИИ (критическая информационная инфраструктура)
          </label>
        </div>
        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button onClick={save} disabled={create.isPending || update.isPending}>
            Сохранить
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function VendorsTab() {
  const { data: vendors, isLoading } = useVendors();
  const create = useCreateVendor();
  const [name, setName] = useState("");
  const [country, setCountry] = useState("");

  const add = async () => {
    if (!name.trim()) {
      toast.error("Укажите название вендора");
      return;
    }
    try {
      await create.mutateAsync({ name, country: country || undefined });
      setName("");
      setCountry("");
      toast.success("Вендор добавлен");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2 rounded-lg border bg-card p-3">
        <div className="space-y-1">
          <Label>Вендор</Label>
          <Input value={name} onChange={(e) => setName(e.target.value)} className="w-64" />
        </div>
        <div className="space-y-1">
          <Label>Страна</Label>
          <Input value={country} onChange={(e) => setCountry(e.target.value)} className="w-44" />
        </div>
        <Button onClick={add} disabled={create.isPending}>
          <Plus className="h-4 w-4" /> Добавить
        </Button>
      </div>

      {isLoading ? (
        <PageLoader />
      ) : !vendors || vendors.length === 0 ? (
        <EmptyState title="Вендоров нет" />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Вендор</TableHead>
                <TableHead>Страна</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {vendors.map((v) => (
                <TableRow key={v.id}>
                  <TableCell className="font-medium">{v.name}</TableCell>
                  <TableCell className="text-muted-foreground">{v.country ?? "—"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
      <p className="text-sm text-muted-foreground">
        Продукты и прайс-листы — в разделе «Каталог продуктов».
      </p>
    </div>
  );
}
