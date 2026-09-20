import { createBrowserRouter, Navigate } from "react-router-dom";

import { AppLayout } from "@/app/layout/AppLayout";
import { RequireAuth, RequireRole } from "@/app/guards";
import { DashboardPage } from "@/pages/DashboardPage";
import { LoginPage } from "@/pages/LoginPage";
import { ProjectsPage } from "@/pages/projects/ProjectsPage";
import { ProjectDetailPage } from "@/pages/projects/ProjectDetailPage";
import { TimesheetsPage } from "@/pages/timesheets/TimesheetsPage";
import { ApprovalsPage } from "@/pages/timesheets/ApprovalsPage";
import { QuotesPage } from "@/pages/quotes/QuotesPage";
import { CatalogPage } from "@/pages/catalog/CatalogPage";
import { FinancePage } from "@/pages/finance/FinancePage";
import { ResourcesPage } from "@/pages/resources/ResourcesPage";
import { AdminUsersPage } from "@/pages/admin/AdminUsersPage";
import { AuditLogPage } from "@/pages/admin/AuditLogPage";
import { DirectoriesPage } from "@/pages/admin/DirectoriesPage";
import { PortalProjectsPage } from "@/pages/portal/PortalProjectsPage";
import { PortalProjectDetailPage } from "@/pages/portal/PortalProjectDetailPage";
import { RisksPortfolioPage } from "@/pages/risks/RisksPortfolioPage";
import { ReportsPage } from "@/pages/reports/ReportsPage";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  {
    path: "/",
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      {
        path: "portal",
        element: (
          <RequireRole roles={["client"]}>
            <PortalProjectsPage />
          </RequireRole>
        ),
      },
      {
        path: "portal/:projectId",
        element: (
          <RequireRole roles={["client"]}>
            <PortalProjectDetailPage />
          </RequireRole>
        ),
      },
      { path: "projects", element: <ProjectsPage /> },
      { path: "projects/:projectId", element: <ProjectDetailPage /> },
      {
        path: "risks",
        element: (
          <RequireRole roles={["pm", "director", "admin", "finance"]}>
            <RisksPortfolioPage />
          </RequireRole>
        ),
      },
      { path: "timesheets", element: <TimesheetsPage /> },
      {
        path: "approvals",
        element: (
          <RequireRole roles={["pm", "admin"]}>
            <ApprovalsPage />
          </RequireRole>
        ),
      },
      {
        path: "quotes",
        element: (
          <RequireRole roles={["presale", "pm", "admin", "director"]}>
            <QuotesPage />
          </RequireRole>
        ),
      },
      {
        path: "catalog",
        element: (
          <RequireRole roles={["presale", "pm", "admin"]}>
            <CatalogPage />
          </RequireRole>
        ),
      },
      {
        path: "finance",
        element: (
          <RequireRole roles={["finance", "director", "pm", "admin"]}>
            <FinancePage />
          </RequireRole>
        ),
      },
      {
        path: "resources",
        element: (
          <RequireRole roles={["pm", "director", "admin", "finance"]}>
            <ResourcesPage />
          </RequireRole>
        ),
      },
      {
        path: "reports",
        element: (
          <RequireRole roles={["pm", "director", "admin", "finance"]}>
            <ReportsPage />
          </RequireRole>
        ),
      },
      {
        path: "admin/users",
        element: (
          <RequireRole roles={["admin", "finance"]}>
            <AdminUsersPage />
          </RequireRole>
        ),
      },
      {
        path: "admin/audit",
        element: (
          <RequireRole roles={["admin", "director", "finance"]}>
            <AuditLogPage />
          </RequireRole>
        ),
      },
      {
        path: "admin/directories",
        element: (
          <RequireRole roles={["admin"]}>
            <DirectoriesPage />
          </RequireRole>
        ),
      },
    ],
  },
  { path: "*", element: <Navigate to="/" replace /> },
]);
