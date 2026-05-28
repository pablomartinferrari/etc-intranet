import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogSurface,
  DialogTitle,
  Input,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Title1,
  tokens,
} from "@fluentui/react-components";
import { useEntity } from "@mf/context/EntityContext";
import { applyNormalizations, fetchNormalizations, patchNormalization } from "@mf/api/entity";

export function NormalizeReviewPage(): React.JSX.Element {
  const { jobId, entitySlug, refetchDashboard } = useEntity();
  const qc = useQueryClient();
  const [confirmOpen, setConfirmOpen] = useState(false);

  const { data: items = [], isLoading } = useQuery({
    queryKey: ["normalizations", jobId, entitySlug],
    queryFn: () => fetchNormalizations(jobId, entitySlug),
  });

  const patchMut = useMutation({
    mutationFn: ({ id, status, value }: { id: string; status: string; value?: string }) =>
      patchNormalization(jobId, entitySlug, id, status, value),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["normalizations", jobId, entitySlug] }),
  });

  const applyMut = useMutation({
    mutationFn: () =>
      applyNormalizations(
        jobId,
        entitySlug,
        items.filter((i) => i.status === "approved" || i.status === "edited").map((i) => i.id)
      ),
    onSuccess: () => {
      setConfirmOpen(false);
      refetchDashboard();
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
    },
  });

  const approveHigh = (): void => {
    items.filter((i) => i.confidence === "high" && i.status === "pending").forEach((i) => {
      patchMut.mutate({ id: i.id, status: "approved" });
    });
  };

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        Review AI suggestions
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM }}>
        Grouped by unique original value. Approve, edit, or reject each suggestion.
      </Text>
      <div style={{ display: "flex", gap: tokens.spacingHorizontalM, marginBottom: tokens.spacingVerticalM }}>
        <Button onClick={approveHigh}>Approve all high confidence</Button>
        <Button appearance="primary" onClick={() => setConfirmOpen(true)}>
          Apply approved changes
        </Button>
      </div>
      {isLoading ? (
        <Spinner />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              {["Field", "Original", "Suggestion", "Rows", "Confidence", "Status", "Actions"].map((h) => (
                <TableHeaderCell key={h}>{h}</TableHeaderCell>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((s) => (
              <TableRow key={s.id}>
                <TableCell>{s.fieldName}</TableCell>
                <TableCell>{s.originalValue}</TableCell>
                <TableCell>
                  <Input
                    size="small"
                    defaultValue={s.approvedValue ?? s.suggestedValue}
                    onBlur={(e) => patchMut.mutate({ id: s.id, status: "edited", value: e.target.value })}
                  />
                </TableCell>
                <TableCell>{s.affectedRowCount}</TableCell>
                <TableCell>{s.confidence}</TableCell>
                <TableCell>{s.status}</TableCell>
                <TableCell>
                  <Button size="small" onClick={() => patchMut.mutate({ id: s.id, status: "approved" })}>Approve</Button>
                  <Button size="small" onClick={() => patchMut.mutate({ id: s.id, status: "rejected" })}>Reject</Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      <Dialog open={confirmOpen} onOpenChange={(_, d) => setConfirmOpen(d.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Apply approved normalization changes?</DialogTitle>
            <Text>Original values will be preserved.</Text>
          </DialogBody>
          <DialogActions>
            <Button appearance="secondary" onClick={() => setConfirmOpen(false)}>Cancel</Button>
            <Button appearance="primary" disabled={applyMut.isPending} onClick={() => applyMut.mutate()}>
              Apply
            </Button>
          </DialogActions>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
