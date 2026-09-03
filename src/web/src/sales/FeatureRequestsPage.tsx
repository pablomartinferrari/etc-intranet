import { useEffect, useState } from "react";
import { InboxIcon, PlusIcon } from "lucide-react";

import { BrandBar, SignOutButton } from "@/components/brand-bar";
import { PageBreadcrumb } from "@/components/page-breadcrumb";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
  featureRequestAreaLabel,
  listFeatureRequests,
  updateFeatureRequestStatus,
  type FeatureRequest,
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
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<FeatureRequest | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [captureOpen, setCaptureOpen] = useState(false);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      setItems(await listFeatureRequests());
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
    try {
      const updated = await updateFeatureRequestStatus(id, status);
      setItems((current) => current.map((row) => (row.id === id ? updated : row)));
      setSelected((current) => (current?.id === id ? updated : current));
    } catch (err) {
      setStatusError(err instanceof Error ? err.message : "Could not update status.");
    }
  }

  function onSaved(created: FeatureRequest) {
    setItems((current) => [created, ...current.filter((row) => row.id !== created.id)]);
    setError(null);
  }

  return (
    <div className="flex min-h-svh flex-col bg-muted/40">
      <BrandBar actions={<SignOutButton outlineOnBlack />} />
      <div className="flex flex-col gap-3 border-b bg-background px-6 py-4">
        <PageBreadcrumb items={[{ label: "Home", to: "/" }, { label: "Feature Requests" }]} />
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <InboxIcon className="size-7" />
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">Feature Requests</h1>
              <p className="text-sm text-muted-foreground">
                Suggest intranet improvements and review the queue.
              </p>
            </div>
          </div>
          <Button type="button" onClick={() => setCaptureOpen(true)}>
            <PlusIcon />
            Add feature request
          </Button>
        </div>
      </div>
      <main className="mx-auto grid w-full max-w-[1100px] flex-1 gap-4 px-5 py-8 pb-14">
        <PageExplainer title="Feature Requests">
          <p>
            Notes staff left from Home for any intranet topic — Chat, Lead, Sales, General,
            or another area you name. Each row is stored in intranet Postgres, including
            older Sales / Bids / Pipeline tickets. Mark a request planned or done when you
            pick it up.
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
              Use Add feature request to suggest an intranet improvement. Missing the
              assistant still saves the raw note.
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
                    onClick={() => setSelected(row)}
                  >
                    <TableCell className="whitespace-nowrap">
                      {formatDate(row.createdAt)}
                    </TableCell>
                    <TableCell>{featureRequestAreaLabel(row)}</TableCell>
                    <TableCell className="max-w-[28rem] truncate font-medium">{row.title}</TableCell>
                    <TableCell>
                      <StatusBadge status={row.status} />
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
        <SheetContent className="sm:max-w-lg" side="right">
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
                  <Select
                    value={selected.status}
                    onValueChange={(value) => {
                      if (value === "new" || value === "planned" || value === "done") {
                        void changeStatus(selected.id, value);
                      }
                    }}
                  >
                    <SelectTrigger className="w-48">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="new">New</SelectItem>
                      <SelectItem value="planned">Planned</SelectItem>
                      <SelectItem value="done">Done</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <CapturedTicket request={selected} />
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>
    </div>
  );
}

function StatusBadge({ status }: { status: FeatureRequestStatus }) {
  if (status === "done") {
    return <Badge variant="secondary">Done</Badge>;
  }
  if (status === "planned") {
    return <Badge variant="outline">Planned</Badge>;
  }
  return <Badge>New</Badge>;
}

function formatDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString();
}
