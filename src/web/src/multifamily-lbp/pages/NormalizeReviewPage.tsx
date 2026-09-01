import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useSearchParams } from "react-router-dom";
import { CheckIcon, RefreshCwIcon, XIcon } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useEntity } from "@mf/context/EntityContext";
import {
  fetchNormalizations,
  formatOriginalDisplay,
  patchNormalization,
  type NormalizationSuggestion,
} from "@mf/api/entity";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";

function statusBadge(status: string): React.JSX.Element {
  switch (status) {
    case "rejected":
      return <Badge variant="secondary">Rejected</Badge>;
    case "edited":
      return <Badge variant="outline">Edited</Badge>;
    case "applied":
      return <Badge>Applied</Badge>;
    default:
      return <Badge variant="outline">Pending</Badge>;
  }
}

function confidenceBadge(confidence: string): React.JSX.Element {
  if (confidence === "high") {
    return <Badge>High</Badge>;
  }
  if (confidence === "medium") {
    return <Badge variant="secondary">Medium</Badge>;
  }
  return <Badge variant="outline">{confidence}</Badge>;
}

function fieldLabel(fieldName: string): string {
  return fieldName === "substrate" ? "Substrate" : "Component";
}

function fieldBadge(fieldName: string): React.JSX.Element {
  return <Badge variant="outline">{fieldLabel(fieldName)}</Badge>;
}

function effectiveValue(s: NormalizationSuggestion): string {
  return s.approvedValue ?? s.suggestedValue;
}

function hasUnsavedEdit(s: NormalizationSuggestion, pendingEdits: Record<string, string>): boolean {
  const pending = pendingEdits[s.id];
  return pending !== undefined && pending.trim() !== effectiveValue(s).trim();
}

export function NormalizeReviewPage(): React.JSX.Element {
  const tableStyles = useDataTableStyles();
  const { jobId, entitySlug, refetchDashboard } = useEntity();
  const nav = useNavigate();
  const qc = useQueryClient();
  const [searchParams] = useSearchParams();
  const [pendingEdits, setPendingEdits] = useState<Record<string, string>>({});

  const runFields = useMemo(() => {
    const raw = searchParams.get("fields");
    if (!raw) return ["component", "substrate"];
    const parsed = raw
      .split(",")
      .map((f) => f.trim().toLowerCase())
      .filter((f) => f === "component" || f === "substrate");
    return parsed.length > 0 ? parsed : ["component", "substrate"];
  }, [searchParams]);

  const autoAppliedCount = Number.parseInt(searchParams.get("autoApplied") ?? "0", 10) || 0;
  const fieldsKey = runFields.join(",");

  const { data: items = [], isLoading } = useQuery({
    queryKey: ["normalizations", jobId, entitySlug, fieldsKey],
    queryFn: () => fetchNormalizations(jobId, entitySlug, undefined, runFields),
  });

  const reviewItems = useMemo(
    () => items.filter((i) => i.status === "pending" || i.status === "edited"),
    [items]
  );
  const hasPendingReview = reviewItems.some((i) => i.status === "pending");

  const invalidateAfterSave = (): void => {
    void qc.invalidateQueries({ queryKey: ["normalizations", jobId, entitySlug, fieldsKey] });
    void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
    refetchDashboard();
  };

  const patchMut = useMutation({
    mutationFn: ({ id, status, value }: { id: string; status: string; value?: string }) =>
      patchNormalization(jobId, entitySlug, id, status, value),
    onSuccess: invalidateAfterSave,
  });

  const resolveValue = (s: NormalizationSuggestion): string =>
    (pendingEdits[s.id] ?? effectiveValue(s)).trim();

  const approveSuggestion = (s: NormalizationSuggestion): void => {
    setPendingEdits((prev) => {
      const next = { ...prev };
      delete next[s.id];
      return next;
    });
    patchMut.mutate({ id: s.id, status: "approved", value: resolveValue(s) });
  };

  const updateSuggestion = (s: NormalizationSuggestion): void => {
    setPendingEdits((prev) => {
      const next = { ...prev };
      delete next[s.id];
      return next;
    });
    patchMut.mutate({ id: s.id, status: "approved", value: resolveValue(s) });
  };

  const approveHigh = (): void => {
    reviewItems
      .filter((i) => i.confidence === "high" && i.status === "pending")
      .forEach((i) => approveSuggestion(i));
  };

  const rowClass = (status: string): string | undefined => {
    if (status === "edited" || status === "applied") return "bg-green-50";
    if (status === "rejected") return "bg-muted opacity-85";
    return tableStyles.zebra;
  };

  return (
    <div>
      <h1 className="mb-2 text-2xl font-semibold tracking-tight">Review AI suggestions</h1>
      <p className="mb-4 text-muted-foreground">
        Approve a suggestion to save it to the grid immediately. Change a normalized value after it has been
        applied, then click Update to save again. Similar spellings (for example singular and plural) appear as
        one row in Original.
      </p>

      {autoAppliedCount > 0 && (
        <Alert className="mb-4">
          <AlertDescription>
            {autoAppliedCount} exact match{autoAppliedCount === 1 ? "" : "es"} applied automatically. Edit and
            click Update if you want to change any of them.
          </AlertDescription>
        </Alert>
      )}

      {items.length > 0 && (
        <div className="mb-4 flex flex-wrap gap-4">
          {hasPendingReview && (
            <Button variant="secondary" disabled={patchMut.isPending} onClick={approveHigh}>
              Approve all high confidence
            </Button>
          )}
          <Button onClick={() => nav(`/jobs/${jobId}/${entitySlug}/grid/groups`)}>
            Continue to grouped readings
          </Button>
        </div>
      )}

      {isLoading ? (
        <Spinner label="Loading suggestions…" />
      ) : items.length === 0 ? (
        <Alert>
          <AlertDescription>
            No normalization results for this run. Run normalization from setup, or try a different scope.
          </AlertDescription>
        </Alert>
      ) : (
        <DataTablePanel>
          <Table className={tableStyles.table} aria-label="Normalization suggestions">
            <TableHeader className={tableStyles.stickyHead}>
              <TableRow>
                {["Field", "Original", "Normalized value", "Rows", "Confidence", "Status", "Actions"].map((h) => (
                  <TableHead key={h} className={tableStyles.headCell}>
                    {h}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((s) => {
                const isRejected = s.status === "rejected";
                const unsaved = hasUnsavedEdit(s, pendingEdits);
                const showApprove =
                  !isRejected &&
                  (s.status === "pending" || s.status === "edited" || s.status === "approved");
                const showUpdate = !isRejected && s.status === "applied" && unsaved;
                const inputValue = pendingEdits[s.id] ?? effectiveValue(s);

                return (
                  <TableRow key={s.id} className={rowClass(s.status)}>
                    <TableCell className={tableStyles.bodyCell}>{fieldBadge(s.fieldName)}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>{formatOriginalDisplay(s.originalValue)}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>
                      <Input
                        className="h-7"
                        value={inputValue}
                        disabled={isRejected || patchMut.isPending}
                        onChange={(e) =>
                          setPendingEdits((prev) => ({ ...prev, [s.id]: e.target.value }))
                        }
                      />
                    </TableCell>
                    <TableCell className={tableStyles.bodyCell}>{s.affectedRowCount}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>{confidenceBadge(s.confidence)}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>{statusBadge(s.status)}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>
                      {showApprove || showUpdate ? (
                        <div className="inline-flex flex-wrap gap-1">
                          {showApprove && (
                            <Button
                              size="sm"
                              disabled={patchMut.isPending}
                              onClick={() => approveSuggestion(s)}
                            >
                              <CheckIcon />
                              Approve
                            </Button>
                          )}
                          {showUpdate && (
                            <Button
                              size="sm"
                              disabled={patchMut.isPending}
                              onClick={() => updateSuggestion(s)}
                            >
                              <RefreshCwIcon />
                              Update
                            </Button>
                          )}
                          <Button
                            variant="secondary"
                            size="sm"
                            disabled={isRejected || patchMut.isPending}
                            onClick={() => patchMut.mutate({ id: s.id, status: "rejected" })}
                          >
                            <XIcon />
                            Reject
                          </Button>
                        </div>
                      ) : (
                        <span className="text-xs text-muted-foreground">
                          {isRejected ? "Rejected" : "Applied to grid"}
                        </span>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </DataTablePanel>
      )}
    </div>
  );
}
