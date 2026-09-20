import { AlertTriangle } from "lucide-react";
import { Link, Navigate } from "react-router-dom";

import { usePortfolio, RISK_LABELS } from "@/entities/dashboard/api";
import { STAGE_LABELS } from "@/entities/project/model";
import { canSeeFinancials, hasAnyRole } from "@/entities/user/model";
import { useUserMap } from "@/entities/user/api";
import { useMe } from "@/shared/auth/useAuth";
import { formatHours, formatMoney, formatPercent } from "@/shared/lib/format";
import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/shared/ui/card";
import { PageHeader, StatCard } from "@/shared/ui/page";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";
import { NAV } from "@/app/layout/nav";

export function DashboardPage() {
  const { data: me } = useMe();
  // Заказчик не видит внутренний дашборд — направляем в его портал.
  if (me && me.roles.includes("client")) {
    return <Navigate to="/portal" replace />;
  }
  const showPortfolio = canSeeFinancials(me);
  const { data: portfolio } = usePortfolio(showPortfolio);
  const users = useUserMap();

  const tiles = NAV.flatMap((g) => g.items).filter(
    (i) => i.to !== "/" && (!i.roles || hasAnyRole(me, i.roles)),
  );

  return (
    <div>
      <PageHeader
        title={`Здравствуйте, ${me?.full_name ?? ""}`}
        description="Личный кабинет управления проектами внедрения."
      />

      {showPortfolio && portfolio && (
        <>
          <div className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-5">
            <StatCard label="Активных проектов" value={portfolio.projects_count} />
            <StatCard label="Выручка портфеля" value={formatMoney(portfolio.total_revenue)} />
            <StatCard label="Маржа портфеля" value={formatMoney(portfolio.total_margin)} />
            <StatCard
              label="Средняя маржа"
              value={formatPercent(portfolio.avg_margin_pct)}
              tone={Number(portfolio.avg_margin_pct) >= 20 ? "success" : "warning"}
            />
            <StatCard
              label="Проектов в зоне риска"
              value={portfolio.at_risk}
              tone={portfolio.at_risk > 0 ? "destructive" : "success"}
            />
          </div>

          <Card className="mb-8">
            <CardHeader>
              <CardTitle>Портфель проектов</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Код</TableHead>
                    <TableHead>Проект</TableHead>
                    <TableHead>Стадия</TableHead>
                    <TableHead>РП</TableHead>
                    <TableHead className="text-right">Выручка</TableHead>
                    <TableHead className="text-right">Маржа</TableHead>
                    <TableHead className="text-right">Часы (факт/план)</TableHead>
                    <TableHead>Риски</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {portfolio.rows.map((r) => (
                    <TableRow key={r.project_id}>
                      <TableCell className="font-mono text-xs">
                        <Link to={`/projects/${r.project_id}`} className="text-primary hover:underline">
                          {r.code}
                        </Link>
                      </TableCell>
                      <TableCell className="max-w-[260px] truncate font-medium">{r.name}</TableCell>
                      <TableCell>
                        <Badge variant="secondary">{STAGE_LABELS[r.stage]}</Badge>
                      </TableCell>
                      <TableCell className="text-sm">
                        {users.get(r.manager_id)?.full_name ?? "—"}
                      </TableCell>
                      <TableCell className="text-right tabular">{formatMoney(r.revenue)}</TableCell>
                      <TableCell
                        className={cn(
                          "text-right tabular font-medium",
                          Number(r.margin_pct) < 15 ? "text-destructive" : "text-success",
                        )}
                      >
                        {formatPercent(r.margin_pct)}
                      </TableCell>
                      <TableCell className="text-right tabular text-sm">
                        {formatHours(r.actual_hours)} / {formatHours(r.planned_hours)}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          {r.risks.map((risk) => (
                            <Badge key={risk} variant="destructive" className="gap-1">
                              <AlertTriangle className="h-3 w-3" />
                              {RISK_LABELS[risk] ?? risk}
                            </Badge>
                          ))}
                          {r.risks.length === 0 && (
                            <span className="text-xs text-muted-foreground">—</span>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                  {portfolio.rows.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} className="py-6 text-center text-muted-foreground">
                        Нет активных проектов
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {tiles.map((t) => (
          <Link key={t.to} to={t.to}>
            <Card className="transition-shadow hover:shadow-md">
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <t.icon className="h-5 w-5 text-primary" />
                  {t.label}
                </CardTitle>
              </CardHeader>
              <CardContent className="text-sm text-muted-foreground">
                Перейти в раздел «{t.label}»
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
