import { useEffect, useState } from "react";
import { useIsAuthenticated } from "@azure/msal-react";
import { Navigate, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { MessageBar, MessageBarBody, Spinner, Text } from "@fluentui/react-components";
import { isAuthRequiredError } from "@mf/auth/AuthRequiredError";
import { savePostLoginReturnPath } from "@mf/auth/jobEntryPaths";
import { ensureJob } from "@mf/api/jobs";
import { fetchEntityDashboard, importLegacy } from "@mf/api/entity";
import { isValidEntitySlug } from "@mf/config/entities";

/**
 * Entry route from SharePoint (`/jobs/:jobId/multifamily-lbp?import=1`).
 * Ensures the job exists, optionally imports SharePoint files into SQL, then
 * redirects per design: data → grid, no data → source files.
 */
export function EntityLandingPage(): React.JSX.Element {
  const { jobId = "", entitySlug = "" } = useParams<{ jobId: string; entitySlug: string }>();
  const [searchParams] = useSearchParams();
  const nav = useNavigate();
  const isAuthenticated = useIsAuthenticated();
  const [error, setError] = useState<string | null>(null);
  const [authRequired, setAuthRequired] = useState(false);

  const importFromSharePoint = searchParams.get("import") === "1";
  const base = `/jobs/${encodeURIComponent(jobId)}/${encodeURIComponent(entitySlug)}`;

  useEffect(() => {
    if (!jobId || !isValidEntitySlug(entitySlug)) return;

    if (!isAuthenticated) {
      return;
    }

    let cancelled = false;
    setAuthRequired(false);
    setError(null);

    (async () => {
      try {
        await ensureJob(jobId);

        if (importFromSharePoint) {
          try {
            await importLegacy(jobId, entitySlug, false);
          } catch {
            /* SharePoint may be empty or not configured; landing still proceeds */
          }
        }

        const dashboard = await fetchEntityDashboard(jobId, entitySlug);
        if (cancelled) return;

        if (dashboard.hasRows) {
          nav(`${base}/grid`, { replace: true });
        } else {
          nav(`${base}/uploads`, { replace: true });
        }
      } catch (e) {
        if (!cancelled) {
          if (isAuthRequiredError(e)) {
            setAuthRequired(true);
          } else {
            setError(e instanceof Error ? e.message : "Could not open workspace");
          }
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [jobId, entitySlug, importFromSharePoint, isAuthenticated, nav, base]);

  if (!isValidEntitySlug(entitySlug)) {
    return <Text>Unknown application.</Text>;
  }

  if (authRequired) {
    savePostLoginReturnPath(window.location.pathname, window.location.search);
    return <Navigate to="/" replace />;
  }

  if (error) {
    return (
      <MessageBar intent="error">
        <MessageBarBody>{error}</MessageBarBody>
      </MessageBar>
    );
  }

  return <Spinner label={importFromSharePoint ? "Importing from SharePoint…" : "Opening workspace…"} />;
}
