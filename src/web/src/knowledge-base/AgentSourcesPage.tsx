import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { DatabaseIcon, UnplugIcon } from "lucide-react";
import { useMemo } from "react";
import { Link as RouterLink } from "react-router-dom";

import { BrandBar, SignOutButton } from "@/components/brand-bar";
import { PageBreadcrumb } from "@/components/page-breadcrumb";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { RequireAuth } from "../multifamily-lbp/auth/RequireAuth";
import { AddSharePointFolderButton } from "./AddSharePointFolderSheet";
import {
  disconnectAgentSource,
  listAgentSources,
  type AgentSource,
} from "./api/sources";

const ACTIVE_JOB = new Set(["queued", "probing", "running"]);

function formatJobStatus(status: string): string {
  switch (status) {
    case "queued":
      return "Queued";
    case "probing":
      return "Probing";
    case "running":
      return "Running";
    case "done":
      return "Done";
    case "failed":
      return "Failed";
    case "awaiting_approval":
      return "Needs approval";
    default:
      return status;
  }
}

function jobBadgeVariant(status: string): "default" | "secondary" | "destructive" | "outline" {
  if (status === "failed") return "destructive";
  if (status === "done") return "secondary";
  if (status === "awaiting_approval") return "outline";
  return "default";
}

function AgentSourcesPage() {
  const queryClient = useQueryClient();

  const sourcesQuery = useQuery({
    queryKey: ["kb-sources"],
    queryFn: listAgentSources,
    refetchInterval: (query) => {
      const rows = query.state.data ?? [];
      return rows.some((s) => s.latestJob && ACTIVE_JOB.has(s.latestJob.status)) ? 3000 : false;
    },
  });

  const disconnectMutation = useMutation({
    mutationFn: disconnectAgentSource,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["kb-sources"] });
    },
  });

  const sources = sourcesQuery.data ?? [];
  const connected = useMemo(
    () => sources.filter((s) => s.status !== "disconnected"),
    [sources],
  );

  return (
    <div className="flex min-h-svh flex-col bg-muted/40">
      <BrandBar actions={<SignOutButton outlineOnBlack />} />
      <div className="flex flex-col gap-3 border-b bg-background px-6 py-4">
        <PageBreadcrumb
          items={[
            { label: "Home", to: "/" },
            { label: "Chat", to: "/knowledge" },
            { label: "Manage sources" },
          ]}
        />
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <DatabaseIcon className="size-7" />
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">Manage sources</h1>
              <p className="text-sm text-muted-foreground">
                Job status and disconnect for SharePoint folders Chat already knows about. To add a
                folder, use Add SharePoint folder in Chat or Help.
              </p>
            </div>
          </div>
          <AddSharePointFolderButton>Add SharePoint folder</AddSharePointFolderButton>
        </div>
      </div>

      <main className="mx-auto flex w-full max-w-[960px] flex-1 flex-col gap-6 p-6">
        {disconnectMutation.isError && (
          <p className="text-sm text-destructive">
            {disconnectMutation.error instanceof Error
              ? disconnectMutation.error.message
              : "Could not disconnect."}
          </p>
        )}
        <section className="flex flex-col gap-3">
          <h2 className="text-base font-semibold">Connected folders</h2>
          <p className="text-xs text-muted-foreground">
            Disconnect stops future sync and hides those documents from Chat. Indexed chunks are kept
            in v1 (not deleted).
          </p>
          {sourcesQuery.isLoading ? (
            <Spinner label="Loading sources…" />
          ) : connected.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No SharePoint folders are connected yet. Use Add SharePoint folder in Chat (or Help) to
              paste a site URL and path.
            </p>
          ) : (
            <div className="flex flex-col gap-3">
              {connected.map((source) => (
                <SourceRow
                  key={source.id}
                  source={source}
                  disconnecting={disconnectMutation.isPending}
                  onDisconnect={() => disconnectMutation.mutate(source.id)}
                />
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

function SourceRow({
  source,
  disconnecting,
  onDisconnect,
}: {
  source: AgentSource;
  disconnecting: boolean;
  onDisconnect: () => void;
}) {
  const job = source.latestJob;
  return (
    <Card>
      <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 flex-1">
          <p className="font-semibold">{source.label || source.displayPath}</p>
          <p className="truncate text-sm text-muted-foreground">{source.displayPath}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Added by {source.createdBy}
            {source.approvalRequestId ? ` · Request #${source.approvalRequestId}` : ""}
          </p>
          {job && (
            <div className="mt-2 flex flex-col gap-1">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant={jobBadgeVariant(job.status)}>{formatJobStatus(job.status)}</Badge>
                <span className="text-xs text-muted-foreground">
                  {job.filesProcessed} indexed
                  {job.filesFailed ? ` · ${job.filesFailed} failed` : ""}
                  {job.filesSkipped ? ` · ${job.filesSkipped} skipped` : ""}
                </span>
              </div>
              {job.errorMessage && <p className="text-sm text-destructive">{job.errorMessage}</p>}
            </div>
          )}
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={disconnecting || source.status === "disconnected"}
          onClick={onDisconnect}
        >
          <UnplugIcon />
          Disconnect
        </Button>
      </CardContent>
    </Card>
  );
}

export function AgentSourcesRoute(): React.JSX.Element {
  return (
    <RequireAuth>
      <AgentSourcesPage />
    </RequireAuth>
  );
}

export function AgentSourcesChatLink() {
  return (
    <RouterLink
      to="/knowledge/sources"
      className="mx-auto flex size-10 items-center justify-center rounded-md text-muted-foreground no-underline hover:bg-card"
      title="Manage sources — connected SharePoint folders"
    >
      <DatabaseIcon className="size-5" />
    </RouterLink>
  );
}
