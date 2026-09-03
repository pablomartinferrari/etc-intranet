import { NavLink, Outlet, useNavigate, useParams } from "react-router-dom";
import {
  DownloadIcon,
  FileTextIcon,
  LayoutDashboardIcon,
  ListTreeIcon,
  SparklesIcon,
  TableIcon,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";
import { RequireAuth } from "@mf/auth/RequireAuth";
import { EntityProvider, useEntity } from "@mf/context/EntityContext";
import { isValidEntitySlug } from "@mf/config/entities";

function ShellInner(): React.JSX.Element {
  const nav = useNavigate();
  const { jobId, entitySlug } = useParams<{ jobId: string; entitySlug: string }>();
  const { entityDisplayName, job, dashboard, isLoading } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;

  if (!isValidEntitySlug(entitySlug ?? "")) {
    return <p>Unknown entity.</p>;
  }

  const navItems = [
    { to: `${base}/overview`, label: "Overview", icon: <LayoutDashboardIcon className="size-4" /> },
    { to: `${base}/uploads`, label: "Source files", icon: <DownloadIcon className="size-4" /> },
    { to: `${base}/grid`, label: "Data grid", icon: <TableIcon className="size-4" /> },
    { to: `${base}/normalize`, label: "AI normalization", icon: <SparklesIcon className="size-4" /> },
    { to: `${base}/grid/groups`, label: "Grouped readings", icon: <ListTreeIcon className="size-4" /> },
    { to: `${base}/reports/configure`, label: "Reports", icon: <FileTextIcon className="size-4" /> },
  ];

  return (
    <div className="flex min-h-[calc(100vh-120px)] flex-col">
      <header className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-md border bg-card p-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-xl font-semibold">
            Job {jobId} · {entityDisplayName}
          </h2>
          <p className="text-sm">
            {[job?.clientName, job?.facilityName].filter(Boolean).join(" · ") || " "}
            {dashboard && (
              <>
                {" "}
                · {dashboard.unitsRowCount + dashboard.commonAreasRowCount} rows
              </>
            )}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" onClick={() => nav(`${base}/normalize`)}>
            <SparklesIcon />
            Normalize
          </Button>
          <Button onClick={() => nav(`${base}/reports/configure`)}>
            <FileTextIcon />
            Generate report
          </Button>
        </div>
      </header>
      {isLoading ? (
        <Spinner label="Loading…" />
      ) : (
        <div className="flex flex-1 gap-6">
          <nav className="flex w-[200px] shrink-0 flex-col gap-1">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === base}
                className={({ isActive }) =>
                  cn(
                    "flex items-center gap-2 rounded-md px-3 py-2 text-sm no-underline hover:bg-muted",
                    isActive && "bg-muted font-semibold",
                  )
                }
              >
                {item.icon}
                {item.label}
              </NavLink>
            ))}
          </nav>
          <main className="min-w-0 flex-1">
            <Outlet />
          </main>
        </div>
      )}
    </div>
  );
}

export function EntityShellLayout(): React.JSX.Element {
  return (
    <RequireAuth>
      <EntityProvider>
        <ShellInner />
      </EntityProvider>
    </RequireAuth>
  );
}
