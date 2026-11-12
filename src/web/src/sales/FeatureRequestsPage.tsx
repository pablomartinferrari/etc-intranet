import { useEffect, useState } from "react";
import { InboxIcon, PlusIcon } from "lucide-react";

import { BrandBar, SignOutButton } from "@/components/brand-bar";
import { PageBreadcrumb } from "@/components/page-breadcrumb";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
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
import { RequireAuth } from "../multifamily-lbp/auth/RequireAuth";
import { PageExplainer } from "./PageExplainer";
import { CapturedTicket, RequestChangeSheet } from "./RequestChangeSheet";
import {
  FEATURE_REQUEST_STATUS_LABEL,
  featureRequestAreaLabel,
  getFeatureRequestMeta,
  listFeatureRequests,
  normalizeFeatureRequestStatus,
  updateFeatureRequestStatus,
  type FeatureRequest,
  type FeatureRequestMeta,
  type FeatureRequestStatus,
} from "./api/featureRequests";

export function FeatureRequestsRoute(): React.JSX.Element {
  return (
    <RequireAuth>
      <FeatureRequestsPage />
    </RequireAuth>
  );
}

function FeatureRequestsPage() {
  const [items, setItems] = useState<FeatureRequest[]>([]);
  const [meta, setMeta] = useState<FeatureRequestMeta | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<FeatureRequest | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [statusBusy, setStatusBusy] = useState(false);
  const [captureOpen, setCaptureOpen] = useState(false);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const [rows, nextMeta] = await Promise.all([listFeatureRequests(), getFeatureRequestMeta()]);
      setItems(rows);
      setMeta(nextMeta);
    } catch (err) {
      setItems([]);
      setError(err instanceof Error ? err.message : "Could not load requests.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function changeStatus(id: number, status: FeatureRequestStatus) {
    setStatusError(null);
    setStatusBusy(true);
    try {
      const updated = await updateFeatureRequestStatus(id, status);
      setItems((current) => current.map((row) => (row.id === id ? updated : row)));
      setSelected((current) => (current?.id === id ? updated : current));
    } catch (err) {
      setStatusError(err instanceof Error ? err.message : "Could not update status.");
    } finally {
      setStatusBusy(false);
    }
  }

  function onSaved(created: FeatureRequest) {
    setItems((current) => [created, ...current.filter((row) => row.id !== created.id)]);
    setError(null);
  }

  return (
    <div className="flex min-h-svh flex-col bg-muted/40">
      <BrandBar actions={<SignOutButton outlineOnBlack />} />
      <div className="flex flex-col gap-3 border-b bg-background px-4 py-3 md:px-6 md:py-4">
        <PageBreadcrumb items={[{ label: "Home", to: "/" }, { label: "Feature Requests" }]} />
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex min-w-0 items-center gap-3">
            <InboxIcon className="size-7 shrink-0" />
            <div className="min-w-0">
              <h1 className="text-xl font-semibold tracking-tight md:text-2xl">Feature Requests</h1>
              <p className="text-sm text-muted-foreground">
                Submit intranet improvements for approval, then track them through ship and confirm.
              </p>
            </div>
          </div>
          <Button type="button" onClick={() => setCaptureOpen(true)}>
            <PlusIcon />
            Add feature request
          </Button>
        </div>
      </div>
      <main className="mx-auto grid w-full max-w-[1100px] flex-1 gap-4 px-4 py-6 pb-24 md:px-5 md:py-8 md:pb-14">
        <PageExplainer title="How approval works">
          <p>
            Anyone signed in can submit a request. Approvers review new items (Approve or Reject).
            After a request is approved and the work is deployed, mark it shipped. The original
            requester or an approver can then confirm and close it — or close it without fanfare
            (won&apos;t do / duplicate).
          </p>
        </PageExplainer>

        {loading && <Spinner label="Loading requests..." />}

        {error && (
          <Alert variant="destructive">
            <AlertTitle>Could not load requests</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {statusError && (
          <Alert variant="destructive">
            <AlertTitle>Could not update status</AlertTitle>
            <AlertDescription>{statusError}</AlertDescription>
          </Alert>
        )}

        {!loading && !error && items.length === 0 && (
          <Alert>
            <AlertTitle>No requests yet</AlertTitle>
            <AlertDescription>
              Use Add feature request to suggest an intranet improvement. New tickets start as
              awaiting approval.
            </AlertDescription>
          </Alert>
        )}

        {!loading && items.length > 0 && (
          <div className="overflow-x-auto rounded-lg bg-card p-2 shadow-sm">
            <Table aria-label="Feature requests">
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Area</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Person</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((row) => (
                  <TableRow
                    key={row.id}
                    className="cursor-pointer"
                    onClick={() => {
                      setStatusError(null);
                      setSelected(row);
                    }}
                  >
                    <TableCell className="whitespace-nowrap">
                      {formatDate(row.createdAt)}
                    </TableCell>
                    <TableCell>{featureRequestAreaLabel(row)}</TableCell>
                    <TableCell className="max-w-[28rem] truncate font-medium">{row.title}</TableCell>
                    <TableCell>
                      <StatusBadge status={normalizeFeatureRequestStatus(row.status)} />
                    </TableCell>
                    <TableCell className="max-w-[16rem] truncate">{row.createdBy}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </main>

      <RequestChangeSheet
        open={captureOpen}
        onOpenChange={setCaptureOpen}
        onSaved={onSaved}
      />

      <Sheet open={selected !== null} onOpenChange={(open) => !open && setSelected(null)}>
        <SheetContent className="w-full sm:max-w-lg" side="right">
          {selected && (
            <>
              <SheetHeader>
                <SheetTitle>{selected.title}</SheetTitle>
                <SheetDescription>
                  {featureRequestAreaLabel(selected)} · {formatDate(selected.createdAt)} ·{" "}
                  {selected.createdBy}
                </SheetDescription>
              </SheetHeader>
              <div className="flex flex-1 flex-col gap-4 overflow-y-auto px-4 pb-4">
                <div className="flex flex-col gap-2">
                  <p className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
                    Status
                  </p>
                  <StatusBadge status={normalizeFeatureRequestStatus(selected.status)} />
                </div>
                <RequestActions
                  request={selected}
                  meta={meta}
                  busy={statusBusy}
                  onChange={(status) => void changeStatus(selected.id, status)}
                />
                {(selected.reviewedBy || selected.closedBy) && (
                  <div className="flex flex-col gap-1 text-sm text-muted-foreground">
                    {selected.reviewedBy && (
                      <p>
                        Reviewed by {selected.reviewedBy}
                        {selected.reviewedAt ? ` · ${formatDate(selected.reviewedAt)}` : ""}
                      </p>
                    )}
                    {selected.closedBy && (
                      <p>
                        Closed by {selected.closedBy}
                        {selected.closedAt ? ` · ${formatDate(selected.closedAt)}` : ""}
                      </p>
                    )}
                  </div>
                )}
                <CapturedTicket request={selected} />
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>
    </div>
  );
}

function RequestActions({
  request,
  meta,
  busy,
  onChange,
}: {
  request: FeatureRequest;
  meta: FeatureRequestMeta | null;
  busy: boolean;
  onChange: (status: FeatureRequestStatus) => void;
}) {
  const status = normalizeFeatureRequestStatus(request.status);
  const canApprove = request.viewerCanApprove ?? meta?.viewerCanApprove ?? false;
  const canClose = request.viewerCanClose ?? false;

  if (status === "new") {
    return (
      <div className="flex flex-col gap-2">
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            disabled={busy || !canApprove}
            onClick={() => onChange("approved")}
          >
            Approve
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={busy || !canApprove}
            onClick={() => onChange("rejected")}
          >
            Reject
          </Button>
        </div>
        {!canApprove && (
          <p className="text-sm text-muted-foreground">
            Only configured approvers can approve or reject.
          </p>
        )}
      </div>
    );
  }

  if (status === "approved") {
    return (
      <div className="flex flex-col gap-2">
        <Button type="button" disabled={busy} onClick={() => onChange("shipped")}>
          Mark shipped
        </Button>
        <p className="text-sm text-muted-foreground">
          Use this after the change is deployed.
        </p>
      </div>
    );
  }

  if (status === "shipped") {
    return (
      <div className="flex flex-col gap-2">
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            disabled={busy || !canClose}
            onClick={() => onChange("closed")}
          >
            Confirm &amp; close
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={busy || !canClose}
            onClick={() => onChange("closed")}
          >
            Close
          </Button>
        </div>
        {!canClose && (
          <p className="text-sm text-muted-foreground">
            Only the original requester or an approver can confirm or close this.
          </p>
        )}
      </div>
    );
  }

  if (status === "rejected") {
    return <p className="text-sm text-muted-foreground">This request was rejected.</p>;
  }

  return <p className="text-sm text-muted-foreground">This request is closed.</p>;
}

function StatusBadge({ status }: { status: FeatureRequestStatus }) {
  const label = FEATURE_REQUEST_STATUS_LABEL[status] ?? status;
  if (status === "rejected") {
    return <Badge variant="destructive">{label}</Badge>;
  }
  if (status === "approved") {
    return <Badge variant="outline">{label}</Badge>;
  }
  if (status === "shipped") {
    return <Badge variant="secondary">{label}</Badge>;
  }
  if (status === "closed") {
    return <Badge variant="outline">{label}</Badge>;
  }
  return <Badge>{label}</Badge>;
}

function formatDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString();
}
