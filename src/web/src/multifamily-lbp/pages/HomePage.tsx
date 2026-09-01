import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useIsAuthenticated } from "@azure/msal-react";
import { useQuery } from "@tanstack/react-query";
import { ArrowRightIcon, SearchIcon } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Spinner } from "@/components/ui/spinner";
import { isAuthRequiredError } from "@mf/auth/AuthRequiredError";
import { SignInPrompt } from "@mf/auth/SignInPrompt";
import { ensureJob, fetchJob, fetchRecentJobs } from "@mf/api/jobs";
import { entityDashboardPath, MULTIFAMILY_LBP_SLUG } from "@mf/config/entities";

export function HomePage(): React.JSX.Element {
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
    return <SignInPrompt message="Sign in to look up jobs and open the lead inspection workspace." />;
  }

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold tracking-tight">Job lookup</h1>
      <Card className="max-w-[520px]">
        <CardHeader>
          <CardTitle>Enter job number</CardTitle>
          <CardDescription>Opens the multifamily LBP dashboard for this project.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-1.5">
            <Label htmlFor="job-number">Job number</Label>
            <Input
              id="job-number"
              value={jobNumber}
              onChange={(e) => setJobNumber(e.target.value)}
              placeholder="e.g. 285744"
              onKeyDown={(e) => e.key === "Enter" && void onLookup()}
            />
          </div>
          <div className="mt-6 flex flex-wrap gap-4">
            <Button onClick={() => void onLookup()} disabled={loading || !jobNumber.trim()}>
              {loading ? (
                <Spinner size="sm" />
              ) : (
                <>
                  <SearchIcon />
                  Look up
                </>
              )}
            </Button>
            <Button variant="outline" onClick={onContinue} disabled={!jobNumber.trim()}>
              <ArrowRightIcon />
              Open multifamily LBP
            </Button>
          </div>
          {error && (
            <Alert variant="destructive" className="mt-4">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
          {preview && (
            <Alert className="mt-4">
              <AlertDescription>
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
              </AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>
      {recentQuery.data && recentQuery.data.length > 0 && (
        <div className="mt-8 max-w-[520px]">
          <p className="mb-2 font-semibold">Recent jobs</p>
          {recentQuery.data.map((j) => (
            <Button
              key={j.jobIdentifier}
              variant="ghost"
              onClick={() => void openJob(j.jobIdentifier)}
              className="mb-1 w-full justify-start"
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
