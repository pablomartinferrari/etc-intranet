import {
  Badge,
  Body1,
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

export default function App() {
  const styles = useStyles();
  const [status, setStatus] = useState<ApiStatus | null>(null);
  const [messages, setMessages] = useState<SiteMessage[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [statusRes, messagesRes] = await Promise.all([
          fetch("/api/status"),
          fetch("/api/messages"),
        ]);

        if (!statusRes.ok || !messagesRes.ok) {
          throw new Error("API request failed");
        }

        setStatus(await statusRes.json());
        setMessages(await messagesRes.json());
      } catch {
        setError(
          "Could not reach the API. Start PostgreSQL (docker compose up) and run the API project.",
        );
      }
    }

    void load();
  }, []);

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
            React frontend, .NET 10 API, and PostgreSQL powered by Microsoft
            Fluent UI.
          </Body1>
        </header>

        <Card>
          <CardHeader header={<strong>API status</strong>} />
          {error && <Body1 className={styles.error}>{error}</Body1>}
          {!status && !error && <Spinner label="Loading API status..." />}
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
          {messages.length === 0 ? (
            <Body1>No messages yet.</Body1>
          ) : (
            <div className={styles.messages}>
              {messages.map((message) => (
                <article className={styles.messageItem} key={message.id}>
                  <strong>{message.title}</strong>
                  <Body1>{message.body}</Body1>
                </article>
              ))}
            </div>
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
