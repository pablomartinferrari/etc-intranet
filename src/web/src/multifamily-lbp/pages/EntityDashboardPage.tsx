import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  DownloadIcon,
  FileTextIcon,
  ListTreeIcon,
  SparklesIcon,
  TableIcon,
  Trash2Icon,
} from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useEntity } from "@mf/context/EntityContext";
import { clearWorkspace } from "@mf/api/entity";
import { ClearWorkspaceDialog } from "@mf/components/ClearWorkspaceDialog";

export function EntityDashboardPage(): React.JSX.Element {
  const nav = useNavigate();
  const { jobId, entitySlug, dashboard, refetchDashboard } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;
  const qc = useQueryClient();
  const [clearOpen, setClearOpen] = useState(false);

  const clearMut = useMutation({
    mutationFn: () => clearWorkspace(jobId, entitySlug),
    onSuccess: async () => {
      setClearOpen(false);
      await refetchDashboard();
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
      void qc.invalidateQueries({ queryKey: ["normalizations", jobId, entitySlug] });
    },
  });

  if (!dashboard) return <p>Loading dashboard…</p>;

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold tracking-tight">Dashboard</h1>
      <div className="mb-8 grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-4">
        <Card>
          <CardHeader>
            <CardTitle>Uploaded files</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-4xl font-bold">{dashboard.uploadedFilesCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Units rows</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-4xl font-bold">{dashboard.unitsRowCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Common areas rows</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-4xl font-bold">{dashboard.commonAreasRowCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Validation warnings</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-4xl font-bold">{dashboard.validationWarningCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Pending AI review</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-4xl font-bold">{dashboard.pendingNormalizationCount}</p>
          </CardContent>
        </Card>
      </div>
      <div className="flex flex-wrap gap-4">
        <Button onClick={() => nav(`${base}/uploads`)}>
          <DownloadIcon />
          Import from SharePoint
        </Button>
        {dashboard.hasRows && (
          <Button variant="outline" onClick={() => nav(`${base}/grid`)}>
            <TableIcon />
            Open data grid
          </Button>
        )}
        <Button variant="outline" onClick={() => nav(`${base}/normalize`)}>
          <SparklesIcon />
          Run normalization
        </Button>
        {dashboard.pendingNormalizationCount > 0 && (
          <Button variant="outline" onClick={() => nav(`${base}/normalize/review`)}>
            <SparklesIcon />
            Review AI suggestions
          </Button>
        )}
        {dashboard.hasRows && (
          <Button variant="outline" onClick={() => nav(`${base}/grid/groups`)}>
            <ListTreeIcon />
            Grouped readings
          </Button>
        )}
        <Button variant="outline" onClick={() => nav(`${base}/reports/configure`)}>
          <FileTextIcon />
          Generate report
        </Button>
        {dashboard.hasRows && (
          <Button variant="outline" onClick={() => setClearOpen(true)}>
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
    </div>
  );
}
