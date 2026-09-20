import { AlertTriangle, CheckCircle2, ChevronRight } from "lucide-react";
import { Link } from "react-router-dom";

import { usePortalProjects } from "@/entities/customer/api";
import { STAGE_LABELS } from "@/entities/project/model";
import { Badge } from "@/shared/ui/badge";
import { Card, CardContent } from "@/shared/ui/card";
import { EmptyState, PageHeader } from "@/shared/ui/page";
import { PageLoader } from "@/shared/ui/spinner";

export function PortalProjectsPage() {
  const { data: projects, isLoading } = usePortalProjects();

  return (
    <div>
      <PageHeader
        title="Мои проекты"
        description="Статус проектов и задачи, требующие вашего участия"
      />
      {isLoading ? (
        <PageLoader />
      ) : !projects || projects.length === 0 ? (
        <EmptyState title="Проектов нет" />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {projects.map((p) => (
            <Link key={p.project_id} to={`/portal/${p.project_id}`}>
              <Card className="transition-shadow hover:shadow-md">
                <CardContent className="p-5">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="font-mono text-xs text-muted-foreground">{p.code}</p>
                      <p className="mt-0.5 font-medium">{p.name}</p>
                      <Badge variant="secondary" className="mt-2">
                        Стадия: {STAGE_LABELS[p.stage]}
                      </Badge>
                    </div>
                    <ChevronRight className="h-5 w-5 text-muted-foreground" />
                  </div>
                  <div className="mt-4 border-t pt-3">
                    {p.blocked_on_customer ? (
                      <div className="flex items-center gap-2 text-sm font-medium text-warning">
                        <AlertTriangle className="h-4 w-4" />
                        Требуется ваше участие: {p.open_actions}
                      </div>
                    ) : (
                      <div className="flex items-center gap-2 text-sm text-success">
                        <CheckCircle2 className="h-4 w-4" />
                        От вас ничего не требуется
                      </div>
                    )}
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
