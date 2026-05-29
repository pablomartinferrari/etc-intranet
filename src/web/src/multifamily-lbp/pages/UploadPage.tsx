import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Button,
  Link,
  MessageBar,
  MessageBarBody,
  Spinner,
  Text,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { ArrowDownloadRegular, DeleteRegular, OpenRegular } from "@fluentui/react-icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEntity } from "@mf/context/EntityContext";
import { clearWorkspace, fetchSourceFiles, importLegacy } from "@mf/api/entity";
import { ClearWorkspaceDialog } from "@mf/components/ClearWorkspaceDialog";
import { JobSourceFilesPanel } from "@mf/components/JobSourceFilesPanel";
import { SHAREPOINT_UPLOAD_SITE_URL } from "@mf/config/sharepoint";

const useStyles = makeStyles({
  actions: {
    display: "flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalL,
  },
});

type PageMessage = { intent: "error" | "warning" | "success" | "info"; text: string };

export function UploadPage(): React.JSX.Element {
  const styles = useStyles();
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
      <Title1 block style={{ marginBottom: tokens.spacingVerticalS }}>
        Source files
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM, color: tokens.colorNeutralForeground2 }}>
        Upload inspection workbooks in SharePoint (<strong>XRF-SourceFiles</strong>) using the{" "}
        <strong>Lead Inspection — Upload</strong> web part for job <strong>{jobId}</strong>. Then import them here
        for grid review, normalization, and reports.
      </Text>

      <div className={styles.actions}>
        <Link href={SHAREPOINT_UPLOAD_SITE_URL} target="_blank" rel="noopener noreferrer">
          <Button appearance="secondary" icon={<OpenRegular />}>
            Open SharePoint site
          </Button>
        </Link>
        <Button
          appearance="primary"
          icon={<ArrowDownloadRegular />}
          disabled={importMut.isPending}
          onClick={() => {
            setMessage(null);
            importMut.mutate();
          }}
        >
          {importMut.isPending ? <Spinner size="tiny" /> : "Import into workspace"}
        </Button>
        {dashboard?.hasRows && (
          <Button appearance="secondary" onClick={() => nav(`${base}/grid`)}>
            Open data grid
          </Button>
        )}
        {dashboard?.hasRows && (
          <Button
            appearance="secondary"
            icon={<DeleteRegular />}
            onClick={() => setClearOpen(true)}
          >
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
        <MessageBar intent={message.intent} style={{ marginBottom: tokens.spacingVerticalM }}>
          <MessageBarBody>{message.text}</MessageBarBody>
        </MessageBar>
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
