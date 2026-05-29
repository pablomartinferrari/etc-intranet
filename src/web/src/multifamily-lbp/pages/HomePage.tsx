import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useIsAuthenticated } from "@azure/msal-react";
import { useQuery } from "@tanstack/react-query";
import { isAuthRequiredError } from "@mf/auth/AuthRequiredError";
import { SignInPrompt } from "@mf/auth/SignInPrompt";
import {
  Button,
  Card,
  CardHeader,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Spinner,
  Text,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { SearchRegular, ArrowRightRegular } from "@fluentui/react-icons";
import { ensureJob, fetchJob, fetchRecentJobs } from "@mf/api/jobs";
import { entityDashboardPath, MULTIFAMILY_LBP_SLUG } from "@mf/config/entities";

const useStyles = makeStyles({
  card: { maxWidth: "520px" },
  actions: { display: "flex", gap: tokens.spacingHorizontalM, marginTop: tokens.spacingVerticalL, flexWrap: "wrap" },
  recent: { marginTop: tokens.spacingVerticalXL, maxWidth: "520px" },
});

export function HomePage(): React.JSX.Element {
  const styles = useStyles();
  const nav = useNavigate();
  const isAuthenticated = useIsAuthenticated();
  const [jobNumber, setJobNumber] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<Awaited<ReturnType<typeof fetchJob>> | undefined>(undefined);

  const recentQuery = useQuery({
    queryKey: ["recent-jobs"],
    queryFn: () => fetchRecentJobs(10),
    enabled: isAuthenticated,
  });

  const openJob = async (id: string): Promise<void> => {
    const v = id.trim();
    if (!v) return;
    await ensureJob(v);
    nav(entityDashboardPath(v, MULTIFAMILY_LBP_SLUG));
  };

  const onLookup = async (): Promise<void> => {
    const v = jobNumber.trim();
    if (!v) return;
    setError(null);
    setPreview(undefined);
    setLoading(true);
    try {
      const job = await fetchJob(v);
      setPreview(job);
      if (!job) setError("Job not found via API. You can still open the workspace to create it.");
    } catch (e) {
      if (isAuthRequiredError(e)) {
        setError("Sign in to look up jobs.");
      } else {
        setError(e instanceof Error ? e.message : "Lookup failed");
      }
    } finally {
      setLoading(false);
    }
  };

  const onContinue = (): void => {
    void openJob(jobNumber);
  };

  if (!isAuthenticated) {
    return (
      <SignInPrompt message="Sign in to look up jobs and open the lead inspection workspace." />
    );
  }

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        Job lookup
      </Title1>
      <Card className={styles.card}>
        <CardHeader
          header={<Text weight="semibold">Enter job number</Text>}
          description={
            <Text size={200}>
              Opens the multifamily LBP dashboard for this project.
            </Text>
          }
        />
        <div style={{ padding: tokens.spacingHorizontalL }}>
          <Field label="Job number">
            <Input
              value={jobNumber}
              onChange={(_, d) => setJobNumber(d.value)}
              placeholder="e.g. 285744"
              onKeyDown={(e) => e.key === "Enter" && void onLookup()}
            />
          </Field>
          <div className={styles.actions}>
            <Button
              appearance="primary"
              icon={<SearchRegular />}
              onClick={() => void onLookup()}
              disabled={loading || !jobNumber.trim()}
            >
              {loading ? <Spinner size="tiny" /> : "Look up"}
            </Button>
            <Button icon={<ArrowRightRegular />} onClick={onContinue} disabled={!jobNumber.trim()}>
              Open multifamily LBP
            </Button>
          </div>
          {error && (
            <MessageBar intent="error" style={{ marginTop: tokens.spacingVerticalM }}>
              <MessageBarBody>{error}</MessageBarBody>
            </MessageBar>
          )}
          {preview && (
            <MessageBar intent="success" style={{ marginTop: tokens.spacingVerticalM }}>
              <MessageBarBody>
                <strong>Job {preview.jobId}</strong>
                {preview.jobStatus && ` · Status ${preview.jobStatus}`}
                <br />
                {preview.clientName && <>Client: {preview.clientName}</>}
                {(preview.facilityName || preview.facilityAddress) && (
                  <>
                    <br />
                    {[preview.facilityName, preview.facilityAddress].filter(Boolean).join(" · ")}
                  </>
                )}
              </MessageBarBody>
            </MessageBar>
          )}
        </div>
      </Card>
      {recentQuery.data && recentQuery.data.length > 0 && (
        <div className={styles.recent}>
          <Text weight="semibold" block style={{ marginBottom: tokens.spacingVerticalS }}>
            Recent jobs
          </Text>
          {recentQuery.data.map((j) => (
            <Button
              key={j.jobIdentifier}
              appearance="subtle"
              onClick={() => void openJob(j.jobIdentifier)}
              style={{ display: "block", marginBottom: tokens.spacingVerticalXS }}
            >
              {j.jobIdentifier}
              {j.facilityName ? ` · ${j.facilityName}` : ""}
            </Button>
          ))}
        </div>
      )}
    </div>
  );
}
