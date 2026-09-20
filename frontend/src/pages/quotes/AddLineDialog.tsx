import { useEffect, useState } from "react";

import { useProducts } from "@/entities/catalog/api";
import { useAddLine } from "@/entities/quote/api";
import {
  KIND_LABELS,
  LICENSING_LABELS,
  type LicensingModel,
  type QuoteLineInput,
  type QuoteLineKind,
} from "@/entities/quote/model";
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

export function AddLineDialog({
  projectId,
  quoteId,
  open,
  onOpenChange,
}: {
  projectId: string;
  quoteId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const addLine = useAddLine(projectId, quoteId);
  const { data: products } = useProducts();
  const [kind, setKind] = useState<QuoteLineKind>("license");
  const [form, setForm] = useState<QuoteLineInput>({
    kind: "license",
    name: "",
    qty: "1",
    unit_price: "0",
    currency: "RUB",
    licensing_model: "per_user",
    partner_discount_pct: "0",
    client_discount_pct: "0",
  });

  useEffect(() => {
    setForm((f) => ({ ...f, kind }));
  }, [kind]);

  const set = (patch: Partial<QuoteLineInput>) => setForm((f) => ({ ...f, ...patch }));

  const submit = async () => {
    if (!form.name.trim()) {
      toast.error("Укажите наименование строки");
      return;
    }
    try {
      await addLine.mutateAsync(form);
      toast.success("Строка добавлена");
      onOpenChange(false);
      setForm({
        kind,
        name: "",
        qty: "1",
        unit_price: "0",
        currency: "RUB",
        licensing_model: kind === "license" ? "per_user" : null,
        partner_discount_pct: "0",
        client_discount_pct: "0",
      });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const isLicense = kind === "license";

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Добавить строку в спецификацию</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1.5">
              <Label>Тип</Label>
              <Select value={kind} onValueChange={(v) => setKind(v as QuoteLineKind)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(KIND_LABELS).map(([v, l]) => (
                    <SelectItem key={v} value={v}>
                      {l}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="col-span-2 space-y-1.5">
              <Label>Наименование</Label>
              {isLicense && products ? (
                <Select
                  value=""
                  onValueChange={(pid) => {
                    const p = products.items.find((x) => x.id === pid);
                    if (p)
                      set({
                        name: p.name,
                        product_id: p.id,
                        currency: p.price_currency,
                        licensing_model: p.licensing_model,
                      });
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder={form.name || "Выбрать продукт из каталога…"} />
                  </SelectTrigger>
                  <SelectContent>
                    {products.items.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.name}
                        {p.edition ? ` (${p.edition})` : ""}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Input value={form.name} onChange={(e) => set({ name: e.target.value })} />
              )}
            </div>
          </div>

          {isLicense && (
            <Input
              value={form.name}
              onChange={(e) => set({ name: e.target.value })}
              placeholder="Наименование строки (можно отредактировать)"
            />
          )}

          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1.5">
              <Label>Количество</Label>
              <Input
                type="number"
                value={form.qty}
                onChange={(e) => set({ qty: e.target.value })}
                className="tabular"
              />
            </div>
            <div className="space-y-1.5">
              <Label>{isLicense ? "Цена прайса за ед." : "Цена продажи за ед."}</Label>
              <Input
                type="number"
                value={form.unit_price}
                onChange={(e) => set({ unit_price: e.target.value })}
                className="tabular"
              />
            </div>
            {isLicense ? (
              <div className="space-y-1.5">
                <Label>Валюта</Label>
                <Input
                  value={form.currency}
                  onChange={(e) => set({ currency: e.target.value.toUpperCase() })}
                />
              </div>
            ) : (
              <div className="space-y-1.5">
                <Label>Себестоимость за ед.</Label>
                <Input
                  type="number"
                  value={form.unit_cost ?? ""}
                  onChange={(e) => set({ unit_cost: e.target.value })}
                  className="tabular"
                />
              </div>
            )}
          </div>

          {isLicense && (
            <div className="grid grid-cols-4 gap-3">
              <div className="space-y-1.5">
                <Label>Модель</Label>
                <Select
                  value={form.licensing_model ?? "per_user"}
                  onValueChange={(v) => set({ licensing_model: v as LicensingModel })}
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
                <Label>Скидка партн., %</Label>
                <Input
                  type="number"
                  value={form.partner_discount_pct}
                  onChange={(e) => set({ partner_discount_pct: e.target.value })}
                  className="tabular"
                />
              </div>
              <div className="space-y-1.5">
                <Label>Скидка клиенту, %</Label>
                <Input
                  type="number"
                  value={form.client_discount_pct}
                  onChange={(e) => set({ client_discount_pct: e.target.value })}
                  className="tabular"
                />
              </div>
              {form.licensing_model === "subscription" ? (
                <div className="space-y-1.5">
                  <Label>Срок, мес.</Label>
                  <Input
                    type="number"
                    value={form.term_months ?? ""}
                    onChange={(e) => set({ term_months: Number(e.target.value) })}
                    className="tabular"
                  />
                </div>
              ) : (
                <div className="space-y-1.5">
                  <Label>Поддержка, %/год</Label>
                  <Input
                    type="number"
                    value={form.support_pct ?? ""}
                    onChange={(e) => set({ support_pct: e.target.value })}
                    className="tabular"
                  />
                </div>
              )}
            </div>
          )}
        </div>
        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Отмена
          </Button>
          <Button onClick={submit} disabled={addLine.isPending}>
            {addLine.isPending && <Spinner />} Добавить
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
