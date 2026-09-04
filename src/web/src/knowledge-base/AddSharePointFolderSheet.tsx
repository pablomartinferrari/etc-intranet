import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FolderPlusIcon } from "lucide-react";
import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { Link as RouterLink } from "react-router-dom";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Spinner } from "@/components/ui/spinner";
import {
  AgentSourceApiError,
  connectAgentSource,
  getAgentSourceCapabilities,
  probeAgentSource,
  type AgentSourceProbe,
} from "./api/sources";

type AgentSourceUi = {
  openAddFolder: () => void;
};

const AgentSourceUiContext = createContext<AgentSourceUi | null>(null);

export function useAddSharePointFolder(): AgentSourceUi {
  const ctx = useContext(AgentSourceUiContext);
  if (!ctx) {
    return { openAddFolder: () => undefined };
  }
  return ctx;
}

export function AgentSourceUiProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const openAddFolder = useCallback(() => setOpen(true), []);

  return (
    <AgentSourceUiContext.Provider value={{ openAddFolder }}>
      {children}
      <AddSharePointFolderSheet open={open} onOpenChange={setOpen} />
    </AgentSourceUiContext.Provider>
  );
}

export function AddSharePointFolderButton({
  variant = "default",
  size = "default",
  className,
  children = "Add SharePoint folder",
}: {
  variant?: "default" | "outline" | "ghost" | "secondary";
  size?: "default" | "sm" | "lg";
  className?: string;
  children?: ReactNode;
}) {
  const { openAddFolder } = useAddSharePointFolder();
  return (
    <Button type="button" variant={variant} size={size} className={className} onClick={openAddFolder}>
      <FolderPlusIcon />
      {children}
    </Button>
  );
}

export function AddSharePointFolderSheet({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
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
    enabled: open,
  });

  function reset() {
    setSiteUrl("");
    setFolderPath("");
    setLabel("");
    setProbe(null);
    setError(null);
    setNotice(null);
  }

  function handleOpenChange(next: boolean) {
    onOpenChange(next);
    if (!next) {
      reset();
    }
  }

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
      connectAgentSource(
        siteUrl.trim(),
        folderPath.trim() || undefined,
        label.trim() || undefined,
        confirmMedium,
      ),
    onSuccess: (source) => {
      setNotice(
        source.status === "awaiting_approval"
          ? `This folder is too large for self-serve ingest. Approval request #${source.approvalRequestId ?? "—"} was filed under Feature Requests.`
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

  const capabilities = capabilitiesQuery.data;

  return (
    <Sheet open={open} onOpenChange={handleOpenChange}>
      <SheetContent className="z-[60] sm:max-w-lg" side="right">
        <SheetHeader>
          <SheetTitle>Add SharePoint folder</SheetTitle>
          <SheetDescription>
            Paste a site URL and folder path so ETC Chat can use those documents. Chat estimates size
            before anything is ingested.
          </SheetDescription>
        </SheetHeader>
        <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-4 pb-2">
          {error && (
            <Alert variant="destructive">
              <AlertTitle>Could not add that folder</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
          {notice && (
            <Alert>
              <AlertTitle>Saved</AlertTitle>
              <AlertDescription>
                {notice}{" "}
                <RouterLink to="/knowledge/sources" className="underline underline-offset-4">
                  Sources
                </RouterLink>
              </AlertDescription>
            </Alert>
          )}
          {capabilities && !capabilities.graphConfigured && (
            <Alert>
              <AlertTitle>SharePoint Graph is not fully wired</AlertTitle>
              <AlertDescription>
                Probe and ingest need the Entra app client secret plus Sites.Read.All / Files.Read.All.
                You can still fill this in; the API will return a readable error until Graph is configured.
              </AlertDescription>
            </Alert>
          )}
          <p className="text-sm text-muted-foreground">
            Automatic ingest is for folders up to about{" "}
            {capabilities ? capabilities.softMaxFiles.toLocaleString() : "2,000"} files and 2 GB.
            Larger folders ask for confirmation; huge folders file an admin request instead.
          </p>
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-2">
              <Label htmlFor="agent-source-site-url">SharePoint site URL</Label>
              <Input
                id="agent-source-site-url"
                placeholder="https://contoso.sharepoint.com/sites/Company"
                value={siteUrl}
                onChange={(event) => setSiteUrl(event.target.value)}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="agent-source-folder-path">Folder path</Label>
              <Input
                id="agent-source-folder-path"
                placeholder="Shared Documents/Policies"
                value={folderPath}
                onChange={(event) => setFolderPath(event.target.value)}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="agent-source-label">Label (optional)</Label>
              <Input
                id="agent-source-label"
                placeholder="HR policies"
                value={label}
                onChange={(event) => setLabel(event.target.value)}
              />
            </div>
          </div>
          {probeMutation.isPending && <Spinner label="Counting files in SharePoint…" />}
          {probe && <ProbeSummary probe={probe} />}
        </div>
        <SheetFooter>
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
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

function ProbeSummary({ probe }: { probe: AgentSourceProbe }) {
  return (
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
        <p className="mt-2 text-muted-foreground">Sample types: {probe.sampleExtensions.join(", ")}</p>
      )}
      {probe.truncated && (
        <p className="mt-2 text-muted-foreground">
          Probe stopped early on a very large tree. An admin should review before ingest.
        </p>
      )}
      {probe.requiresConfirm && (
        <p className="mt-3 font-medium">
          This is larger than the automatic limit. Click Confirm and connect if you still want to ingest it.
        </p>
      )}
    </div>
  );
}
