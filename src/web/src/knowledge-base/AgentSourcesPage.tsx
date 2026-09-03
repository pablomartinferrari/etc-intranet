import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { DatabaseIcon, FolderPlusIcon, UnplugIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";

import { BrandBar, SignOutButton } from "@/components/brand-bar";
import { PageBreadcrumb } from "@/components/page-breadcrumb";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Spinner } from "@/components/ui/spinner";
import { RequireAuth } from "../multifamily-lbp/auth/RequireAuth";
import {
  AgentSourceApiError,
  connectAgentSource,
  disconnectAgentSource,
  getAgentSourceCapabilities,
  listAgentSources,
  probeAgentSource,
  type AgentSource,
  type AgentSourceProbe,
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
  const [siteUrl, setSiteUrl] = useState("");
  const [folderPath, setFolderPath] = useState("");
  const [label, setLabel] = useState("");
  const [probe, setProbe] = useState<AgentSourceProbe | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const capabilitiesQuery = useQuery({
    queryKey: ["kb-source-capabilities"],
    queryFn: getAgentSourceCapabilities,
  });

  const sourcesQuery = useQuery({
    queryKey: ["kb-sources"],
    queryFn: listAgentSources,
    refetchInterval: (query) => {
      const rows = query.state.data ?? [];
      return rows.some((s) => s.latestJob && ACTIVE_JOB.has(s.latestJob.status)) ? 3000 : false;
    },
  });

  const probeMutation = useMutation({
    mutationFn: () => probeAgentSource(siteUrl.trim(), folderPath.trim() || undefined),
    onSuccess: (result) => {
      setProbe(result);
      setError(null);
      setNotice(null);
    },
    onError: (err: Error) => {
      setProbe(null);
      setError(err.message || "Could not probe that folder.");
    },
  });

  const connectMutation = useMutation({
    mutationFn: (confirmMedium: boolean) =>
      connectAgentSource(siteUrl.trim(), folderPath.trim() || undefined, label.trim() || undefined, confirmMedium),
    onSuccess: (source) => {
      setNotice(
        source.status === "awaiting_approval"
          ? `This folder is too large for self-serve ingest. Approval request #${source.approvalRequestId ?? "—"} was filed under Feature Requests (Chat agent sources).`
          : "Folder connected. Ingest is queued — Chat will use these documents when the job finishes.",
      );
      setError(null);
      setProbe(null);
      setSiteUrl("");
      setFolderPath("");
      setLabel("");
      void queryClient.invalidateQueries({ queryKey: ["kb-sources"] });
    },
    onError: (err: Error) => {
      if (err instanceof AgentSourceApiError && err.code === "confirmRequired" && err.probe) {
        setProbe(err.probe);
        setError(null);
        return;
      }
      setError(err.message || "Could not connect that folder.");
    },
  });

  const disconnectMutation = useMutation({
    mutationFn: disconnectAgentSource,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["kb-sources"] });
    },
    onError: (err: Error) => setError(err.message || "Could not disconnect."),
  });

  const capabilities = capabilitiesQuery.data;
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
            { label: "Agent sources" },
          ]}
        />
        <div className="flex flex-wrap items-center gap-3">
          <DatabaseIcon className="size-7" />
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Agent sources</h1>
            <p className="text-sm text-muted-foreground">
              Add a SharePoint folder so ETC Chat can use those documents.
            </p>
          </div>
        </div>
      </div>

      <main className="mx-auto flex w-full max-w-[960px] flex-1 flex-col gap-6 p-6">
        {error && (
          <Alert variant="destructive">
            <AlertTitle>Could not add that folder</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {notice && (
          <Alert>
            <AlertTitle>Saved</AlertTitle>
            <AlertDescription>{notice}</AlertDescription>
          </Alert>
        )}

        {capabilities && !capabilities.graphConfigured && (
          <Alert>
            <AlertTitle>SharePoint is not fully wired in this environment</AlertTitle>
            <AlertDescription>
              You can still use this page. Probe and ingest need AzureAd tenant, client, and secret plus Graph
              Sites.Read.All / Files.Read.All on the Entra app. Hosted embeddings use
              KnowledgeBase__Embeddings__ApiKey (or the chat fallback key).
            </AlertDescription>
          </Alert>
        )}

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FolderPlusIcon className="size-5" />
              Add SharePoint folder
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">
              Paste the site URL and folder path. Chat will estimate size before anything is ingested. Automatic
              ingest is for folders up to about {capabilities ? capabilities.softMaxFiles.toLocaleString() : "2,000"}{" "}
              files and 2 GB. Larger folders ask for confirmation; huge folders file an admin request instead.
            </p>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-2 sm:col-span-2">
                <Label htmlFor="siteUrl">SharePoint site URL</Label>
                <Input
                  id="siteUrl"
                  placeholder="https://contoso.sharepoint.com/sites/Company"
                  value={siteUrl}
                  onChange={(event) => setSiteUrl(event.target.value)}
                />
              </div>
              <div className="flex flex-col gap-2">
                <Label htmlFor="folderPath">Folder path</Label>
                <Input
                  id="folderPath"
                  placeholder="Shared Documents/Policies"
                  value={folderPath}
                  onChange={(event) => setFolderPath(event.target.value)}
                />
              </div>
              <div className="flex flex-col gap-2">
                <Label htmlFor="label">Label (optional)</Label>
                <Input
                  id="label"
                  placeholder="HR policies"
                  value={label}
                  onChange={(event) => setLabel(event.target.value)}
                />
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                disabled={!siteUrl.trim() || probeMutation.isPending}
                onClick={() => probeMutation.mutate()}
              >
                {probeMutation.isPending ? "Probing…" : "Estimate folder"}
              </Button>
              <Button
                type="button"
                disabled={!siteUrl.trim() || connectMutation.isPending}
                onClick={() => connectMutation.mutate(probe?.requiresConfirm ?? false)}
              >
                {connectMutation.isPending
                  ? "Connecting…"
                  : probe?.requiresApproval
                    ? "Request admin approval"
                    : probe?.requiresConfirm
                      ? "Confirm and connect"
                      : "Connect folder"}
              </Button>
            </div>

            {probeMutation.isPending && <Spinner label="Counting files in SharePoint…" />}

            {probe && (
              <div className="rounded-lg border bg-muted/50 p-4 text-sm">
                <p className="font-medium">{probe.displayPath}</p>
                <p className="mt-2 text-muted-foreground">{probe.summary}</p>
                <ul className="mt-3 grid gap-1 sm:grid-cols-2">
                  <li>Ingestible files: {probe.allowedFiles.toLocaleString()}</li>
                  <li>Ingestible size: {probe.allowedBytesLabel}</li>
                  <li>All files seen: {probe.fileCount.toLocaleString()}</li>
                  <li>Skipped (video/iso/oversize): {probe.skippedFiles.toLocaleString()}</li>
                  <li>Depth: {probe.maxDepth}</li>
                  <li>Limit: {probe.limitTier}</li>
                </ul>
                {probe.sampleExtensions.length > 0 && (
                  <p className="mt-2 text-muted-foreground">
                    Sample types: {probe.sampleExtensions.join(", ")}
                  </p>
                )}
                {probe.truncated && (
                  <p className="mt-2 text-muted-foreground">
                    Probe stopped early on a very large tree. An admin should review before ingest.
                  </p>
                )}
                {probe.requiresConfirm && (
                  <p className="mt-3 font-medium">
                    This is larger than the automatic limit. Click Confirm and connect if you still want to ingest
                    it.
                  </p>
                )}
              </div>
            )}
          </CardContent>
        </Card>

        <section className="flex flex-col gap-3">
          <h2 className="text-base font-semibold">Connected folders</h2>
          <p className="text-xs text-muted-foreground">
            Disconnect stops future sync and hides those documents from Chat. Indexed chunks are kept in v1 (not
            deleted).
          </p>
          {sourcesQuery.isLoading ? (
            <Spinner label="Loading sources…" />
          ) : connected.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No SharePoint folders are connected yet. Add one above so Chat can use those documents company-wide.
              Disconnect later stops new ingest and hides those docs from Chat; already-indexed chunks stay in the
              database.
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
              {job.errorMessage && (
                <p className="text-sm text-destructive">{job.errorMessage}</p>
              )}
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
      title="Agent sources — add SharePoint folders to Chat"
    >
      <DatabaseIcon className="size-5" />
    </RouterLink>
  );
}
