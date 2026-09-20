import { Copy, Download, Plus, Trash2 } from "lucide-react";
import { useState } from "react";

import {
  downloadQuoteXlsx,
  useCloneQuote,
  useDeleteLine,
  useQuote,
  useSetQuoteStatus,
} from "@/entities/quote/api";
import {
  KIND_LABELS,
  QUOTE_STATUS_LABELS,
  QUOTE_STATUS_VARIANT,
  type QuoteStatus,
} from "@/entities/quote/model";
import { ApiError } from "@/shared/api/types";
import { formatCurrency, formatMoney, formatNumber, formatPercent } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { StatCard } from "@/shared/ui/page";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
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
import { AddLineDialog } from "./AddLineDialog";

export function QuoteBuilder({
  projectId,
  quoteId,
  projectCode,
  canEdit,
}: {
  projectId: string;
  quoteId: string;
  projectCode: string;
  canEdit: boolean;
}) {
  const { data: quote, isLoading } = useQuote(projectId, quoteId);
  const deleteLine = useDeleteLine(projectId, quoteId);
  const setStatus = useSetQuoteStatus(projectId, quoteId);
  const clone = useCloneQuote(projectId, quoteId);
  const [addOpen, setAddOpen] = useState(false);

  if (isLoading || !quote) return <PageLoader />;

  const editable = canEdit && quote.status === "draft";
  const t = quote.totals;
  const marginPct = Number(t.margin_pct ?? 0);

  const onExport = async () => {
    try {
      await downloadQuoteXlsx(projectId, quoteId, `TKP_${projectCode}_v${quote.version}.xlsx`);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Не удалось скачать XLSX");
    }
  };

  const onDeleteLine = (lineId: string, name: string) => {
    if (!window.confirm(`Удалить строку «${name}» из расчёта?`)) return;
    deleteLine.mutate(lineId, {
      onSuccess: () => toast.success("Строка удалена"),
      onError: (e) => toast.error(e instanceof ApiError ? e.message : "Не удалось удалить"),
    });
  };

  const onClone = async () => {
    try {
      await clone.mutateAsync();
      toast.success("Создана новая версия");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <h3 className="text-lg font-semibold">
            {quote.title} · версия {quote.version}
          </h3>
          <Badge variant={QUOTE_STATUS_VARIANT[quote.status]}>
            {QUOTE_STATUS_LABELS[quote.status]}
          </Badge>
        </div>
        <div className="flex items-center gap-2">
          {canEdit && (
            <Select
              value={quote.status}
              onValueChange={(v) => setStatus.mutate(v as QuoteStatus)}
            >
              <SelectTrigger className="h-8 w-36">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {Object.entries(QUOTE_STATUS_LABELS).map(([v, l]) => (
                  <SelectItem key={v} value={v}>
                    {l}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
          <Button variant="outline" size="sm" onClick={onExport}>
            <Download className="h-4 w-4" /> XLSX
          </Button>
          {canEdit && (
            <Button variant="outline" size="sm" onClick={onClone} disabled={clone.isPending}>
              <Copy className="h-4 w-4" /> Новая версия
            </Button>
          )}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Лицензии" value={formatMoney(t.licenses_sell)} />
        <StatCard label="Работы + субподряд" value={formatMoney(
          String(Number(t.work_sell ?? 0) + Number(t.subcontract_sell ?? 0)),
        )} />
        <StatCard label="Итого продажа" value={formatMoney(t.total_sell)} />
        <StatCard
          label="Маржа"
          value={formatMoney(t.margin)}
          hint={`Маржинальность: ${formatPercent(t.margin_pct)}`}
          tone={marginPct >= 20 ? "success" : marginPct >= 10 ? "warning" : "destructive"}
        />
      </div>

      <div className="rounded-lg border bg-card">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Тип</TableHead>
              <TableHead>Наименование</TableHead>
              <TableHead className="text-right">Кол-во</TableHead>
              <TableHead className="text-right">Цена ед.</TableHead>
              <TableHead className="text-right">Скидки п/к</TableHead>
              <TableHead className="text-right">Себестоим.</TableHead>
              <TableHead className="text-right">Продажа</TableHead>
              <TableHead className="text-right">Маржа</TableHead>
              {editable && <TableHead className="w-10" />}
            </TableRow>
          </TableHeader>
          <TableBody>
            {quote.lines.map((line) => (
              <TableRow key={line.id}>
                <TableCell>
                  <Badge variant="secondary">{KIND_LABELS[line.kind]}</Badge>
                </TableCell>
                <TableCell className="font-medium">
                  {line.name}
                  {line.currency !== "RUB" && (
                    <span className="ml-1 text-xs text-muted-foreground">({line.currency})</span>
                  )}
                </TableCell>
                <TableCell className="text-right tabular">{formatNumber(line.qty)}</TableCell>
                <TableCell className="text-right tabular">
                  {formatCurrency(line.unit_price, line.currency)}
                </TableCell>
                <TableCell className="text-right tabular text-xs text-muted-foreground">
                  {line.kind === "license"
                    ? `${line.partner_discount_pct}/${line.client_discount_pct}%`
                    : "—"}
                </TableCell>
                <TableCell className="text-right tabular">{formatMoney(line.cost_amount)}</TableCell>
                <TableCell className="text-right tabular font-medium">
                  {formatMoney(line.sell_amount)}
                </TableCell>
                <TableCell className="text-right tabular text-success">
                  {formatMoney(line.margin)}
                </TableCell>
                {editable && (
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => onDeleteLine(line.id, line.name)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </TableCell>
                )}
              </TableRow>
            ))}
            {quote.lines.length === 0 && (
              <TableRow>
                <TableCell colSpan={editable ? 9 : 8} className="py-6 text-center text-muted-foreground">
                  Строк пока нет
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      {editable && (
        <Button onClick={() => setAddOpen(true)}>
          <Plus className="h-4 w-4" /> Добавить строку
        </Button>
      )}

      <AddLineDialog
        projectId={projectId}
        quoteId={quoteId}
        open={addOpen}
        onOpenChange={setAddOpen}
      />
    </div>
  );
}
