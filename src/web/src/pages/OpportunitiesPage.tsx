import {
  Badge,
  Body1,
  Button,
  Caption1,
  Drawer,
  DrawerBody,
  DrawerHeader,
  DrawerHeaderTitle,
  Link,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableCellLayout,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { Dismiss24Regular, Open24Regular } from "@fluentui/react-icons";
import { useEffect, useState } from "react";
import {
  CleatApiError,
  fetchOpportunity,
  fetchRecommendations,
  type Opportunity,
} from "../api/cleat";

const DEFAULT_MIN_SCORE = 80;

export function OpportunitiesPage() {
  const styles = useStyles();
  const [items, setItems] = useState<Opportunity[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<CleatApiError | Error | null>(null);
  const [selected, setSelected] = useState<Opportunity | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);
      try {
        const result = await fetchRecommendations(DEFAULT_MIN_SCORE);
        if (!cancelled) {
          setItems(result.items ?? []);
        }
      } catch (err) {
        if (!cancelled) {
          setItems([]);
          setError(err instanceof Error ? err : new Error("Unknown error"));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, []);

  async function openDetail(row: Opportunity) {
    setSelected(row);
    setDetailError(null);
    setDetailLoading(true);
    try {
      const detail = await fetchOpportunity(row.id);
      setSelected((current) =>
        current?.id === row.id ? { ...current, ...detail } : current,
      );
    } catch (err) {
      const message =
        err instanceof CleatApiError
          ? err.message
          : "Could not load opportunity detail from CLEATUS.";
      setDetailError(message);
    } finally {
      setDetailLoading(false);
    }
  }

  const missingKey = error instanceof CleatApiError && error.isMissingKey;
  const upstream = error && !missingKey;

  return (
    <main className={styles.page}>
      <header className={styles.header}>
        <Title1>Opportunities</Title1>
        <Body1 className={styles.subtitle}>
          Recommended SAM.gov and SLED bids from CLEATUS, scored against ETC&apos;s
          capture profile. This page loads on open (no webhooks) and does not
          store CLEATUS data locally.
        </Body1>
      </header>

      {loading && <Spinner label="Loading recommended opportunities..." />}

      {missingKey && (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>Add Cleat__ApiKey</MessageBarTitle>
            <div>
              {error.message} The intranet compiles and runs without a key; set
              it in user secrets locally or as an App Setting / Key Vault secret
              in Azure, then refresh this page.
            </div>
          </MessageBarBody>
        </MessageBar>
      )}

      {upstream && (
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>Could not load CLEATUS recommendations</MessageBarTitle>
            <div>{error.message}</div>
          </MessageBarBody>
        </MessageBar>
      )}

      {!loading && !error && items.length === 0 && (
        <MessageBar intent="info">
          <MessageBarBody>
            <MessageBarTitle>No recommendations</MessageBarTitle>
            <div>
              CLEATUS returned no opportunities at the default minimum score of{" "}
              {DEFAULT_MIN_SCORE}. Try a lower threshold later, or review the
              capture profile in CLEATUS.
            </div>
          </MessageBarBody>
        </MessageBar>
      )}

      {!loading && items.length > 0 && (
        <div className={styles.tableWrap}>
          <Table aria-label="Recommended opportunities">
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Title</TableHeaderCell>
                <TableHeaderCell>Agency</TableHeaderCell>
                <TableHeaderCell>Score</TableHeaderCell>
                <TableHeaderCell>Deadline</TableHeaderCell>
                <TableHeaderCell>NAICS / set-aside</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((item) => (
                <TableRow
                  key={item.id}
                  onClick={() => void openDetail(item)}
                  className={styles.clickableRow}
                >
                  <TableCell>
                    <TableCellLayout>
                      <div className={styles.titleCell}>
                        <span>{item.title ?? "Untitled opportunity"}</span>
                        {item.solicitationNumber && (
                          <Caption1 className={styles.muted}>
                            {item.solicitationNumber}
                          </Caption1>
                        )}
                      </div>
                    </TableCellLayout>
                  </TableCell>
                  <TableCell>{item.agency ?? "—"}</TableCell>
                  <TableCell>
                    {item.score == null ? (
                      "—"
                    ) : (
                      <Badge appearance="tint" color="brand">
                        {Math.round(item.score)}
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className={styles.titleCell}>
                      <span>{formatDate(item.deadlineDate)}</span>
                      {item.postedDate && (
                        <Caption1 className={styles.muted}>
                          Posted {formatDate(item.postedDate)}
                        </Caption1>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className={styles.titleCell}>
                      <span>{item.naics ?? "—"}</span>
                      {item.setAside && (
                        <Caption1 className={styles.muted}>{item.setAside}</Caption1>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Drawer
        type="overlay"
        position="end"
        open={selected !== null}
        onOpenChange={(_, data) => {
          if (!data.open) {
            setSelected(null);
          }
        }}
        size="medium"
      >
        <DrawerHeader>
          <DrawerHeaderTitle
            action={
              <Button
                appearance="subtle"
                aria-label="Close"
                icon={<Dismiss24Regular />}
                onClick={() => setSelected(null)}
              />
            }
          >
            {selected?.title ?? "Opportunity"}
          </DrawerHeaderTitle>
        </DrawerHeader>
        <DrawerBody>
          {selected && (
            <div className={styles.detail}>
              {detailLoading && <Spinner size="tiny" label="Loading detail..." />}
              {detailError && (
                <MessageBar intent="warning">
                  <MessageBarBody>
                    <div>Showing the list row only. {detailError}</div>
                  </MessageBarBody>
                </MessageBar>
              )}
              <DetailField label="Agency" value={selected.agency} />
              <DetailField label="Solicitation" value={selected.solicitationNumber} />
              <DetailField label="Score" value={formatScore(selected.score)} />
              <DetailField label="Posted" value={formatDate(selected.postedDate)} />
              <DetailField label="Deadline" value={formatDate(selected.deadlineDate)} />
              <DetailField label="NAICS" value={selected.naics} />
              <DetailField label="Set-aside" value={selected.setAside} />
              <DetailField label="Type" value={selected.opportunityType} />
              <DetailField label="Response type" value={selected.responseType} />
              <DetailField
                label="Place of performance"
                value={selected.placeOfPerformance}
              />
              <DetailField
                label="In pipeline"
                value={
                  selected.inPipeline == null
                    ? null
                    : selected.inPipeline
                      ? "Yes"
                      : "No"
                }
              />
              <DetailField label="Match reason" value={selected.matchReason} />
              <DetailField label="Overview" value={selected.overview} />
              <DetailField label="Summary" value={selected.summary} />
              <DetailField label="Description" value={selected.description} />

              <div className={styles.actions}>
                {selected.cleatusUrl && (
                  <Button
                    appearance="primary"
                    icon={<Open24Regular />}
                    onClick={() =>
                      window.open(selected.cleatusUrl!, "_blank", "noopener,noreferrer")
                    }
                  >
                    Open in CLEATUS
                  </Button>
                )}
                {selected.sourceUrl && (
                  <Link href={selected.sourceUrl} target="_blank" rel="noreferrer">
                    Original notice
                  </Link>
                )}
              </div>
            </div>
          )}
        </DrawerBody>
      </Drawer>
    </main>
  );
}

function DetailField({ label, value }: { label: string; value: string | null }) {
  const styles = useDetailStyles();
  if (!value) {
    return null;
  }

  return (
    <div className={styles.field}>
      <Caption1 className={styles.label}>{label}</Caption1>
      <Body1>{value}</Body1>
    </div>
  );
}

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return "—";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function formatScore(score: number | null): string | null {
  return score == null ? null : String(Math.round(score));
}

const useStyles = makeStyles({
  page: {
    margin: "0 auto",
    maxWidth: "1100px",
    padding: "32px 20px 56px",
    display: "grid",
    rowGap: "16px",
  },
  header: {
    display: "grid",
    rowGap: "8px",
  },
  subtitle: {
    color: tokens.colorNeutralForeground2,
  },
  tableWrap: {
    overflowX: "auto",
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusMedium,
    padding: "8px",
    boxShadow: tokens.shadow4,
  },
  clickableRow: {
    cursor: "pointer",
  },
  titleCell: {
    display: "grid",
    rowGap: "2px",
  },
  muted: {
    color: tokens.colorNeutralForeground3,
  },
  detail: {
    display: "grid",
    rowGap: "12px",
    paddingBottom: "24px",
  },
  actions: {
    display: "flex",
    gap: "12px",
    alignItems: "center",
    flexWrap: "wrap",
    marginTop: "8px",
  },
});

const useDetailStyles = makeStyles({
  field: {
    display: "grid",
    rowGap: "4px",
  },
  label: {
    color: tokens.colorNeutralForeground3,
    textTransform: "uppercase",
    letterSpacing: "0.04em",
  },
});
