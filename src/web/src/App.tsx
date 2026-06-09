import {
  Body1,
  Button,
  Caption1,
  Card,
  CardHeader,
  FluentProvider,
  Subtitle1,
  Title1,
  Title3,
  makeStyles,
  tokens,
  webLightTheme,
} from "@fluentui/react-components";
import {
  ArrowRight24Regular,
  ChatSparkle24Regular,
  ClipboardTaskListLtr24Regular,
} from "@fluentui/react-icons";
import { useEffect, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { BrowserRouter, Link as RouterLink, Route, Routes, useNavigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { apiRequest, signInRequest } from "./authConfig";
import etcLogo from "./images/etc-logo.png";
import MultifamilyRoutes from "./multifamily-lbp/MultifamilyRoutes";
import KnowledgeRoutes from "./knowledge-base/KnowledgeRoutes";
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

const INTRANET_APPS = [
  {
    to: "/lead-inspection",
    title: "Lead inspection data manager",
    description: "Upload XRF readings, review the grid, normalize components, and generate reports.",
    Icon: ClipboardTaskListLtr24Regular,
    accent: tokens.colorPaletteBlueBorderActive,
  },
  {
    to: "/knowledge",
    title: "Knowledge assistant",
    description: "Organize project files, search your library, and chat with citations or web results.",
    Icon: ChatSparkle24Regular,
    accent: tokens.colorPaletteTealBorderActive,
  },
] as const;

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
  const firstName = displayName.split(/\s+/)[0] ?? displayName;
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
          <div className={styles.hero}>
            <Title1 className={styles.heroTitle}>ETC intranet</Title1>
            <Body1 className={styles.heroLead}>
              {pendingJobId
                ? `Sign in to continue to job ${pendingJobId} in the lead inspection workspace.`
                : "Sign in with your Microsoft work account to open company applications."}
            </Body1>
          </div>
        ) : (
          <div className={styles.hero}>
            <Caption1 className={styles.heroEyebrow}>Environmental Testing & Consulting</Caption1>
            <Title1 className={styles.heroTitle}>Welcome back, {firstName}</Title1>
            {displayEmail && (
              <Caption1 className={styles.heroMeta}>{displayEmail}</Caption1>
            )}
            {error && <Body1 className={styles.error}>{error}</Body1>}
          </div>
        )}
      </header>

      {!isSignedIn && pendingJobId && (
        <Card className={styles.noticeCard}>
          <CardHeader header={<Title3>Lead inspection workspace</Title3>} />
          <Body1 className={styles.noticeBody}>
            After you sign in, you will return to job <strong>{pendingJobId}</strong> to import
            SharePoint files, review readings, and generate reports.
          </Body1>
        </Card>
      )}

      {isSignedIn && (
        <section className={styles.appsSection}>
          <Subtitle1 className={styles.appsHeading}>Applications</Subtitle1>
          <div className={styles.appGrid}>
            {INTRANET_APPS.map((app) => (
              <RouterLink key={app.to} to={app.to} className={styles.appCard}>
                <div
                  className={styles.appIconWrap}
                  style={{ borderColor: app.accent }}
                >
                  <app.Icon className={styles.appIcon} />
                </div>
                <div className={styles.appCopy}>
                  <Subtitle1 className={styles.appTitle}>{app.title}</Subtitle1>
                  <Caption1 className={styles.appDescription}>{app.description}</Caption1>
                </div>
                <ArrowRight24Regular className={styles.appArrow} aria-hidden />
              </RouterLink>
            ))}
          </div>
        </section>
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
              <Route path="/knowledge/*" element={<KnowledgeRoutes />} />
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
    maxWidth: "960px",
    minHeight: "100vh",
    padding: "28px 24px 64px",
    display: "flex",
    flexDirection: "column",
    gap: "32px",
    backgroundColor: tokens.colorNeutralBackground2,
  },
  header: {
    display: "flex",
    flexDirection: "column",
    gap: "28px",
  },
  brandBar: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    flexWrap: "wrap",
    gap: "16px",
    padding: "16px 24px",
    backgroundColor: "#000000",
    borderRadius: tokens.borderRadiusXLarge,
    boxShadow: tokens.shadow16,
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
  hero: {
    padding: "8px 4px 0",
    display: "flex",
    flexDirection: "column",
    gap: "8px",
  },
  heroEyebrow: {
    color: tokens.colorNeutralForeground3,
    letterSpacing: "0.06em",
    textTransform: "uppercase",
    fontWeight: tokens.fontWeightSemibold,
  },
  heroTitle: {
    fontWeight: tokens.fontWeightSemibold,
    letterSpacing: "-0.02em",
    lineHeight: tokens.lineHeightHero800,
  },
  heroLead: {
    maxWidth: "560px",
    color: tokens.colorNeutralForeground2,
    fontSize: tokens.fontSizeBase400,
    lineHeight: tokens.lineHeightBase400,
  },
  heroMeta: {
    color: tokens.colorNeutralForeground3,
    marginTop: "2px",
  },
  noticeCard: {
    borderRadius: tokens.borderRadiusXLarge,
    boxShadow: tokens.shadow4,
  },
  noticeBody: {
    padding: "0 16px 16px",
    color: tokens.colorNeutralForeground2,
  },
  appsSection: {
    display: "flex",
    flexDirection: "column",
    gap: "16px",
  },
  appsHeading: {
    paddingLeft: "4px",
    color: tokens.colorNeutralForeground2,
    fontWeight: tokens.fontWeightSemibold,
  },
  appGrid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
    gap: "16px",
  },
  appCard: {
    display: "flex",
    alignItems: "center",
    gap: "16px",
    padding: "20px",
    borderRadius: tokens.borderRadiusXLarge,
    backgroundColor: tokens.colorNeutralBackground1,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    boxShadow: tokens.shadow2,
    textDecoration: "none",
    color: "inherit",
    transitionProperty: "transform, box-shadow, border-color",
    transitionDuration: "160ms",
    transitionTimingFunction: "ease-out",
    ":hover": {
      transform: "translateY(-2px)",
      boxShadow: tokens.shadow8,
      borderTopColor: tokens.colorNeutralStroke1,
      borderRightColor: tokens.colorNeutralStroke1,
      borderBottomColor: tokens.colorNeutralStroke1,
      borderLeftColor: tokens.colorNeutralStroke1,
    },
    ":focus-visible": {
      outline: `2px solid ${tokens.colorBrandStroke1}`,
      outlineOffset: "2px",
    },
  },
  appIconWrap: {
    flexShrink: 0,
    width: "48px",
    height: "48px",
    borderRadius: tokens.borderRadiusLarge,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: tokens.colorNeutralBackground2,
    border: "1px solid",
  },
  appIcon: {
    width: "24px",
    height: "24px",
    color: tokens.colorNeutralForeground1,
  },
  appCopy: {
    flex: 1,
    minWidth: 0,
    display: "flex",
    flexDirection: "column",
    gap: "6px",
  },
  appTitle: {
    fontWeight: tokens.fontWeightSemibold,
    lineHeight: tokens.lineHeightBase300,
  },
  appDescription: {
    color: tokens.colorNeutralForeground3,
    lineHeight: tokens.lineHeightBase200,
  },
  appArrow: {
    flexShrink: 0,
    color: tokens.colorNeutralForeground3,
  },
  error: {
    color: tokens.colorPaletteRedForeground1,
    marginTop: "8px",
  },
});
