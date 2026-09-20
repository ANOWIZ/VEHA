import { ShieldAlert } from "lucide-react";
import { Link } from "react-router-dom";

import { STAGE_LABELS } from "@/entities/project/model";
import {
  RISK_CATEGORY_LABELS,
  useRiskPortfolio,
} from "@/entities/risk/api";
import { RiskMatrix } from "@/entities/risk/RiskMatrix";
import { useUserMap } from "@/entities/user/api";
import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/shared/ui/card";
import { EmptyState, PageHeader, StatCard } from "@/shared/ui/page";
import { PageLoader } from "@/shared/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";

export function RisksPortfolioPage() {
  const { data, isLoading } = useRiskPortfolio();
  const users = useUserMap();

  if (isLoading) return <PageLoader />;

  const maxCat = Math.max(1, ...(data?.by_category.map((c) => c.count) ?? [1]));

  return (
    <div>
      <PageHeader
        title="Портфель рисков"
        description="Активные риски по доступным проектам: матрица вероятность×влияние и сводка."
      />

      <div className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="Проектов с рисками" value={data?.projects_count ?? 0} />
        <StatCard label="Активных рисков" value={data?.total_active ?? 0} />
        <StatCard
          label="Высокой критичности"
          value={data?.total_high ?? 0}
          tone={(data?.total_high ?? 0) > 0 ? "destructive" : "success"}
        />
        <StatCard
          label="Категорий затронуто"
          value={data?.by_category.length ?? 0}
        />
      </div>

      <div className="mb-8 grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Матрица вероятность × влияние</CardTitle>
          </CardHeader>
          <CardContent>
            {data && <RiskMatrix cells={data.matrix} />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Активные риски по категориям</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {(data?.by_category.length ?? 0) === 0 && (
              <p className="text-sm text-muted-foreground">Активных рисков нет.</p>
            )}
            {data?.by_category.map((c) => (
              <div key={c.category} className="flex items-center gap-3">
                <span className="w-32 shrink-0 text-sm">
                  {RISK_CATEGORY_LABELS[c.category]}
                </span>
                <div className="h-3 flex-1 overflow-hidden rounded-full bg-muted">
                  <div
                    className="h-full rounded-full bg-primary"
                    style={{ width: `${(c.count / maxCat) * 100}%` }}
                  />
                </div>
                <span className="w-6 text-right text-sm tabular font-medium">{c.count}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Проекты с активными рисками</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {(data?.rows.length ?? 0) === 0 ? (
            <EmptyState
              title="Активных рисков нет"
              description="По доступным проектам нет открытых рисков."
              icon={<ShieldAlert className="h-8 w-8" />}
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Код</TableHead>
                  <TableHead>Проект</TableHead>
                  <TableHead>Стадия</TableHead>
                  <TableHead>РП</TableHead>
                  <TableHead className="text-right">Активных</TableHead>
                  <TableHead className="text-right">Высоких</TableHead>
                  <TableHead className="text-right">Макс. балл</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.rows.map((r) => (
                  <TableRow key={r.project_id}>
                    <TableCell className="font-mono text-xs">
                      <Link
                        to={`/projects/${r.project_id}`}
                        className="text-primary hover:underline"
                      >
                        {r.code}
                      </Link>
                    </TableCell>
                    <TableCell className="max-w-[280px] truncate font-medium">
                      {r.name}
                    </TableCell>
                    <TableCell>
                      <Badge variant="secondary">{STAGE_LABELS[r.stage]}</Badge>
                    </TableCell>
                    <TableCell className="text-sm">
                      {users.get(r.manager_id)?.full_name ?? "—"}
                    </TableCell>
                    <TableCell className="text-right tabular">{r.active_count}</TableCell>
                    <TableCell
                      className={cn(
                        "text-right tabular font-medium",
                        r.high_count > 0 ? "text-destructive" : "text-muted-foreground",
                      )}
                    >
                      {r.high_count}
                    </TableCell>
                    <TableCell className="text-right tabular">{r.top_score}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
