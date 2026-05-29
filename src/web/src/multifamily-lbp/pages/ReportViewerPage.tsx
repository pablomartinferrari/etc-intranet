import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  Button,
  MessageBar,
  MessageBarBody,
  Spinner,
  Tab,
  TabList,
  Text,
  Title1,
  tokens,
} from "@fluentui/react-components";
import { ArrowDownloadRegular } from "@fluentui/react-icons";
import { useState } from "react";
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
  if (!data) return <Text>No report found. Generate one from Reports.</Text>;

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
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "flex-start",
          justifyContent: "space-between",
          gap: tokens.spacingHorizontalM,
          marginBottom: tokens.spacingVerticalM,
        }}
      >
        <div>
          <Title1 block style={{ marginBottom: tokens.spacingVerticalS }}>
            Report viewer
          </Title1>
          <Text block style={{ color: tokens.colorNeutralForeground2 }}>
            {dataTypeLabel(data.dataType)} · generated {new Date(data.generatedAt).toLocaleString()} · statistical
            sample {STATISTICAL_SAMPLE_SIZE}+ readings · uniform/non-uniform below {STATISTICAL_SAMPLE_SIZE}
          </Text>
        </div>
        <Button
          appearance="primary"
          icon={<ArrowDownloadRegular />}
          disabled={exporting}
          onClick={() => void handleExport()}
        >
          {exporting ? <Spinner size="tiny" /> : "Export to Excel"}
        </Button>
      </div>

      {isLegacyReport && (
        <MessageBar intent="warning" style={{ marginBottom: tokens.spacingVerticalM }}>
          <MessageBarBody>
            This report was generated with an older format. Tables are summarized for display. Generate a new
            report from Reports for correct Uniform / Non-uniform breakdown and Excel export.
          </MessageBarBody>
        </MessageBar>
      )}

      <TabList selectedValue={tab} onTabSelect={(_, d) => setTab(d.value as string)}>
        {REPORT_VIEWER_TABS.map((t) => (
          <Tab key={t.key} value={t.key}>
            {t.label}
          </Tab>
        ))}
      </TabList>

      <div style={{ marginTop: tokens.spacingVerticalM }}>
        <ReportSectionGrid
          sectionKey={activeTab.key}
          data={result[activeTab.key]}
          emptyMessage={`No ${activeTab.label.toLowerCase()} in this report.`}
        />
      </div>
    </div>
  );
}
