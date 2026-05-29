import {
  Body1,
  Button,
  Card,
  CardHeader,
  FluentProvider,
  makeStyles,
  tokens,
  webLightTheme,
} from "@fluentui/react-components";
import { useEffect, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { BrowserRouter, Link as RouterLink, Route, Routes, useNavigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { apiRequest, signInRequest } from "./authConfig";
import etcLogo from "./images/etc-logo.png";
import MultifamilyRoutes from "./multifamily-lbp/MultifamilyRoutes";
import { ApiAuthBridge } from "./multifamily-lbp/api/ApiAuthBridge";
import {
  parseJobIdFromReturnPath,
  readPostLoginReturnPath,
  POST_LOGIN_NAV_KEY,
} from "./multifamily-lbp/auth/jobEntryPaths";

type MeResponse = {
  name: string | null;
  email: string | null;
  objectId: string | null;
  tenantId: string | null;
};

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (failureCount, error) =>
        failureCount < 1 && !(error instanceof Error && error.name === "AuthRequiredError"),
    },
  },
});

/** After Entra redirect, return user to a multifamily deep link saved before login. */
function PostLoginRedirect(): null {
  const navigate = useNavigate();
  const { accounts } = useMsal();

  useEffect(() => {
    if (accounts.length === 0) return;
    const target = readPostLoginReturnPath();
    if (target) {
      sessionStorage.removeItem(POST_LOGIN_NAV_KEY);
      navigate(target, { replace: true });
    }
  }, [accounts.length, navigate]);

  return null;
}

function IntranetHome() {
  const styles = useStyles();
  const { instance, accounts } = useMsal();
  const [me, setMe] = useState<MeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const isSignedIn = accounts.length > 0;
  const account = accounts[0];
  const pendingReturnPath = readPostLoginReturnPath();
  const pendingJobId = parseJobIdFromReturnPath(pendingReturnPath);
  const displayName =
    me?.name && !me.name.includes("@")
      ? me.name
      : (account?.name ?? me?.name ?? "there");
  const displayEmail = me?.email ?? account?.username ?? null;

  async function loadData() {
    if (!isSignedIn) {
      setMe(null);
      return;
    }

    setError(null);

    try {
      const account = accounts[0];
      const tokenResponse = await instance.acquireTokenSilent({
        ...apiRequest,
        account,
      });

      const authHeaders = {
        Authorization: `Bearer ${tokenResponse.accessToken}`,
      };

      const meRes = await fetch("/api/me", { headers: authHeaders });

      if (!meRes.ok) {
        throw new Error("API request failed");
      }

      setMe(await meRes.json());
    } catch {
      setError(
        "Could not authenticate with the API. Check Entra app registrations and API scope configuration.",
      );
    }
  }

  useEffect(() => {
    void loadData();
  }, [isSignedIn]);

  return (
    <main className={styles.page}>
      <header className={styles.header}>
        <div className={styles.brandBar}>
          <img
            alt="Environmental Testing & Consulting"
            className={styles.logo}
            src={etcLogo}
          />
          <div className={styles.brandActions}>
            {!isSignedIn ? (
              <Button
                appearance="primary"
                onClick={() => {
                  if (pendingReturnPath) {
                    sessionStorage.setItem(POST_LOGIN_NAV_KEY, pendingReturnPath);
                  }
                  void instance.loginRedirect(signInRequest);
                }}
              >
                Sign in with Microsoft
              </Button>
            ) : (
              <Button
                appearance="outline"
                className={styles.signOutButton}
                onClick={() => void instance.logoutRedirect()}
              >
                Sign out
              </Button>
            )}
          </div>
        </div>
        {!isSignedIn ? (
          <Body1 className={styles.subtitle}>
            {pendingJobId
              ? `Continuing from SharePoint — sign in to open job ${pendingJobId} in the lead inspection workspace. Your upload is already saved.`
              : "Company intranet — sign in with your Microsoft work account."}
          </Body1>
        ) : (
          <Body1 className={styles.subtitle}>Welcome, {displayName}.</Body1>
        )}
      </header>

      {!isSignedIn && pendingJobId && (
        <Card>
          <CardHeader header={<strong>Lead inspection workspace</strong>} />
          <Body1>
            After you sign in, you will return to job <strong>{pendingJobId}</strong> to import
            SharePoint files, review readings, and generate reports.
          </Body1>
        </Card>
      )}

      {isSignedIn && (
        <Card>
          <CardHeader header={<strong>Applications</strong>} />
          <Body1>
            <RouterLink to="/lead-inspection">Lead inspection data manager</RouterLink>{" "}
            — multifamily LBP upload, grid, normalization, and reports.
          </Body1>
        </Card>
      )}

      {isSignedIn && (me || account) && (
        <Card>
          <CardHeader header={<strong>Signed-in user</strong>} />
          {error && <Body1 className={styles.error}>{error}</Body1>}
          <div className={styles.statusGrid}>
            <div className={styles.statusTile}>
              <Body1 className={styles.statusLabel}>Name</Body1>
              <Body1 className={styles.statusValue}>{displayName}</Body1>
            </div>
            <div className={styles.statusTile}>
              <Body1 className={styles.statusLabel}>Email</Body1>
              <Body1 className={styles.statusValue}>{displayEmail ?? "n/a"}</Body1>
            </div>
            <div className={styles.statusTile}>
              <Body1 className={styles.statusLabel}>Tenant</Body1>
              <Body1 className={styles.statusValue}>
                {me?.tenantId ?? account?.tenantId ?? "n/a"}
              </Body1>
            </div>
          </div>
        </Card>
      )}
    </main>
  );
}

export default function App() {
  return (
    <FluentProvider theme={webLightTheme}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <ApiAuthBridge>
            <PostLoginRedirect />
            <Routes>
              <Route path="/" element={<IntranetHome />} />
              <Route path="/*" element={<MultifamilyRoutes />} />
            </Routes>
          </ApiAuthBridge>
        </BrowserRouter>
      </QueryClientProvider>
    </FluentProvider>
  );
}

const useStyles = makeStyles({
  page: {
    margin: "0 auto",
    maxWidth: "980px",
    padding: "32px 20px 56px",
    display: "grid",
    rowGap: "16px",
  },
  header: {
    display: "grid",
    rowGap: "12px",
  },
  brandBar: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    flexWrap: "wrap",
    gap: "16px",
    padding: "14px 20px",
    backgroundColor: "#000000",
    borderRadius: tokens.borderRadiusLarge,
    boxShadow: tokens.shadow8,
  },
  logo: {
    display: "block",
    height: "48px",
    width: "auto",
    maxWidth: "min(100%, 300px)",
    objectFit: "contain",
  },
  brandActions: {
    display: "flex",
    alignItems: "center",
    flexShrink: 0,
  },
  signOutButton: {
    color: "#ffffff",
    borderTopColor: "rgba(255, 255, 255, 0.85)",
    borderRightColor: "rgba(255, 255, 255, 0.85)",
    borderBottomColor: "rgba(255, 255, 255, 0.85)",
    borderLeftColor: "rgba(255, 255, 255, 0.85)",
    ":hover": {
      color: "#000000",
      backgroundColor: "#ffffff",
      borderTopColor: "#ffffff",
      borderRightColor: "#ffffff",
      borderBottomColor: "#ffffff",
      borderLeftColor: "#ffffff",
    },
  },
  subtitle: {
    color: tokens.colorNeutralForeground2,
    paddingLeft: "4px",
  },
  statusGrid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(170px, 1fr))",
    gap: "12px",
  },
  statusTile: {
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    padding: "12px",
    display: "grid",
    rowGap: "6px",
  },
  statusLabel: {
    color: tokens.colorNeutralForeground3,
  },
  statusValue: {
    fontWeight: tokens.fontWeightSemibold,
  },
  error: {
    color: tokens.colorPaletteRedForeground1,
    marginBottom: "12px",
  },
});
