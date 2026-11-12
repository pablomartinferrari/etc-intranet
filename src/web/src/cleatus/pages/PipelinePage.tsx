import { useEffect, useMemo, useState } from "react";
import { ExternalLinkIcon } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Spinner } from "@/components/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";
import { PageExplainer } from "../../sales/PageExplainer";
import {
  CleatApiError,
  CloseoutSyncError,
  closeOutPursuit,
  fetchPipeline,
  type PipelineDashboard,
  type PipelineItem,
} from "../api/cleat";
import {
  filterPipelineItems,
  pipelineFilterHeading,
} from "../pipelinePhase";

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

  const filterActive = phaseFilter !== "all";
  const filtered = useMemo(
    () => filterPipelineItems(items, phaseFilter),
    [items, phaseFilter],
  );
  const listHeading = pipelineFilterHeading(phaseFilter, filtered.length);

  function togglePhaseFilter(next: string) {
    setPhaseFilter((current) => (current === next ? "all" : next));
  }

  return (
    <main className="mx-auto grid w-full max-w-[1100px] gap-4 px-4 py-6 pb-24 md:px-5 md:py-8 md:pb-14">
      <PageExplainer title="Pipeline">
        <p>
          Pursuits from CLEATUS (triage / preparing / submitted / won / lost /
          archived). Needs close-out is overdue (past deadline, or no deadline on
          file).
        </p>
        <p>
          When someone closes won/lost/dropped, the reason is stored here in
          Postgres; CLEATUS only gets the board/archive change.
        </p>
      </PageExplainer>

      {loading && <Spinner label="Loading pipeline..." />}

      {missingKey && (
        <Alert>
          <AlertTitle>Add Cleat__ApiKey</AlertTitle>
          <AlertDescription>
            {error.message} The intranet compiles and runs without a key; set
            it in user secrets locally or as an App Setting / Key Vault secret
            in Azure, then refresh this page.
          </AlertDescription>
        </Alert>
      )}

      {upstream && (
        <Alert variant="destructive">
          <AlertTitle>Could not load CLEATUS pipeline</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      {dashboard && !dashboard.lastActivityFieldFound && (
        <Alert>
          <AlertTitle>No last-activity field from CLEATUS</AlertTitle>
          <AlertDescription>
            OpenAPI/Zapier do not document a pursuit updated-at. Stale 21-day
            detection is off; overdue uses deadline, and items with no
            deadline on file are listed under Needs close-out.
          </AlertDescription>
        </Alert>
      )}

      {dashboard && (
        <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4 xl:grid-cols-8">
          <CountTile
            label="Triage"
            value={dashboard.counts.triage}
            selected={phaseFilter === "triage"}
            onSelect={() => togglePhaseFilter("triage")}
          />
          <CountTile
            label="Preparing"
            value={dashboard.counts.preparing}
            selected={phaseFilter === "preparing"}
            onSelect={() => togglePhaseFilter("preparing")}
          />
          <CountTile
            label="Submitted"
            value={dashboard.counts.submitted}
            selected={phaseFilter === "submitted"}
            onSelect={() => togglePhaseFilter("submitted")}
          />
          <CountTile
            label="Won"
            value={dashboard.counts.won}
            selected={phaseFilter === "won"}
            onSelect={() => togglePhaseFilter("won")}
          />
          <CountTile
            label="Lost"
            value={dashboard.counts.lost}
            selected={phaseFilter === "lost"}
            onSelect={() => togglePhaseFilter("lost")}
          />
          <CountTile
            label="Archived"
            value={dashboard.counts.archived}
            selected={phaseFilter === "archived"}
            onSelect={() => togglePhaseFilter("archived")}
          />
          <CountTile
            label="Needs close-out"
            value={needs.length}
            selected={phaseFilter === "needs"}
            onSelect={() => togglePhaseFilter("needs")}
          />
          <CountTile
            label="Total"
            value={dashboard.counts.total}
            selected={phaseFilter === "all"}
            onSelect={() => setPhaseFilter("all")}
          />
        </div>
      )}

      {dashboard && showNeedsCloseOut && !filterActive && (
        <section className="grid gap-2.5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <strong>Needs close-out ({needs.length})</strong>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={showNeedsCloseOut}
                onCheckedChange={(checked) => setShowNeedsCloseOut(Boolean(checked))}
              />
              Show this section
            </label>
          </div>
          {needs.length === 0 ? (
            <p>Nothing currently needs close-out.</p>
          ) : (
            <PursuitTable
              items={needs}
              showAssignee={dashboard.assigneeFieldFound}
              onOpen={setSelected}
            />
          )}
        </section>
      )}

      {dashboard && !showNeedsCloseOut && !filterActive && (
        <label className="flex items-center gap-2 text-sm">
          <Checkbox
            checked={false}
            onCheckedChange={(checked) => setShowNeedsCloseOut(Boolean(checked))}
          />
          Show needs close-out
        </label>
      )}

      {dashboard && (
        <section className="grid gap-2.5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <strong aria-live="polite">{listHeading}</strong>
            <div className="flex flex-wrap items-center gap-2">
              {filterActive && (
                <Button type="button" size="sm" onClick={() => setPhaseFilter("all")}>
                  Show all
                </Button>
              )}
              <Select value={phaseFilter} onValueChange={setPhaseFilter}>
                <SelectTrigger className="w-full sm:w-[200px]" aria-label="Filter pursuits by phase">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All phases</SelectItem>
                  <SelectItem value="needs">Needs close-out</SelectItem>
                  <SelectItem value="triage">Triage</SelectItem>
                  <SelectItem value="preparing">Preparing</SelectItem>
                  <SelectItem value="submitted">Submitted</SelectItem>
                  <SelectItem value="won">Won</SelectItem>
                  <SelectItem value="lost">Lost</SelectItem>
                  <SelectItem value="archived">Archived</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          {filtered.length === 0 ? (
            <Alert>
              <AlertTitle>No pursuits</AlertTitle>
              <AlertDescription>
                {items.length === 0
                  ? "CLEATUS returned no pipeline items for this entity."
                  : "No pursuits match this filter."}
              </AlertDescription>
            </Alert>
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
  return (
    <div className="overflow-x-auto rounded-lg bg-card p-2 shadow-sm">
      <Table aria-label="Pursuits">
        <TableHeader>
          <TableRow>
            <TableHead>Title</TableHead>
            <TableHead>Agency</TableHead>
            <TableHead>Phase</TableHead>
            <TableHead>Deadline</TableHead>
            <TableHead>Status</TableHead>
            {showAssignee && <TableHead>Owner</TableHead>}
            <TableHead>Reason</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((item) => (
            <TableRow
              key={item.pursuit.id}
              className="cursor-pointer"
              onClick={() => onOpen(item)}
            >
              <TableCell>
                <div className="grid gap-0.5">
                  <span>{item.pursuit.title ?? "Untitled pursuit"}</span>
                  {item.pursuit.solicitationNumber && (
                    <span className="text-xs text-muted-foreground">
                      {item.pursuit.solicitationNumber}
                    </span>
                  )}
                </div>
              </TableCell>
              <TableCell>{item.pursuit.agency ?? "—"}</TableCell>
              <TableCell>{phaseDisplay(item)}</TableCell>
              <TableCell>{formatDate(item.pursuit.deadlineDate)}</TableCell>
              <TableCell>
                {item.needsCloseOut && (
                  <Badge variant="destructive">{badgeLabel(item.closeOutReasons)}</Badge>
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
    <Sheet
      open={item !== null}
      onOpenChange={(open) => {
        if (!open) onDismiss();
      }}
    >
      <SheetContent side="right" className="w-full sm:max-w-lg">
        <SheetHeader>
          <SheetTitle>{item?.pursuit.title ?? "Pursuit"}</SheetTitle>
        </SheetHeader>
        {item && (
          <div className="grid gap-3 overflow-y-auto px-4 pb-6">
            {syncWarning && (
              <Alert>
                <AlertDescription>{syncWarning}</AlertDescription>
              </Alert>
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

            <div className="grid gap-2">
              <Label>Close-out</Label>
              <RadioGroup
                value={outcome}
                onValueChange={(value) => {
                  setOutcome(value);
                  setReason("");
                }}
              >
                <label className="flex items-center gap-2 text-sm">
                  <RadioGroupItem value="won" />
                  Won
                </label>
                <label className="flex items-center gap-2 text-sm">
                  <RadioGroupItem value="lost" />
                  Lost
                </label>
                <label className="flex items-center gap-2 text-sm">
                  <RadioGroupItem value="dropped" />
                  No longer pursuing
                </label>
              </RadioGroup>
            </div>
            <div className="grid gap-2">
              <Label>{reasonRequired ? "Reason (required)" : "Reason (optional)"}</Label>
              <Select value={reason || undefined} onValueChange={setReason}>
                <SelectTrigger>
                  <SelectValue placeholder="Select a reason" />
                </SelectTrigger>
                <SelectContent>
                  {reasonOptions.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Note (optional)</Label>
              <Textarea
                value={note}
                onChange={(event) => setNote(event.target.value)}
                rows={4}
              />
            </div>
            {formError && <p className="text-sm text-destructive">{formError}</p>}
            <div className="mt-2 flex flex-wrap items-center gap-3">
              <Button disabled={saving} onClick={() => void submit()}>
                {saving ? "Saving..." : "Save close-out"}
              </Button>
              {item.pursuit.cleatusUrl && (
                <Button
                  variant="outline"
                  onClick={() =>
                    window.open(item.pursuit.cleatusUrl!, "_blank", "noopener,noreferrer")
                  }
                >
                  <ExternalLinkIcon />
                  Open in CLEATUS
                </Button>
              )}
            </div>
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}

function CountTile({
  label,
  value,
  selected,
  onSelect,
}: {
  label: string;
  value: number;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      aria-label={`${label}: ${value}. ${selected ? "Clear filter" : `Filter list to ${label}`}`}
      onClick={onSelect}
      className={cn(
        "grid gap-1 rounded-lg bg-card p-3 text-left shadow-sm transition",
        "hover:-translate-y-px hover:shadow-md",
        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
        selected && "ring-2 ring-ring ring-offset-2 ring-offset-background",
      )}
    >
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="font-semibold">{value}</span>
    </button>
  );
}

function DetailField({ label, value }: { label: string; value: string | null }) {
  if (!value) {
    return null;
  }
  return (
    <div className="grid gap-1">
      <p className="text-xs tracking-wide text-muted-foreground uppercase">{label}</p>
      <p className="text-sm">{value}</p>
    </div>
  );
}

function phaseDisplay(item: PipelineItem): string {
  if (item.pursuit.archived) {
    return "archived";
  }
  return item.pursuit.phase ?? item.pursuit.columnTitle ?? "—";
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
