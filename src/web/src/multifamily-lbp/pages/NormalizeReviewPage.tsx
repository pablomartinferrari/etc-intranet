import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useSearchParams } from "react-router-dom";
import {
  Badge,
  Button,
  Input,
  MessageBar,
  MessageBarBody,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { ArrowSyncRegular, CheckmarkRegular, DismissRegular } from "@fluentui/react-icons";
import { useEntity } from "@mf/context/EntityContext";
import {
  fetchNormalizations,
  formatOriginalDisplay,
  patchNormalization,
  type NormalizationSuggestion,
} from "@mf/api/entity";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";

const useStyles = makeStyles({
  toolbar: {
    display: "flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
  actionGroup: {
    display: "inline-flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalXS,
  },
  rowApproved: {
    backgroundColor: tokens.colorPaletteGreenBackground1,
  },
  rowRejected: {
    backgroundColor: tokens.colorNeutralBackground3,
    opacity: 0.85,
  },
});

function statusBadge(status: string): React.JSX.Element {
  switch (status) {
    case "rejected":
      return (
        <Badge appearance="filled" color="subtle">
          Rejected
        </Badge>
      );
    case "edited":
      return (
        <Badge appearance="filled" color="informative">
          Edited
        </Badge>
      );
    case "applied":
      return (
        <Badge appearance="filled" color="success">
          Applied
        </Badge>
      );
    default:
      return (
        <Badge appearance="outline" color="warning">
          Pending
        </Badge>
      );
  }
}

function confidenceBadge(confidence: string): React.JSX.Element {
  if (confidence === "high") {
    return (
      <Badge appearance="filled" color="success">
        High
      </Badge>
    );
  }
  if (confidence === "medium") {
    return (
      <Badge appearance="filled" color="warning">
        Medium
      </Badge>
    );
  }
  return (
    <Badge appearance="outline" color="subtle">
      {confidence}
    </Badge>
  );
}

function fieldLabel(fieldName: string): string {
  return fieldName === "substrate" ? "Substrate" : "Component";
}

function fieldBadge(fieldName: string): React.JSX.Element {
  return (
    <Badge appearance="outline" color={fieldName === "substrate" ? "brand" : "informative"}>
      {fieldLabel(fieldName)}
    </Badge>
  );
}

function effectiveValue(s: NormalizationSuggestion): string {
  return s.approvedValue ?? s.suggestedValue;
}

function hasUnsavedEdit(s: NormalizationSuggestion, pendingEdits: Record<string, string>): boolean {
  const pending = pendingEdits[s.id];
  return pending !== undefined && pending.trim() !== effectiveValue(s).trim();
}

export function NormalizeReviewPage(): React.JSX.Element {
  const styles = useStyles();
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
    if (status === "edited" || status === "applied") return styles.rowApproved;
    if (status === "rejected") return styles.rowRejected;
    return tableStyles.zebra;
  };

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalS }}>
        Review AI suggestions
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM, color: tokens.colorNeutralForeground2 }}>
        Approve a suggestion to save it to the grid immediately. Change a normalized value after it has been
        applied, then click Update to save again. Similar spellings (for example singular and plural) appear as
        one row in Original.
      </Text>

      {autoAppliedCount > 0 && (
        <MessageBar intent="success" style={{ marginBottom: tokens.spacingVerticalM }}>
          <MessageBarBody>
            {autoAppliedCount} exact match{autoAppliedCount === 1 ? "" : "es"} applied automatically. Edit and
            click Update if you want to change any of them.
          </MessageBarBody>
        </MessageBar>
      )}

      {items.length > 0 && (
        <div className={styles.toolbar}>
          {hasPendingReview && (
            <Button appearance="secondary" disabled={patchMut.isPending} onClick={approveHigh}>
              Approve all high confidence
            </Button>
          )}
          <Button appearance="primary" onClick={() => nav(`/jobs/${jobId}/${entitySlug}/grid/groups`)}>
            Continue to grouped readings
          </Button>
        </div>
      )}

      {isLoading ? (
        <Spinner label="Loading suggestions…" />
      ) : items.length === 0 ? (
        <MessageBar intent="info">
          <MessageBarBody>
            No normalization results for this run. Run normalization from setup, or try a different scope.
          </MessageBarBody>
        </MessageBar>
      ) : (
        <DataTablePanel>
          <Table className={tableStyles.table} aria-label="Normalization suggestions">
            <TableHeader className={tableStyles.stickyHead}>
              <TableRow>
                {["Field", "Original", "Normalized value", "Rows", "Confidence", "Status", "Actions"].map((h) => (
                  <TableHeaderCell key={h} className={tableStyles.headCell}>
                    {h}
                  </TableHeaderCell>
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
                        size="small"
                        appearance="filled-darker"
                        value={inputValue}
                        disabled={isRejected || patchMut.isPending}
                        onChange={(_, data) =>
                          setPendingEdits((prev) => ({ ...prev, [s.id]: data.value }))
                        }
                      />
                    </TableCell>
                    <TableCell className={tableStyles.bodyCell}>{s.affectedRowCount}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>{confidenceBadge(s.confidence)}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>{statusBadge(s.status)}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>
                      {showApprove || showUpdate ? (
                        <div className={styles.actionGroup}>
                          {showApprove && (
                            <Button
                              appearance="primary"
                              size="small"
                              icon={<CheckmarkRegular />}
                              disabled={patchMut.isPending}
                              onClick={() => approveSuggestion(s)}
                            >
                              Approve
                            </Button>
                          )}
                          {showUpdate && (
                            <Button
                              appearance="primary"
                              size="small"
                              icon={<ArrowSyncRegular />}
                              disabled={patchMut.isPending}
                              onClick={() => updateSuggestion(s)}
                            >
                              Update
                            </Button>
                          )}
                          <Button
                            appearance="secondary"
                            size="small"
                            icon={<DismissRegular />}
                            disabled={isRejected || patchMut.isPending}
                            onClick={() => patchMut.mutate({ id: s.id, status: "rejected" })}
                          >
                            Reject
                          </Button>
                        </div>
                      ) : (
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                          {isRejected ? "Rejected" : "Applied to grid"}
                        </Text>
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
