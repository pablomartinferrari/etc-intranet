import { createContext, useContext, type ReactNode } from "react";import { useIsAuthenticated } from "@azure/msal-react";
import { useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { fetchEntityDashboard } from "@mf/api/entity";
import { fetchJob, type JobDto } from "@mf/api/jobs";
import { getEntityDefinition, isValidEntitySlug } from "@mf/config/entities";

interface EntityContextValue {
  jobId: string;
  entitySlug: string;
  entityDisplayName: string;
  job: JobDto | null | undefined;
  dashboard: Awaited<ReturnType<typeof fetchEntityDashboard>> | undefined;
  isLoading: boolean;
  refetchDashboard: () => Promise<unknown>;
}

const EntityContext = createContext<EntityContextValue | null>(null);

export function EntityProvider({ children }: { children: ReactNode }): React.JSX.Element {
  const { jobId = "", entitySlug = "" } = useParams<{ jobId: string; entitySlug: string }>();
  const def = getEntityDefinition(entitySlug);
  const isAuthenticated = useIsAuthenticated();
  const queriesEnabled =
    isAuthenticated && Boolean(jobId) && isValidEntitySlug(entitySlug);

  const jobQuery = useQuery({
    queryKey: ["job", jobId],
    queryFn: () => fetchJob(jobId),
    enabled: queriesEnabled,
  });

  const dashboardQuery = useQuery({
    queryKey: ["dashboard", jobId, entitySlug],
    queryFn: () => fetchEntityDashboard(jobId, entitySlug),
    enabled: queriesEnabled,
  });

  const value: EntityContextValue = {
    jobId,
    entitySlug,
    entityDisplayName: def?.displayName ?? entitySlug,
    job: jobQuery.data,
    dashboard: dashboardQuery.data,
    isLoading: jobQuery.isLoading || dashboardQuery.isLoading,
    refetchDashboard: dashboardQuery.refetch,
  };

  return <EntityContext.Provider value={value}>{children}</EntityContext.Provider>;
}

export function useEntity(): EntityContextValue {
  const ctx = useContext(EntityContext);
  if (!ctx) throw new Error("useEntity must be used within EntityProvider");
  return ctx;
}
