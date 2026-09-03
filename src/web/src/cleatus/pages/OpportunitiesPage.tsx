import { useEffect, useMemo, useState } from "react";
import { ExternalLinkIcon, SearchIcon } from "lucide-react";

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

const DEADLINE_FILTERS = [
  { value: "all", label: "All deadlines" },
  { value: "overdue", label: "Overdue" },
  { value: "7days", label: "Due in 7 days" },
  { value: "30days", label: "Due in 30 days" },
  { value: "none", label: "No deadline" },
] as const;

type DeadlineFilter = (typeof DEADLINE_FILTERS)[number]["value"];

export function OpportunitiesPage() {
  const [items, setItems] = useState<Opportunity[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<CleatApiError | Error | null>(null);
  const [selected, setSelected] = useState<Opportunity | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [deadlineFilter, setDeadlineFilter] = useState<DeadlineFilter>("all");
  const [agencyFilter, setAgencyFilter] = useState("all");
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

  const agencies = useMemo(() => uniqueSorted(items.map((item) => item.agency)), [items]);
  const setAsides = useMemo(() => uniqueSorted(items.map((item) => item.setAside)), [items]);

  const filtered = useMemo(
    () =>
      items.filter((item) => {
        if (!matchesSearch(item, search)) {
          return false;
        }
        if (agencyFilter !== "all" && (item.agency ?? "") !== agencyFilter) {
          return false;
        }
        if (setAsideFilter !== "all" && (item.setAside ?? "") !== setAsideFilter) {
          return false;
        }
        return matchesDeadline(item.deadlineDate, deadlineFilter);
      }),
    [agencyFilter, deadlineFilter, items, search, setAsideFilter],
  );

  const hasActiveFilters =
    search.trim() !== "" ||
    deadlineFilter !== "all" ||
    agencyFilter !== "all" ||
    setAsideFilter !== "all";

  function clearFilters() {
    setSearch("");
    setDeadlineFilter("all");
    setAgencyFilter("all");
    setSetAsideFilter("all");
  }

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
        <section className="grid gap-2.5">
          <div className="flex flex-wrap items-end gap-2.5">
            <div className="grid min-w-[220px] flex-1 gap-1.5">
              <Label htmlFor="bids-search">Search</Label>
              <div className="relative">
                <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="bids-search"
                  type="search"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Title, agency, or notice ID"
                  className="pl-8"
                />
              </div>
            </div>
            {agencies.length > 0 && (
              <div className="grid gap-1.5">
                <Label htmlFor="bids-agency">Agency</Label>
                <Select value={agencyFilter} onValueChange={setAgencyFilter}>
                  <SelectTrigger id="bids-agency" className="w-[200px]">
                    <SelectValue placeholder="All agencies" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All agencies</SelectItem>
                    {agencies.map((agency) => (
                      <SelectItem key={agency} value={agency}>
                        {agency}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="grid gap-1.5">
              <Label htmlFor="bids-deadline">Deadline</Label>
              <Select
                value={deadlineFilter}
                onValueChange={(value) => setDeadlineFilter(value as DeadlineFilter)}
              >
                <SelectTrigger id="bids-deadline" className="w-[180px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DEADLINE_FILTERS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {setAsides.length > 0 && (
              <div className="grid gap-1.5">
                <Label htmlFor="bids-set-aside">Set-aside</Label>
                <Select value={setAsideFilter} onValueChange={setSetAsideFilter}>
                  <SelectTrigger id="bids-set-aside" className="w-[180px]">
                    <SelectValue placeholder="All set-asides" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All set-asides</SelectItem>
                    {setAsides.map((setAside) => (
                      <SelectItem key={setAside} value={setAside}>
                        {setAside}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <Button
              type="button"
              variant="outline"
              disabled={!hasActiveFilters}
              onClick={clearFilters}
            >
              Clear filters
            </Button>
          </div>
          <p className="text-sm text-muted-foreground">
            {hasActiveFilters
              ? `Showing ${filtered.length} of ${items.length} opportunities`
              : `${items.length} opportunities`}
          </p>

          {filtered.length === 0 ? (
            <Alert>
              <AlertTitle>No matching opportunities</AlertTitle>
              <AlertDescription>
                Nothing matches the current filters. Clear filters to see all{" "}
                {items.length} recommendations.
              </AlertDescription>
            </Alert>
          ) : (
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
        </section>
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

function uniqueSorted(values: Array<string | null | undefined>): string[] {
  return [...new Set(values.filter((value): value is string => Boolean(value && value.trim())))].sort(
    (a, b) => a.localeCompare(b),
  );
}

function matchesSearch(item: Opportunity, query: string): boolean {
  const needle = query.trim().toLowerCase();
  if (!needle) {
    return true;
  }

  const haystack = [
    item.title,
    item.agency,
    item.solicitationNumber,
    item.naics,
    item.setAside,
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();

  return haystack.includes(needle);
}

function matchesDeadline(deadline: string | null | undefined, filter: DeadlineFilter): boolean {
  if (filter === "all") {
    return true;
  }

  const parsed = parseDate(deadline);
  if (filter === "none") {
    return parsed == null;
  }
  if (parsed == null) {
    return false;
  }

  const today = startOfDay(new Date());
  const deadlineDay = startOfDay(parsed);
  if (filter === "overdue") {
    return deadlineDay.getTime() < today.getTime();
  }

  const days = filter === "7days" ? 7 : 30;
  const until = startOfDay(new Date(today));
  until.setDate(until.getDate() + days);
  return deadlineDay.getTime() >= today.getTime() && deadlineDay.getTime() <= until.getTime();
}

function parseDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function startOfDay(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate());
}
