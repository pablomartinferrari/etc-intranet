import { useEffect, useMemo, useState } from "react";
import { ExternalLinkIcon } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
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
import { PageExplainer } from "../../sales/PageExplainer";
import {
  CleatApiError,
  fetchOpportunity,
  fetchRecommendations,
  type Opportunity,
} from "../api/cleat";

const DEFAULT_MIN_SCORE = 80;

type DeadlineFilter = "all" | "overdue" | "upcoming" | "none";

export function OpportunitiesPage() {
  const [items, setItems] = useState<Opportunity[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<CleatApiError | Error | null>(null);
  const [selected, setSelected] = useState<Opportunity | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [deadlineFilter, setDeadlineFilter] = useState<DeadlineFilter>("all");
  const [naicsFilter, setNaicsFilter] = useState("all");
  const [setAsideFilter, setSetAsideFilter] = useState("all");

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

  const naicsOptions = useMemo(() => uniqueValues(items.map((item) => item.naics)), [items]);
  const setAsideOptions = useMemo(
    () => uniqueValues(items.map((item) => item.setAside)),
    [items],
  );

  const query = search.trim().toLowerCase();
  const hasActiveFilters =
    query.length > 0 ||
    deadlineFilter !== "all" ||
    naicsFilter !== "all" ||
    setAsideFilter !== "all";

  const filtered = useMemo(() => {
    return items.filter((item) => {
      if (query && !matchesSearch(item, query)) {
        return false;
      }
      if (deadlineFilter !== "all" && deadlineBucket(item.deadlineDate) !== deadlineFilter) {
        return false;
      }
      if (naicsFilter !== "all" && (item.naics ?? "") !== naicsFilter) {
        return false;
      }
      if (setAsideFilter !== "all" && (item.setAside ?? "") !== setAsideFilter) {
        return false;
      }
      return true;
    });
  }, [deadlineFilter, items, naicsFilter, query, setAsideFilter]);

  function clearFilters() {
    setSearch("");
    setDeadlineFilter("all");
    setNaicsFilter("all");
    setSetAsideFilter("all");
  }

  return (
    <main className="mx-auto grid w-full max-w-[1100px] gap-4 px-5 py-8 pb-14">
      <PageExplainer title="Bids">
        <p>
          Live list from CLEATUS, loaded when you open the page (score ≥ 80 by
          default). Not stored in the intranet DB.
        </p>
        <p>
          Open a row for detail; Open in CLEATUS for the full breakdown. A missing
          API key shows a clear setup message.
        </p>
      </PageExplainer>

      {loading && <Spinner label="Loading recommended opportunities..." />}

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
          <AlertTitle>Could not load CLEATUS recommendations</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      {!loading && !error && items.length === 0 && (
        <Alert>
          <AlertTitle>No recommendations</AlertTitle>
          <AlertDescription>
            CLEATUS returned no opportunities at the default minimum score of{" "}
            {DEFAULT_MIN_SCORE}. Try a lower threshold later, or review the
            capture profile in CLEATUS.
          </AlertDescription>
        </Alert>
      )}

      {!loading && items.length > 0 && (
        <div className="flex flex-col gap-3 rounded-lg bg-card p-3 shadow-sm">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <div className="grid gap-1.5">
              <Label htmlFor="bids-search">Search</Label>
              <Input
                id="bids-search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Title, agency, or id"
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="bids-deadline">Deadline</Label>
              <Select
                value={deadlineFilter}
                onValueChange={(value) => setDeadlineFilter(value as DeadlineFilter)}
              >
                <SelectTrigger id="bids-deadline">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All deadlines</SelectItem>
                  <SelectItem value="overdue">Overdue</SelectItem>
                  <SelectItem value="upcoming">Upcoming</SelectItem>
                  <SelectItem value="none">No deadline</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="bids-naics">NAICS</Label>
              <Select value={naicsFilter} onValueChange={setNaicsFilter}>
                <SelectTrigger id="bids-naics">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All NAICS</SelectItem>
                  {naicsOptions.map((value) => (
                    <SelectItem key={value} value={value}>
                      {value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="bids-set-aside">Set-aside</Label>
              <Select value={setAsideFilter} onValueChange={setSetAsideFilter}>
                <SelectTrigger id="bids-set-aside">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All set-asides</SelectItem>
                  {setAsideOptions.map((value) => (
                    <SelectItem key={value} value={value}>
                      {value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-sm text-muted-foreground">
              Showing {filtered.length} of {items.length}
            </p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={clearFilters}
              disabled={!hasActiveFilters}
            >
              Clear filters
            </Button>
          </div>
        </div>
      )}

      {!loading && items.length > 0 && filtered.length === 0 && (
        <Alert>
          <AlertTitle>No matching bids</AlertTitle>
          <AlertDescription>
            Nothing matches these filters. Clear them to see the full list.
          </AlertDescription>
        </Alert>
      )}

      {!loading && filtered.length > 0 && (
        <div className="overflow-x-auto rounded-lg bg-card p-2 shadow-sm">
          <Table aria-label="Recommended opportunities">
            <TableHeader>
              <TableRow>
                <TableHead>Title</TableHead>
                <TableHead>Agency</TableHead>
                <TableHead>Score</TableHead>
                <TableHead>Deadline</TableHead>
                <TableHead>NAICS / set-aside</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((item) => (
                <TableRow
                  key={item.id}
                  className="cursor-pointer"
                  onClick={() => void openDetail(item)}
                >
                  <TableCell>
                    <div className="grid gap-0.5">
                      <span>{item.title ?? "Untitled opportunity"}</span>
                      {item.solicitationNumber && (
                        <span className="text-xs text-muted-foreground">
                          {item.solicitationNumber}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>{item.agency ?? "—"}</TableCell>
                  <TableCell>
                    {item.score == null ? (
                      "—"
                    ) : (
                      <Badge variant="secondary">{Math.round(item.score)}</Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="grid gap-0.5">
                      <span>{formatDate(item.deadlineDate)}</span>
                      {item.postedDate && (
                        <span className="text-xs text-muted-foreground">
                          Posted {formatDate(item.postedDate)}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="grid gap-0.5">
                      <span>{item.naics ?? "—"}</span>
                      {item.setAside && (
                        <span className="text-xs text-muted-foreground">{item.setAside}</span>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Sheet
        open={selected !== null}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
      >
        <SheetContent side="right" className="w-full sm:max-w-lg">
          <SheetHeader>
            <SheetTitle>{selected?.title ?? "Opportunity"}</SheetTitle>
          </SheetHeader>
          {selected && (
            <div className="grid gap-3 overflow-y-auto px-4 pb-6">
              {detailLoading && <Spinner size="sm" label="Loading detail..." />}
              {detailError && (
                <Alert>
                  <AlertDescription>
                    Showing the list row only. {detailError}
                  </AlertDescription>
                </Alert>
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

              <div className="mt-2 flex flex-wrap items-center gap-3">
                {selected.cleatusUrl && (
                  <Button
                    onClick={() =>
                      window.open(selected.cleatusUrl!, "_blank", "noopener,noreferrer")
                    }
                  >
                    <ExternalLinkIcon />
                    Open in CLEATUS
                  </Button>
                )}
                {selected.sourceUrl && (
                  <a
                    href={selected.sourceUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="text-sm underline underline-offset-4"
                  >
                    Original notice
                  </a>
                )}
              </div>
            </div>
          )}
        </SheetContent>
      </Sheet>
    </main>
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

function matchesSearch(item: Opportunity, query: string): boolean {
  const haystack = [item.title, item.agency, item.id, item.solicitationNumber]
    .filter((value): value is string => Boolean(value))
    .join(" ")
    .toLowerCase();
  return haystack.includes(query);
}

function deadlineBucket(value: string | null | undefined): Exclude<DeadlineFilter, "all"> {
  if (!value) {
    return "none";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "none";
  }
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return date < today ? "overdue" : "upcoming";
}

function uniqueValues(values: Array<string | null | undefined>): string[] {
  return [...new Set(values.filter((value): value is string => Boolean(value && value.trim())))].sort(
    (a, b) => a.localeCompare(b),
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
