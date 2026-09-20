import { Plus } from "lucide-react";
import { useEffect, useState } from "react";

import { useProjects } from "@/entities/project/api";
import { useCreateQuote, useQuotes } from "@/entities/quote/api";
import { hasAnyRole } from "@/entities/user/model";
import { ApiError } from "@/shared/api/types";
import { useMe } from "@/shared/auth/useAuth";
import { formatDate } from "@/shared/lib/format";
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
import { toast } from "@/shared/ui/toast";
import { QuoteBuilder } from "./QuoteBuilder";

export function QuotesPage() {
  const { data: me } = useMe();
  const { data: projects } = useProjects({ limit: 200 });
  const [projectId, setProjectId] = useState<string>("");
  const { data: quotes } = useQuotes(projectId || undefined);
  const [quoteId, setQuoteId] = useState<string>("");
  const create = useCreateQuote(projectId);
  const [createOpen, setCreateOpen] = useState(false);
  const [buffer, setBuffer] = useState("2");
  const [usd, setUsd] = useState("");
  const [eur, setEur] = useState("");
  const [cny, setCny] = useState("");

  const canEdit = hasAnyRole(me, ["presale", "pm", "admin"]);
  const project = projects?.items.find((p) => p.id === projectId);

  // Автовыбор первого проекта при загрузке списка.
  useEffect(() => {
    if (!projectId && projects && projects.items.length > 0) {
      setProjectId(projects.items[0].id);
    }
  }, [projects, projectId]);

  // Автовыбор первого расчёта при смене проекта.
  useEffect(() => {
    if (quotes && quotes.length > 0) setQuoteId((q) => q || quotes[0].id);
    else setQuoteId("");
  }, [quotes]);

  const onCreate = async () => {
    const rates: Record<string, string> = {};
    if (usd) rates.USD = usd;
    if (eur) rates.EUR = eur;
    if (cny) rates.CNY = cny;
    try {
      const q = await create.mutateAsync({
        currency_rates: rates,
        currency_buffer_pct: buffer || "0",
      });
      toast.success(`Расчёт v${q.version} создан`);
      setQuoteId(q.id);
      setCreateOpen(false);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <div>
      <PageHeader
        title="Калькулятор лицензий / ТКП"
        description="Подбор продуктов, расчёт маржи и формирование коммерческого предложения"
        actions={
          canEdit &&
          projectId && (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" /> Новый расчёт
            </Button>
          )
        }
      />

      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="w-80">
          <Select value={projectId} onValueChange={(v) => { setProjectId(v); setQuoteId(""); }}>
            <SelectTrigger>
              <SelectValue placeholder="Выберите проект…" />
            </SelectTrigger>
            <SelectContent>
              {projects?.items.map((p) => (
                <SelectItem key={p.id} value={p.id}>
                  {p.code} · {p.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {quotes && quotes.length > 0 && (
          <div className="flex gap-1">
            {quotes.map((q) => (
              <Button
                key={q.id}
                size="sm"
                variant={quoteId === q.id ? "default" : "outline"}
                onClick={() => setQuoteId(q.id)}
                title={formatDate(q.created_at)}
              >
                v{q.version}
              </Button>
            ))}
          </div>
        )}
      </div>

      {!projectId ? (
        <EmptyState title="Выберите проект" description="Расчёты ведутся в рамках проекта." />
      ) : quoteId && project ? (
        <QuoteBuilder
          projectId={projectId}
          quoteId={quoteId}
          projectCode={project.code}
          canEdit={canEdit}
        />
      ) : (
        <EmptyState
          title="Расчётов пока нет"
          description="Создайте первый расчёт для этого проекта."
          action={
            canEdit && (
              <Button onClick={() => setCreateOpen(true)}>
                <Plus className="h-4 w-4" /> Новый расчёт
              </Button>
            )
          }
        />
      )}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Новый расчёт</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            Курсы валют фиксируются в версии расчёта вручную (рублей за 1 единицу).
            Оставьте пустым, если все позиции в рублях.
          </p>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Буфер курса, %</Label>
              <Input type="number" value={buffer} onChange={(e) => setBuffer(e.target.value)} className="tabular" />
            </div>
            <div className="space-y-1.5">
              <Label>USD → ₽</Label>
              <Input type="number" value={usd} onChange={(e) => setUsd(e.target.value)} className="tabular" />
            </div>
            <div className="space-y-1.5">
              <Label>EUR → ₽</Label>
              <Input type="number" value={eur} onChange={(e) => setEur(e.target.value)} className="tabular" />
            </div>
            <div className="space-y-1.5">
              <Label>CNY → ₽</Label>
              <Input type="number" value={cny} onChange={(e) => setCny(e.target.value)} className="tabular" />
            </div>
          </div>
          <DialogFooter className="gap-2">
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Отмена
            </Button>
            <Button onClick={onCreate} disabled={create.isPending}>
              Создать
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
