import {
  Badge,
  Body1,
  Button,
  Card,
  CardHeader,
  FluentProvider,
  Spinner,
  Title1,
  makeStyles,
  tokens,
  webLightTheme,
} from "@fluentui/react-components";
import { BuildingBank24Regular } from "@fluentui/react-icons";
import { useEffect, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { apiRequest, loginRequest } from "./authConfig";

type ApiStatus = {
  service: string;
  database: string;
  messageCount: number;
  timestamp: string;
};

type SiteMessage = {
  id: number;
  title: string;
  body: string;
  createdAt: string;
};

type MeResponse = {
  name: string | null;
  email: string | null;
  objectId: string | null;
  tenantId: string | null;
};

export default function App() {
  const styles = useStyles();
  const { instance, accounts } = useMsal();
  const [me, setMe] = useState<MeResponse | null>(null);
  const [status, setStatus] = useState<ApiStatus | null>(null);
  const [messages, setMessages] = useState<SiteMessage[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const isSignedIn = accounts.length > 0;

  async function loadData() {
    if (!isSignedIn) {
      setStatus(null);
      setMessages([]);
      setMe(null);
      return;
    }

    setIsLoading(true);
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

      const [statusRes, messagesRes, meRes] = await Promise.all([
        fetch("/api/status", { headers: authHeaders }),
        fetch("/api/messages", { headers: authHeaders }),
        fetch("/api/me", { headers: authHeaders }),
      ]);

      if (!statusRes.ok || !messagesRes.ok || !meRes.ok) {
        throw new Error("API request failed");
      }

      setStatus(await statusRes.json());
      setMessages(await messagesRes.json());
      setMe(await meRes.json());
    } catch {
      setError(
        "Could not authenticate with the API. Check Entra app registrations and API scope configuration.",
      );
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void loadData();
  }, [isSignedIn]);

  return (
    <FluentProvider theme={webLightTheme}>
      <main className={styles.page}>
        <header className={styles.header}>
          <div className={styles.headerRow}>
            <BuildingBank24Regular />
            <Badge appearance="filled">ETC Intranet</Badge>
          </div>
          <Title1>Welcome to ETC</Title1>
          <Body1 className={styles.subtitle}>
            Single sign-on ready intranet with Microsoft Entra ID.
          </Body1>
          <div className={styles.actions}>
            {!isSignedIn ? (
              <Button
                appearance="primary"
                onClick={() => void instance.loginRedirect(loginRequest)}
              >
                Sign in with Microsoft
              </Button>
            ) : (
              <Button appearance="secondary" onClick={() => void instance.logoutRedirect()}>
                Sign out
              </Button>
            )}
          </div>
        </header>

        {isSignedIn && me && (
          <Card>
            <CardHeader header={<strong>Signed-in user</strong>} />
            <div className={styles.statusGrid}>
              <div className={styles.statusTile}>
                <Body1 className={styles.statusLabel}>Name</Body1>
                <Body1 className={styles.statusValue}>{me.name ?? "n/a"}</Body1>
              </div>
              <div className={styles.statusTile}>
                <Body1 className={styles.statusLabel}>Email</Body1>
                <Body1 className={styles.statusValue}>{me.email ?? "n/a"}</Body1>
              </div>
              <div className={styles.statusTile}>
                <Body1 className={styles.statusLabel}>Tenant</Body1>
                <Body1 className={styles.statusValue}>{me.tenantId ?? "n/a"}</Body1>
              </div>
            </div>
          </Card>
        )}

        <Card>
          <CardHeader header={<strong>API status</strong>} />
          {error && <Body1 className={styles.error}>{error}</Body1>}
          {!isSignedIn && (
            <Body1 className={styles.subtitle}>
              Sign in to load API data and user context from the tenant.
            </Body1>
          )}
          {isLoading && <Spinner label="Loading API status..." />}
          {status && (
            <div className={styles.statusGrid}>
              <div className={styles.statusTile}>
                <Body1 className={styles.statusLabel}>Service</Body1>
                <Body1 className={styles.statusValue}>{status.service}</Body1>
              </div>
              <div className={styles.statusTile}>
                <Body1 className={styles.statusLabel}>Database</Body1>
                <Body1 className={styles.statusValue}>{status.database}</Body1>
              </div>
              <div className={styles.statusTile}>
                <Body1 className={styles.statusLabel}>Messages</Body1>
                <Body1 className={styles.statusValue}>{status.messageCount}</Body1>
              </div>
            </div>
          )}
        </Card>

        <Card>
          <CardHeader header={<strong>Latest messages</strong>} />
          {isSignedIn && messages.length === 0 ? (
            <Body1>No messages yet.</Body1>
          ) : isSignedIn ? (
            <div className={styles.messages}>
              {messages.map((message) => (
                <article className={styles.messageItem} key={message.id}>
                  <strong>{message.title}</strong>
                  <Body1>{message.body}</Body1>
                </article>
              ))}
            </div>
          ) : (
            <Body1 className={styles.subtitle}>Sign in to view messages.</Body1>
          )}
        </Card>
      </main>
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
    rowGap: "8px",
  },
  actions: {
    marginTop: "4px",
  },
  headerRow: {
    display: "flex",
    alignItems: "center",
    gap: "10px",
    color: tokens.colorBrandForeground1,
  },
  subtitle: {
    color: tokens.colorNeutralForeground2,
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
  messages: {
    display: "grid",
    rowGap: "10px",
  },
  messageItem: {
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    padding: "12px",
    display: "grid",
    rowGap: "4px",
  },
  error: {
    color: tokens.colorPaletteRedForeground1,
  },
});
