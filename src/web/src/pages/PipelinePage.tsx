import {
  Badge,
  Body1,
  Button,
  Caption1,
  Checkbox,
  Drawer,
  DrawerBody,
  DrawerHeader,
  DrawerHeaderTitle,
  Dropdown,
  Field,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Option,
  Radio,
  RadioGroup,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableCellLayout,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Textarea,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { Dismiss24Regular, Open24Regular } from "@fluentui/react-icons";
import { useEffect, useMemo, useState } from "react";
import {
  CleatApiError,
  CloseoutSyncError,
  closeOutPursuit,
  fetchPipeline,
  type PipelineDashboard,
  type PipelineItem,
} from "../api/cleat";

const LOST_REASONS = [
  { value: "price", label: "Price" },
  { value: "past_performance", label: "Past performance" },
  { value: "capacity", label: "Capacity" },
  { value: "missed_deadline", label: "Missed deadline" },
  { value: "out_of_naics_or_geo", label: "Out of NAICS / geography" },
  { value: "customer_cancelled", label: "Customer cancelled" },
  { value: "other", label: "Other" },
];

const WON_REASONS = [
  { value: "relationship", label: "Relationship" },
  { value: "price", label: "Price" },
  { value: "past_performance", label: "Past performance" },
  { value: "other", label: "Other" },
];

export function PipelinePage() {
  const styles = useStyles();
  const [dashboard, setDashboard] = useState<PipelineDashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<CleatApiError | Error | null>(null);
  const [selected, setSelected] = useState<PipelineItem | null>(null);
  const [phaseFilter, setPhaseFilter] = useState<string>("all");
  const [showNeedsCloseOut, setShowNeedsCloseOut] = useState(true);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const result = await fetchPipeline();
      setDashboard(result);
    } catch (err) {
      setDashboard(null);
      setError(err instanceof Error ? err : new Error("Unknown error"));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const missingKey = error instanceof CleatApiError && error.isMissingKey;
  const upstream = error && !missingKey;
  const items = dashboard?.items ?? [];
  const needs = dashboard?.needsCloseOut ?? [];

  const filtered = useMemo(() => {
    return items.filter((item) => {
      if (phaseFilter === "needs") {
        return item.needsCloseOut;
      }
      if (phaseFilter === "archived") {
        return item.pursuit.archived;
      }
      if (phaseFilter !== "all") {
        const stage = (item.pursuit.phase ?? item.pursuit.columnTitle ?? "").toLowerCase();
        if (stage !== phaseFilter) {
          return false;
        }
      }
      return true;
    });
  }, [items, phaseFilter]);

  return (
    <main className={styles.page}>
      <header className={styles.header}>
        <Title1>Pipeline</Title1>
        <Body1 className={styles.subtitle}>
          Pursuits from CLEATUS, loaded on page open. Close-out reasons (why we
          won, lost, or stopped) are stored in the intranet database — CLEATUS
          has no win/loss-reason field. Needs close-out: past deadline, or
          triage/preparing/submitted with no movement for 21 days when an
          updated-at exists. If CLEATUS does not send last activity, rows
          without a deadline are flagged so they do not hide.
        </Body1>
      </header>

      {loading && <Spinner label="Loading pipeline..." />}

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
            <MessageBarTitle>Could not load CLEATUS pipeline</MessageBarTitle>
            <div>{error.message}</div>
          </MessageBarBody>
        </MessageBar>
      )}

      {dashboard && !dashboard.lastActivityFieldFound && (
        <MessageBar intent="info">
          <MessageBarBody>
            <MessageBarTitle>No last-activity field from CLEATUS</MessageBarTitle>
            <div>
              OpenAPI/Zapier do not document a pursuit updated-at. Stale 21-day
              detection is off; overdue uses deadline, and items with no
              deadline on file are listed under Needs close-out.
            </div>
          </MessageBarBody>
        </MessageBar>
      )}

      {dashboard && (
        <div className={styles.counts}>
          <CountTile label="Triage" value={dashboard.counts.triage} />
          <CountTile label="Preparing" value={dashboard.counts.preparing} />
          <CountTile label="Submitted" value={dashboard.counts.submitted} />
          <CountTile label="Won" value={dashboard.counts.won} />
          <CountTile label="Lost" value={dashboard.counts.lost} />
          <CountTile label="Archived" value={dashboard.counts.archived} />
          <CountTile label="Total" value={dashboard.counts.total} />
        </div>
      )}

      {dashboard && showNeedsCloseOut && (
        <section className={styles.section}>
          <div className={styles.sectionHead}>
            <strong>Needs close-out ({needs.length})</strong>
            <Checkbox
              checked={showNeedsCloseOut}
              onChange={(_, data) => setShowNeedsCloseOut(Boolean(data.checked))}
              label="Show this section"
            />
          </div>
          {needs.length === 0 ? (
            <Body1>Nothing currently needs close-out.</Body1>
          ) : (
            <PursuitTable
              items={needs}
              showAssignee={dashboard.assigneeFieldFound}
              onOpen={setSelected}
            />
          )}
        </section>
      )}

      {dashboard && !showNeedsCloseOut && (
        <Checkbox
          checked={false}
          onChange={(_, data) => setShowNeedsCloseOut(Boolean(data.checked))}
          label="Show needs close-out"
        />
      )}

      {dashboard && (
        <section className={styles.section}>
          <div className={styles.sectionHead}>
            <strong>All pursuits</strong>
            <Dropdown
              value={phaseLabel(phaseFilter)}
              selectedOptions={[phaseFilter]}
              onOptionSelect={(_, data) => setPhaseFilter(data.optionValue ?? "all")}
            >
              <Option value="all">All phases</Option>
              <Option value="needs">Needs close-out</Option>
              <Option value="triage">Triage</Option>
              <Option value="preparing">Preparing</Option>
              <Option value="submitted">Submitted</Option>
              <Option value="won">Won</Option>
              <Option value="lost">Lost</Option>
              <Option value="archived">Archived</Option>
            </Dropdown>
          </div>
          {filtered.length === 0 ? (
            <MessageBar intent="info">
              <MessageBarBody>
                <MessageBarTitle>No pursuits</MessageBarTitle>
                <div>
                  {items.length === 0
                    ? "CLEATUS returned no pipeline items for this entity."
                    : "No pursuits match this filter."}
                </div>
              </MessageBarBody>
            </MessageBar>
          ) : (
            <PursuitTable
              items={filtered}
              showAssignee={dashboard.assigneeFieldFound}
              onOpen={setSelected}
            />
          )}
        </section>
      )}

      <CloseoutDrawer
        item={selected}
        onDismiss={() => setSelected(null)}
        onSaved={(updated) => {
          setSelected(updated);
          void load();
        }}
      />
    </main>
  );
}

function PursuitTable({
  items,
  showAssignee,
  onOpen,
}: {
  items: PipelineItem[];
  showAssignee: boolean;
  onOpen: (item: PipelineItem) => void;
}) {
  const styles = useStyles();
  return (
    <div className={styles.tableWrap}>
      <Table aria-label="Pursuits">
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Title</TableHeaderCell>
            <TableHeaderCell>Agency</TableHeaderCell>
            <TableHeaderCell>Phase</TableHeaderCell>
            <TableHeaderCell>Deadline</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            {showAssignee && <TableHeaderCell>Owner</TableHeaderCell>}
            <TableHeaderCell>Reason</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((item) => (
            <TableRow
              key={item.pursuit.id}
              className={styles.clickableRow}
              onClick={() => onOpen(item)}
            >
              <TableCell>
                <TableCellLayout>
                  <div className={styles.titleCell}>
                    <span>{item.pursuit.title ?? "Untitled pursuit"}</span>
                    {item.pursuit.solicitationNumber && (
                      <Caption1 className={styles.muted}>
                        {item.pursuit.solicitationNumber}
                      </Caption1>
                    )}
                  </div>
                </TableCellLayout>
              </TableCell>
              <TableCell>{item.pursuit.agency ?? "—"}</TableCell>
              <TableCell>{phaseDisplay(item)}</TableCell>
              <TableCell>{formatDate(item.pursuit.deadlineDate)}</TableCell>
              <TableCell>
                {item.needsCloseOut && (
                  <Badge appearance="filled" color="danger">
                    {badgeLabel(item.closeOutReasons)}
                  </Badge>
                )}
              </TableCell>
              {showAssignee && <TableCell>{item.pursuit.assignee ?? "—"}</TableCell>}
              <TableCell>
                {item.closeout
                  ? `${item.closeout.outcome}${item.closeout.reasonCode ? ` · ${item.closeout.reasonCode}` : ""}`
                  : "—"}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function CloseoutDrawer({
  item,
  onDismiss,
  onSaved,
}: {
  item: PipelineItem | null;
  onDismiss: () => void;
  onSaved: (item: PipelineItem) => void;
}) {
  const styles = useStyles();
  const [outcome, setOutcome] = useState("lost");
  const [reason, setReason] = useState<string>("");
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [syncWarning, setSyncWarning] = useState<string | null>(null);

  useEffect(() => {
    if (!item) {
      return;
    }
    setOutcome(item.closeout?.outcome ?? "lost");
    setReason(item.closeout?.reasonCode ?? "");
    setNote(item.closeout?.note ?? "");
    setFormError(null);
    setSyncWarning(
      item.closeout && !item.closeout.cleatusSyncedAt
        ? "A reason is saved here, but CLEATUS was not updated last time."
        : null,
    );
  }, [item]);

  const reasonOptions = outcome === "won" ? WON_REASONS : LOST_REASONS;
  const reasonRequired = outcome === "lost" || outcome === "dropped";

  async function submit() {
    if (!item) {
      return;
    }
    if (reasonRequired && !reason) {
      setFormError("Pick a reason for lost or no longer pursuing.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      const result = await closeOutPursuit(item.pursuit.id, {
        outcome,
        reasonCode: reason || undefined,
        note: note || undefined,
        opportunityId: item.pursuit.opportunityId ?? undefined,
      });
      setSyncWarning(null);
      onSaved({
        ...item,
        closeout: result.closeout,
        needsCloseOut: false,
        closeOutReasons: [],
        pursuit: {
          ...item.pursuit,
          archived: outcome === "dropped" ? true : item.pursuit.archived,
          phase:
            outcome === "won" ? "won" : outcome === "lost" ? "lost" : item.pursuit.phase,
        },
      });
    } catch (err) {
      if (err instanceof CloseoutSyncError) {
        setSyncWarning(
          `${err.message} The reason was saved on the intranet; CLEATUS was not updated.`,
        );
        onSaved({ ...item, closeout: err.closeout });
      } else {
        setFormError(err instanceof Error ? err.message : "Close-out failed.");
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <Drawer
      type="overlay"
      position="end"
      size="medium"
      open={item !== null}
      onOpenChange={(_, data) => {
        if (!data.open) {
          onDismiss();
        }
      }}
    >
      <DrawerHeader>
        <DrawerHeaderTitle
          action={
            <Button
              appearance="subtle"
              aria-label="Close"
              icon={<Dismiss24Regular />}
              onClick={onDismiss}
            />
          }
        >
          {item?.pursuit.title ?? "Pursuit"}
        </DrawerHeaderTitle>
      </DrawerHeader>
      <DrawerBody>
        {item && (
          <div className={styles.detail}>
            {syncWarning && (
              <MessageBar intent="warning">
                <MessageBarBody>
                  <div>{syncWarning}</div>
                </MessageBarBody>
              </MessageBar>
            )}
            <DetailField label="Agency" value={item.pursuit.agency} />
            <DetailField label="Phase" value={phaseDisplay(item)} />
            <DetailField label="Deadline" value={formatDate(item.pursuit.deadlineDate)} />
            <DetailField label="NAICS" value={item.pursuit.naics} />
            <DetailField label="Set-aside" value={item.pursuit.setAside} />
            <DetailField label="Owner" value={item.pursuit.assignee} />
            <DetailField
              label="Needs close-out"
              value={item.needsCloseOut ? item.closeOutReasons.join(", ") : "No"}
            />
            <DetailField label="Overview" value={item.pursuit.overview} />
            <DetailField label="Summary" value={item.pursuit.summary} />
            <DetailField label="Description" value={item.pursuit.description} />
            {item.closeout && (
              <DetailField
                label="Stored reason"
                value={`${item.closeout.outcome}${item.closeout.reasonCode ? ` · ${item.closeout.reasonCode}` : ""}${item.closeout.note ? ` — ${item.closeout.note}` : ""}`}
              />
            )}

            <Field label="Close-out">
              <RadioGroup
                value={outcome}
                onChange={(_, data) => {
                  setOutcome(data.value);
                  setReason("");
                }}
              >
                <Radio value="won" label="Won" />
                <Radio value="lost" label="Lost" />
                <Radio value="dropped" label="No longer pursuing" />
              </RadioGroup>
            </Field>
            <Field
              label={reasonRequired ? "Reason (required)" : "Reason (optional)"}
            >
              <Dropdown
                placeholder="Select a reason"
                selectedOptions={reason ? [reason] : []}
                value={reasonLabel(reason, reasonOptions)}
                onOptionSelect={(_, data) => setReason(data.optionValue ?? "")}
              >
                {reasonOptions.map((option) => (
                  <Option key={option.value} value={option.value}>
                    {option.label}
                  </Option>
                ))}
              </Dropdown>
            </Field>
            <Field label="Note (optional)">
              <Textarea
                value={note}
                onChange={(_, data) => setNote(data.value)}
                rows={4}
              />
            </Field>
            {formError && (
              <Body1 className={styles.error}>{formError}</Body1>
            )}
            <div className={styles.actions}>
              <Button appearance="primary" disabled={saving} onClick={() => void submit()}>
                {saving ? "Saving..." : "Save close-out"}
              </Button>
              {item.pursuit.cleatusUrl && (
                <Button
                  icon={<Open24Regular />}
                  onClick={() =>
                    window.open(item.pursuit.cleatusUrl!, "_blank", "noopener,noreferrer")
                  }
                >
                  Open in CLEATUS
                </Button>
              )}
            </div>
          </div>
        )}
      </DrawerBody>
    </Drawer>
  );
}

function CountTile({ label, value }: { label: string; value: number }) {
  const styles = useStyles();
  return (
    <div className={styles.countTile}>
      <Caption1 className={styles.muted}>{label}</Caption1>
      <Body1 className={styles.countValue}>{value}</Body1>
    </div>
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

function phaseDisplay(item: PipelineItem): string {
  if (item.pursuit.archived) {
    return "archived";
  }
  return item.pursuit.phase ?? item.pursuit.columnTitle ?? "—";
}

function phaseLabel(value: string): string {
  switch (value) {
    case "all":
      return "All phases";
    case "needs":
      return "Needs close-out";
    default:
      return value.charAt(0).toUpperCase() + value.slice(1);
  }
}

function badgeLabel(reasons: string[]): string {
  if (reasons.includes("deadline_passed")) {
    return "Overdue";
  }
  if (reasons.includes("stale_21_days")) {
    return "Stale";
  }
  if (reasons.includes("no_deadline_on_file")) {
    return "No deadline";
  }
  return "Needs close-out";
}

function reasonLabel(
  value: string,
  options: { value: string; label: string }[],
): string {
  return options.find((option) => option.value === value)?.label ?? "";
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
  counts: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(110px, 1fr))",
    gap: "10px",
  },
  countTile: {
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusMedium,
    padding: "12px",
    boxShadow: tokens.shadow2,
    display: "grid",
    rowGap: "4px",
  },
  countValue: {
    fontWeight: tokens.fontWeightSemibold,
  },
  section: {
    display: "grid",
    rowGap: "10px",
  },
  sectionHead: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    gap: "12px",
    flexWrap: "wrap",
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
  error: {
    color: tokens.colorPaletteRedForeground1,
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
