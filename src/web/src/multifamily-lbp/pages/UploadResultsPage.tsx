import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
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

  if (!batchId) return <p>No batch selected.</p>;
  if (isLoading) return <Spinner label="Loading results…" />;
  if (error || !data) return <p>Could not load import results.</p>;

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold tracking-tight">Upload results</h1>
      <p>
        <strong>{data.sourceFileName}</strong> · {data.dataType} · {data.status} · {data.rowCount} rows imported
      </p>
      {data.warnings.length > 0 && (
        <Alert className="mt-4">
          <AlertDescription>{data.warnings.join("; ")}</AlertDescription>
        </Alert>
      )}
      {data.errors.length > 0 && (
        <Alert variant="destructive" className="mt-4">
          <AlertDescription>{data.errors.join("; ")}</AlertDescription>
        </Alert>
      )}
      <div className="mt-6 flex gap-4">
        <Button onClick={() => nav(`${base}/grid`)}>Go to data grid</Button>
        <Button variant="outline" onClick={() => nav(`${base}/uploads`)}>
          Back to source files
        </Button>
        <Button variant="link" asChild>
          <Link to={base}>Back to dashboard</Link>
        </Button>
      </div>
    </div>
  );
}
