import { ShieldIcon } from "lucide-react";
import { useMsal } from "@azure/msal-react";
import { useParams } from "react-router-dom";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { apiRequest, signInRequest } from "../../authConfig";

export interface SignInPromptProps {
  /** Extra detail below the main heading. */
  message?: string;
  /** Override job number from the route (e.g. SharePoint deep link). */
  jobId?: string;
}

/**
 * Friendly gate before any intranet API calls — used for SharePoint deep links and job routes.
 */
export function SignInPrompt({ message, jobId: jobIdProp }: SignInPromptProps): React.JSX.Element {
  const { instance } = useMsal();
  const { jobId: jobIdParam } = useParams<{ jobId?: string }>();
  const missingApiScope = apiRequest.scopes.length === 0;
  const jobId = jobIdProp ?? jobIdParam;

  const detail =
    message ??
    (jobId
      ? "Your SharePoint upload is already saved. Sign in to import readings, review the data grid, and generate reports."
      : "Sign in with your Microsoft work account to open the lead inspection workspace.");

  return (
    <div className="flex justify-center py-16">
      <Card className="grid w-full max-w-xl gap-4 p-8">
        <CardContent className="grid gap-4 p-0">
          <div className="flex items-center gap-3">
            <ShieldIcon className="size-8" />
            <h2 className="text-xl font-semibold">Please sign in to continue</h2>
          </div>

          {jobId && (
            <div>
              <p className="mb-2 text-base font-semibold">Continuing from SharePoint</p>
              <span className="inline-block rounded-md bg-muted px-3 py-1 font-semibold">
                Job {jobId}
              </span>
            </div>
          )}

          <p>{detail}</p>

          {missingApiScope && (
            <Alert>
              <AlertDescription>
                This build is missing <code>VITE_API_SCOPE</code>. Redeploy the intranet web app with Entra
                settings configured.
              </AlertDescription>
            </Alert>
          )}

          <div className="flex flex-col items-start gap-3">
            <Button
              size="lg"
              disabled={missingApiScope}
              onClick={() => {
                const returnPath = `${window.location.pathname}${window.location.search}`;
                sessionStorage.setItem("mf-post-login-nav", returnPath);
                void instance.loginRedirect(signInRequest);
              }}
            >
              Sign in with Microsoft
            </Button>
            <p className="text-xs text-muted-foreground">
              Use the same work account you use for SharePoint and Microsoft 365.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
