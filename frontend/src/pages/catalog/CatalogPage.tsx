import { Plus, Search, Tag } from "lucide-react";
import { useState } from "react";

import {
  useCreateProduct,
  useCreateVendor,
  useProducts,
  useVendors,
} from "@/entities/catalog/api";
import { LICENSING_LABELS, type LicensingModel } from "@/entities/quote/model";
import { hasAnyRole } from "@/entities/user/model";
import { ApiError } from "@/shared/api/types";
import { useMe } from "@/shared/auth/useAuth";
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
import { ProductPricesDialog } from "./ProductPricesDialog";

export function CatalogPage() {
  const { data: me } = useMe();
  const [q, setQ] = useState("");
  const { data: products } = useProducts(q || undefined);
  const { data: vendors } = useVendors();
  const createProduct = useCreateProduct();
  const createVendor = useCreateVendor();
  const canManage = hasAnyRole(me, ["admin", "pm", "presale"]);

  const [createOpen, setCreateOpen] = useState(false);
  const [pricesFor, setPricesFor] = useState<string | null>(null);
  const [form, setForm] = useState({
    vendor_id: "",
    name: "",
    edition: "",
    licensing_model: "per_user" as LicensingModel,
    price_currency: "RUB",
    is_russian_registry: false,
  });
  const [newVendor, setNewVendor] = useState("");

  const addVendor = async () => {
    if (!newVendor.trim()) return;
    try {
      const v = await createVendor.mutateAsync({ name: newVendor });
      setForm((f) => ({ ...f, vendor_id: v.id }));
      setNewVendor("");
      toast.success("Вендор добавлен");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const submit = async () => {
    if (!form.vendor_id || !form.name.trim()) {
      toast.error("Укажите вендора и наименование");
      return;
    }
    try {
      await createProduct.mutateAsync(form);
      toast.success("Продукт добавлен");
      setCreateOpen(false);
      setForm((f) => ({ ...f, name: "", edition: "" }));
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const vendorName = (id: string) => vendors?.find((v) => v.id === id)?.name ?? "—";

  return (
    <div>
      <PageHeader
        title="Каталог продуктов"
        description="Вендоры, продукты и прайс-листы для калькулятора лицензий"
        actions={
          canManage && (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" /> Продукт
            </Button>
          )
        }
      />

      <div className="relative mb-4 max-w-md">
        <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Поиск по названию (pg_trgm)…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          className="pl-8"
        />
      </div>

      {!products || products.items.length === 0 ? (
        <EmptyState title="Продуктов не найдено" icon={<Tag className="h-8 w-8" />} />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Продукт</TableHead>
                <TableHead>Вендор</TableHead>
                <TableHead>Модель</TableHead>
                <TableHead>Валюта</TableHead>
                <TableHead>Реестр РФ</TableHead>
                <TableHead className="w-28" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {products.items.map((p) => (
                <TableRow key={p.id}>
                  <TableCell className="font-medium">
                    {p.name}
                    {p.edition && (
                      <span className="ml-1 text-xs text-muted-foreground">{p.edition}</span>
                    )}
                  </TableCell>
                  <TableCell>{vendorName(p.vendor_id)}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {LICENSING_LABELS[p.licensing_model]}
                  </TableCell>
                  <TableCell>{p.price_currency}</TableCell>
                  <TableCell>
                    {p.is_russian_registry && <Badge variant="success">в реестре</Badge>}
                  </TableCell>
                  <TableCell>
                    <Button variant="outline" size="sm" onClick={() => setPricesFor(p.id)}>
                      Прайс
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Создание продукта */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Новый продукт</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label>Вендор</Label>
              <div className="flex gap-2">
                <Select
                  value={form.vendor_id}
                  onValueChange={(v) => setForm((f) => ({ ...f, vendor_id: v }))}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Выберите…" />
                  </SelectTrigger>
                  <SelectContent>
                    {vendors?.map((v) => (
                      <SelectItem key={v.id} value={v.id}>
                        {v.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="flex gap-2">
                <Input
                  placeholder="…или добавить вендора"
                  value={newVendor}
                  onChange={(e) => setNewVendor(e.target.value)}
                />
                <Button variant="outline" onClick={addVendor}>
                  +
                </Button>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Наименование</Label>
                <Input
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                />
              </div>
              <div className="space-y-1.5">
                <Label>Редакция</Label>
                <Input
                  value={form.edition}
                  onChange={(e) => setForm((f) => ({ ...f, edition: e.target.value }))}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Модель лицензирования</Label>
                <Select
                  value={form.licensing_model}
                  onValueChange={(v) =>
                    setForm((f) => ({ ...f, licensing_model: v as LicensingModel }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Object.entries(LICENSING_LABELS).map(([v, l]) => (
                      <SelectItem key={v} value={v}>
                        {l}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Валюта прайса</Label>
                <Input
                  value={form.price_currency}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, price_currency: e.target.value.toUpperCase() }))
                  }
                />
              </div>
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={form.is_russian_registry}
                onChange={(e) =>
                  setForm((f) => ({ ...f, is_russian_registry: e.target.checked }))
                }
              />
              В реестре российского ПО
            </label>
          </div>
          <DialogFooter className="gap-2">
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Отмена
            </Button>
            <Button onClick={submit} disabled={createProduct.isPending}>
              Создать
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {pricesFor && (
        <ProductPricesDialog
          productId={pricesFor}
          canManage={canManage}
          onClose={() => setPricesFor(null)}
        />
      )}
    </div>
  );
}
