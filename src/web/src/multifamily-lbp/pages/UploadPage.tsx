import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { DownloadIcon, ExternalLinkIcon, Trash2Icon } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useEntity } from "@mf/context/EntityContext";
import { clearWorkspace, fetchSourceFiles, importLegacy } from "@mf/api/entity";
import { ClearWorkspaceDialog } from "@mf/components/ClearWorkspaceDialog";
import { JobSourceFilesPanel } from "@mf/components/JobSourceFilesPanel";
import { SHAREPOINT_UPLOAD_SITE_URL } from "@mf/config/sharepoint";

type PageMessage = { intent: "error" | "warning" | "success" | "info"; text: string };

export function UploadPage(): React.JSX.Element {
  const nav = useNavigate();
  const { jobId, entitySlug, refetchDashboard, dashboard } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;
  const qc = useQueryClient();
  const [message, setMessage] = useState<PageMessage | null>(null);
  const [clearOpen, setClearOpen] = useState(false);

  const sourceFilesQuery = useQuery({
    queryKey: ["source-files", jobId, entitySlug],
    queryFn: () => fetchSourceFiles(jobId, entitySlug),
    retry: false,
  });

  const importMut = useMutation({
    mutationFn: () => importLegacy(jobId, entitySlug, false),
    onSuccess: async (r) => {
      await refetchDashboard();
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
      void sourceFilesQuery.refetch();
      if (r.imported > 0) {
        setMessage({
          intent: "success",
          text: `Imported ${r.imported.toLocaleString()} reading${r.imported === 1 ? "" : "s"} from ${r.filesAdded} file${r.filesAdded === 1 ? "" : "s"}.`,
        });
        nav(`${base}/grid`, { replace: true });
        return;
      }
      if (r.filesSkipped > 0) {
        setMessage({
          intent: "info",
          text: `All ${r.filesSkipped} SharePoint file${r.filesSkipped === 1 ? "" : "s"} for this job are already imported. Upload another file in SharePoint, then import again.`,
        });
        return;
      }
      setMessage({
        intent: "warning",
        text: "No files found in SharePoint for this job. Upload in SharePoint first, then import again.",
      });
    },
    onError: (e: Error) => {
      setMessage({ intent: "error", text: e.message || "Could not import from SharePoint." });
    },
  });

  const clearMut = useMutation({
    mutationFn: () => clearWorkspace(jobId, entitySlug),
    onSuccess: async (r) => {
      setClearOpen(false);
      await refetchDashboard();
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
      void qc.invalidateQueries({ queryKey: ["normalizations", jobId, entitySlug] });
      setMessage({
        intent: "success",
        text: `Cleared workspace: ${r.rowsRemoved.toLocaleString()} rows, ${r.normalizationsRemoved} normalization suggestions, ${r.reportsRemoved} reports.`,
      });
    },
    onError: (e: Error) => {
      setMessage({ intent: "error", text: e.message || "Could not clear workspace." });
    },
  });

  const sourceFilesError =
    sourceFilesQuery.error instanceof Error ? sourceFilesQuery.error.message : null;

  return (
    <div>
      <h1 className="mb-2 text-2xl font-semibold tracking-tight">Source files</h1>
      <p className="mb-4 text-muted-foreground">
        Upload inspection workbooks in SharePoint (<strong>XRF-SourceFiles</strong>) using the{" "}
        <strong>Lead Inspection — Upload</strong> web part for job <strong>{jobId}</strong>. Then import them here
        for grid review, normalization, and reports.
      </p>

      <div className="my-6 flex flex-wrap gap-4">
        <Button variant="secondary" asChild>
          <a href={SHAREPOINT_UPLOAD_SITE_URL} target="_blank" rel="noopener noreferrer">
            <ExternalLinkIcon />
            Open SharePoint site
          </a>
        </Button>
        <Button
          disabled={importMut.isPending}
          onClick={() => {
            setMessage(null);
            importMut.mutate();
          }}
        >
          {importMut.isPending ? (
            <Spinner size="sm" />
          ) : (
            <>
              <DownloadIcon />
              Import into workspace
            </>
          )}
        </Button>
        {dashboard?.hasRows && (
          <Button variant="secondary" onClick={() => nav(`${base}/grid`)}>
            Open data grid
          </Button>
        )}
        {dashboard?.hasRows && (
          <Button variant="secondary" onClick={() => setClearOpen(true)}>
            <Trash2Icon />
            Clear workspace data
          </Button>
        )}
      </div>

      <ClearWorkspaceDialog
        open={clearOpen}
        pending={clearMut.isPending}
        onConfirm={() => clearMut.mutate()}
        onCancel={() => setClearOpen(false)}
      />

      {message && (
        <Alert variant={message.intent === "error" ? "destructive" : "default"} className="mb-4">
          <AlertDescription>{message.text}</AlertDescription>
        </Alert>
      )}

      <JobSourceFilesPanel
        jobId={jobId}
        files={sourceFilesQuery.data ?? []}
        loading={sourceFilesQuery.isLoading}
        error={sourceFilesError}
      />
    </div>
  );
}
