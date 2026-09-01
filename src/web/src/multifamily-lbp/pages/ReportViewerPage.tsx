import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { DownloadIcon } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useEntity } from "@mf/context/EntityContext";
import { downloadReportExcel, fetchReport, fetchLatestReport } from "@mf/api/entity";
import {
  reconcileReportResultForViewer,
  ReportSectionGrid,
} from "@mf/components/ReportSectionGrid";
import { dataTypeLabel, REPORT_VIEWER_TABS, STATISTICAL_SAMPLE_SIZE } from "@mf/config/reportOptions";

export function ReportViewerPage(): React.JSX.Element {
  const { jobId, entitySlug } = useEntity();
  const [params] = useSearchParams();
  const reportId = params.get("reportId");
  const [tab, setTab] = useState<string>(REPORT_VIEWER_TABS[0].key);
  const [exporting, setExporting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ["report", jobId, entitySlug, reportId],
    queryFn: () =>
      reportId ? fetchReport(jobId, entitySlug, reportId) : fetchLatestReport(jobId, entitySlug),
  });

  const handleExport = async (): Promise<void> => {
    if (!data?.id) return;
    setExporting(true);
    try {
      await downloadReportExcel(jobId, entitySlug, data.id);
    } finally {
      setExporting(false);
    }
  };

  if (isLoading) return <Spinner label="Loading report…" />;
  if (!data) return <p>No report found. Generate one from Reports.</p>;

  const rawResult = data.result as Record<string, unknown>;
  const result = reconcileReportResultForViewer(rawResult);
  const activeTab = REPORT_VIEWER_TABS.find((t) => t.key === tab) ?? REPORT_VIEWER_TABS[0];

  const nonUniformRaw = rawResult.nonUniformShots;
  const isLegacyReport =
    Array.isArray(nonUniformRaw) &&
    nonUniformRaw.length > 0 &&
    typeof nonUniformRaw[0] === "object" &&
    nonUniformRaw[0] != null &&
    !("positiveCount" in (nonUniformRaw[0] as object)) &&
    ("readingId" in (nonUniformRaw[0] as object) || "readings" in (nonUniformRaw[0] as object));

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="mb-2 text-2xl font-semibold tracking-tight">Report viewer</h1>
          <p className="text-muted-foreground">
            {dataTypeLabel(data.dataType)} · generated {new Date(data.generatedAt).toLocaleString()} · statistical
            sample {STATISTICAL_SAMPLE_SIZE}+ readings · uniform/non-uniform below {STATISTICAL_SAMPLE_SIZE}
          </p>
        </div>
        <Button disabled={exporting} onClick={() => void handleExport()}>
          {exporting ? (
            <Spinner size="sm" />
          ) : (
            <>
              <DownloadIcon />
              Export to Excel
            </>
          )}
        </Button>
      </div>

      {isLegacyReport && (
        <Alert className="mb-4">
          <AlertDescription>
            This report was generated with an older format. Tables are summarized for display. Generate a new
            report from Reports for correct Uniform / Non-uniform breakdown and Excel export.
          </AlertDescription>
        </Alert>
      )}

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          {REPORT_VIEWER_TABS.map((t) => (
            <TabsTrigger key={t.key} value={t.key}>
              {t.label}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      <div className="mt-4">
        <ReportSectionGrid
          sectionKey={activeTab.key}
          data={result[activeTab.key]}
          emptyMessage={`No ${activeTab.label.toLowerCase()} in this report.`}
        />
      </div>
    </div>
  );
}
