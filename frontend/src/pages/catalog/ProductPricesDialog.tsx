import { useState } from "react";

import { useAddPrice, useProduct } from "@/entities/catalog/api";
import { ApiError } from "@/shared/api/types";
import { formatCurrency, formatDate } from "@/shared/lib/format";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { Spinner } from "@/shared/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";
import { toast } from "@/shared/ui/toast";

export function ProductPricesDialog({
  productId,
  canManage,
  onClose,
}: {
  productId: string;
  canManage: boolean;
  onClose: () => void;
}) {
  const { data: product, isLoading } = useProduct(productId);
  const addPrice = useAddPrice(productId);
  const [metric, setMetric] = useState("");
  const [price, setPrice] = useState("");
  const [validFrom, setValidFrom] = useState(new Date().toISOString().slice(0, 10));

  const add = async () => {
    if (!metric.trim() || !price) {
      toast.error("Укажите метрику и цену");
      return;
    }
    try {
      await addPrice.mutateAsync({
        metric,
        price,
        currency: product?.price_currency ?? "RUB",
        valid_from: validFrom,
      });
      setMetric("");
      setPrice("");
      toast.success("Цена добавлена");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Прайс-лист · {product?.name ?? "…"}</DialogTitle>
        </DialogHeader>
        {isLoading || !product ? (
          <Spinner />
        ) : (
          <div className="space-y-4">
            <div className="rounded-lg border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Метрика</TableHead>
                    <TableHead className="text-right">Цена</TableHead>
                    <TableHead>Действует с</TableHead>
                    <TableHead>по</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {product.price_items.map((p) => (
                    <TableRow key={p.id}>
                      <TableCell>{p.metric}</TableCell>
                      <TableCell className="text-right tabular">
                        {formatCurrency(p.price, p.currency)}
                      </TableCell>
                      <TableCell>{formatDate(p.valid_from)}</TableCell>
                      <TableCell>{p.valid_to ? formatDate(p.valid_to) : "—"}</TableCell>
                    </TableRow>
                  ))}
                  {product.price_items.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} className="py-4 text-center text-muted-foreground">
                        Цен пока нет
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </div>

            {canManage && (
              <div className="flex flex-wrap items-end gap-2 rounded-lg border bg-muted/30 p-3">
                <div className="space-y-1">
                  <Label>Метрика</Label>
                  <Input
                    value={metric}
                    onChange={(e) => setMetric(e.target.value)}
                    placeholder="пользователь / ядро"
                    className="w-40"
                  />
                </div>
                <div className="space-y-1">
                  <Label>Цена ({product.price_currency})</Label>
                  <Input
                    type="number"
                    value={price}
                    onChange={(e) => setPrice(e.target.value)}
                    className="w-32 tabular"
                  />
                </div>
                <div className="space-y-1">
                  <Label>Действует с</Label>
                  <Input
                    type="date"
                    value={validFrom}
                    onChange={(e) => setValidFrom(e.target.value)}
                  />
                </div>
                <Button onClick={add} disabled={addPrice.isPending}>
                  Добавить
                </Button>
              </div>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
