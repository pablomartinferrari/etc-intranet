import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Button, MessageBar, MessageBarBody, Spinner, Text, Title1, tokens } from "@fluentui/react-components";
import { useEntity } from "@mf/context/EntityContext";
import { fetchUploadResults } from "@mf/api/entity";

export function UploadResultsPage(): React.JSX.Element {
  const { jobId, entitySlug } = useEntity();
  const [params] = useSearchParams();
  const batchId = params.get("batchId") ?? "";
  const nav = useNavigate();
  const base = `/jobs/${jobId}/${entitySlug}`;

  const { data, isLoading, error } = useQuery({
    queryKey: ["upload-results", jobId, entitySlug, batchId],
    queryFn: () => fetchUploadResults(jobId, entitySlug, batchId),
    enabled: Boolean(batchId),
  });

  if (!batchId) return <Text>No batch selected.</Text>;
  if (isLoading) return <Spinner label="Loading results…" />;
  if (error || !data) return <Text>Could not load import results.</Text>;

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        Upload results
      </Title1>
      <Text block>
        <strong>{data.sourceFileName}</strong> · {data.dataType} · {data.status} · {data.rowCount} rows imported
      </Text>
      {data.warnings.length > 0 && (
        <MessageBar intent="warning" style={{ marginTop: tokens.spacingVerticalM }}>
          <MessageBarBody>{data.warnings.join("; ")}</MessageBarBody>
        </MessageBar>
      )}
      {data.errors.length > 0 && (
        <MessageBar intent="error" style={{ marginTop: tokens.spacingVerticalM }}>
          <MessageBarBody>{data.errors.join("; ")}</MessageBarBody>
        </MessageBar>
      )}
      <div style={{ display: "flex", gap: tokens.spacingHorizontalM, marginTop: tokens.spacingVerticalL }}>
        <Button appearance="primary" onClick={() => nav(`${base}/grid`)}>
          Go to data grid
        </Button>
        <Button onClick={() => nav(`${base}/uploads`)}>Upload more</Button>
        <Link to={base}>Back to dashboard</Link>
      </div>
    </div>
  );
}
