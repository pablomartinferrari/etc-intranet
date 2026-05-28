import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Spinner, Tab, TabList, Text, Title1, tokens } from "@fluentui/react-components";
import { useState } from "react";
import { useEntity } from "@mf/context/EntityContext";
import { fetchReport, fetchLatestReport } from "@mf/api/entity";

export function ReportViewerPage(): React.JSX.Element {
  const { jobId, entitySlug } = useEntity();
  const [params] = useSearchParams();
  const reportId = params.get("reportId");
  const [tab, setTab] = useState("allShots");

  const { data, isLoading } = useQuery({
    queryKey: ["report", jobId, entitySlug, reportId],
    queryFn: () =>
      reportId ? fetchReport(jobId, entitySlug, reportId) : fetchLatestReport(jobId, entitySlug),
  });

  if (isLoading) return <Spinner label="Loading report…" />;
  if (!data) return <Text>No report found. Generate one from Reports.</Text>;

  const result = data.result as Record<string, unknown>;
  const tabData: Record<string, unknown> = {
    allShots: result.allShots,
    uniformShots: result.uniformShots,
    nonUniformShots: result.nonUniformShots,
    byComponent: result.byComponent,
    bySubstrate: result.bySubstrate,
    exceptions: result.exceptions,
  };

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalS }}>
        Report viewer
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM }}>
        Generated {new Date(data.generatedAt).toLocaleString()} · threshold {data.uniformThreshold}
      </Text>
      <TabList selectedValue={tab} onTabSelect={(_, d) => setTab(d.value as string)}>
        <Tab value="allShots">All Shots</Tab>
        <Tab value="uniformShots">Uniform</Tab>
        <Tab value="nonUniformShots">Non-Uniform</Tab>
        <Tab value="byComponent">By Component</Tab>
        <Tab value="bySubstrate">By Substrate</Tab>
        <Tab value="exceptions">Exceptions</Tab>
      </TabList>
      <pre
        style={{
          marginTop: tokens.spacingVerticalM,
          padding: tokens.spacingVerticalM,
          background: tokens.colorNeutralBackground3,
          overflow: "auto",
          maxHeight: "480px",
          fontSize: tokens.fontSizeBase200,
        }}
      >
        {JSON.stringify(tabData[tab] ?? [], null, 2)}
      </pre>
    </div>
  );
}
