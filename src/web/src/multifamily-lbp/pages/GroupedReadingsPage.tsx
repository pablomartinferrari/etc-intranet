import { Fragment, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronDownIcon, ChevronRightIcon } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
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
import { Spinner } from "@/components/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";
import { useEntity } from "@mf/context/EntityContext";
import { fetchRows, patchRows, type InspectionRow } from "@mf/api/entity";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";
import { DATA_TYPE_FILTER_OPTIONS } from "@mf/config/reportOptions";
import {
  displayComponentWithEdits,
  displaySubstrateWithEdits,
  groupReadingsByComponentSubstrate,
} from "@mf/utils/readingGroups";
import { ReadingResultBadge } from "@mf/utils/readingResult";

type EditFields = Pick<InspectionRow, "normalizedComponent" | "normalizedSubstrate">;

export function GroupedReadingsPage(): React.JSX.Element {
  const tableStyles = useDataTableStyles();
  const { jobId, entitySlug, refetchDashboard } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;
  const qc = useQueryClient();
  const [dataType, setDataType] = useState("");
  const [search, setSearch] = useState("");
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set());
  const [edits, setEdits] = useState<Record<string, Partial<EditFields>>>({});
  const [saveMsg, setSaveMsg] = useState<string | null>(null);

  const params = useMemo(() => {
    const p: Record<string, string> = {};
    if (dataType) p.dataType = dataType;
    if (search) p.search = search;
    return p;
  }, [dataType, search]);

  const { data: rows = [], isLoading } = useQuery({
    queryKey: ["rows", jobId, entitySlug, params],
    queryFn: () => fetchRows(jobId, entitySlug, params),
  });

  const groups = useMemo(() => groupReadingsByComponentSubstrate(rows), [rows]);

  const saveMut = useMutation({
    mutationFn: () =>
      patchRows(
        jobId,
        entitySlug,
        Object.entries(edits).map(([id, e]) => ({
          id,
          normalizedComponent: e.normalizedComponent,
          normalizedSubstrate: e.normalizedSubstrate,
        }))
      ),
    onSuccess: (r) => {
      setEdits({});
      setSaveMsg(`Saved ${r.updated} rows`);
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
      refetchDashboard();
    },
  });

  const toggleGroup = (key: string): void => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const expandAll = (): void => setExpanded(new Set(groups.map((g) => g.key)));
  const collapseAll = (): void => setExpanded(new Set());

  const update = (id: string, field: keyof EditFields, value: string): void => {
    setEdits((prev) => ({ ...prev, [id]: { ...prev[id], [field]: value } }));
  };

  const displayComponent = (row: InspectionRow): string =>
    displayComponentWithEdits(row, edits[row.id]);

  const displaySubstrate = (row: InspectionRow): string =>
    displaySubstrateWithEdits(row, edits[row.id]);

  const rowClass = (row: InspectionRow, detail: boolean): string | undefined => {
    if (edits[row.id]) return "bg-muted";
    return detail ? "bg-card" : tableStyles.zebra;
  };

  const columns = ["", "Component", "Substrate", "Reading", "Location", "Pb (mg/cm²)", "Result"];

  return (
    <div>
      <h1 className="mb-2 text-2xl font-semibold tracking-tight">Grouped readings</h1>
      <p className="mb-4 text-muted-foreground">
        Step 5 — readings grouped by component and substrate (normalized values when set). Expand groups to review
        or edit individual shots inline.
      </p>

      <p className="mb-4">
        <Link to={`${base}/normalize`}>Back to AI normalization</Link>
        {" · "}
        <Link to={`${base}/grid`}>View flat data grid</Link>
      </p>

      <div className="mb-4 flex flex-wrap items-end gap-4 rounded-md border bg-card p-4">
        <div className="grid min-w-[180px] gap-1.5">
          <Label>Data type</Label>
          <Select value={dataType || "all"} onValueChange={(v) => setDataType(v === "all" ? "" : v)}>
            <SelectTrigger className="min-w-[180px]">
              <SelectValue placeholder="All types" />
            </SelectTrigger>
            <SelectContent>
              {DATA_TYPE_FILTER_OPTIONS.map((opt) => (
                <SelectItem key={opt.value || "all"} value={opt.value || "all"}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid min-w-[180px] gap-1.5">
          <Label>Search</Label>
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Component, location…"
          />
        </div>
        <Button variant="ghost" onClick={expandAll} disabled={groups.length === 0}>
          Expand all
        </Button>
        <Button variant="ghost" onClick={collapseAll} disabled={groups.length === 0}>
          Collapse all
        </Button>
        <Button
          disabled={Object.keys(edits).length === 0 || saveMut.isPending}
          onClick={() => saveMut.mutate()}
        >
          {saveMut.isPending ? <Spinner size="sm" /> : "Save changes"}
        </Button>
        {Object.keys(edits).length > 0 && (
          <Button variant="secondary" onClick={() => setEdits({})}>
            Discard edits
          </Button>
        )}
      </div>

      {saveMsg && (
        <Alert className="mb-4">
          <AlertDescription>{saveMsg}</AlertDescription>
        </Alert>
      )}

      {isLoading ? (
        <Spinner label="Loading readings…" />
      ) : groups.length === 0 ? (
        <p className="text-muted-foreground">No readings to group. Import data or adjust filters.</p>
      ) : (
        <DataTablePanel>
          <Table className={tableStyles.table} aria-label="Grouped readings">
            <TableHeader className={tableStyles.stickyHead}>
              <TableRow>
                {columns.map((h) => (
                  <TableHead key={h || "expand"} className={tableStyles.headCell}>
                    {h}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {groups.map((g) => {
                const isOpen = expanded.has(g.key);
                return (
                  <Fragment key={g.key}>
                    <TableRow
                      className="cursor-pointer bg-muted hover:bg-muted/80"
                      onClick={() => toggleGroup(g.key)}
                      aria-expanded={isOpen}
                    >
                      <TableCell className={tableStyles.bodyCell}>
                        <Button
                          className="min-w-7"
                          variant="ghost"
                          size="icon-sm"
                          aria-label={isOpen ? "Collapse group" : "Expand group"}
                          onClick={(e) => {
                            e.stopPropagation();
                            toggleGroup(g.key);
                          }}
                        >
                          {isOpen ? <ChevronDownIcon /> : <ChevronRightIcon />}
                        </Button>
                      </TableCell>
                      <TableCell className={cn(tableStyles.bodyCell, "font-semibold")}>{g.component}</TableCell>
                      <TableCell className={tableStyles.bodyCell}>{g.substrate}</TableCell>
                      <TableCell className={tableStyles.bodyCell}>
                        {g.readingCount} reading{g.readingCount === 1 ? "" : "s"}
                      </TableCell>
                      <TableCell className={tableStyles.bodyCell}>—</TableCell>
                      <TableCell className={cn(tableStyles.bodyCell, "font-mono text-xs")}>
                        {g.avgLeadContent.toFixed(2)} avg
                      </TableCell>
                      <TableCell className={tableStyles.bodyCell}>
                        {g.positiveCount > 0 ? (
                          <Badge variant="destructive">{g.positiveCount} positive</Badge>
                        ) : (
                          <Badge variant="outline">All negative</Badge>
                        )}
                      </TableCell>
                    </TableRow>

                    {isOpen &&
                      g.rows.map((row) => (
                        <TableRow
                          key={row.id}
                          className={rowClass(row, true)}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <TableCell className={cn(tableStyles.bodyCell, "pl-8")} />
                          <TableCell className={tableStyles.bodyCell}>
                            <Input
                              className="h-7"
                              value={displayComponent(row)}
                              onChange={(e) => update(row.id, "normalizedComponent", e.target.value)}
                            />
                          </TableCell>
                          <TableCell className={tableStyles.bodyCell}>
                            <Input
                              className="h-7"
                              value={displaySubstrate(row)}
                              onChange={(e) => update(row.id, "normalizedSubstrate", e.target.value)}
                            />
                          </TableCell>
                          <TableCell className={cn(tableStyles.bodyCell, "font-mono text-xs")}>{row.readingId}</TableCell>
                          <TableCell className={tableStyles.bodyCell}>{row.location || "—"}</TableCell>
                          <TableCell className={cn(tableStyles.bodyCell, "font-mono text-xs")}>
                            {row.leadContent.toFixed(2)}
                          </TableCell>
                          <TableCell className={tableStyles.bodyCell}>
                            <ReadingResultBadge row={row} />
                          </TableCell>
                        </TableRow>
                      ))}
                  </Fragment>
                );
              })}
            </TableBody>
          </Table>
        </DataTablePanel>
      )}

      <p className="mt-2 text-xs text-muted-foreground">
        {groups.length} group{groups.length === 1 ? "" : "s"} · {rows.length} reading
        {rows.length === 1 ? "" : "s"}
        {Object.keys(edits).length > 0 && ` · ${Object.keys(edits).length} unsaved edit${Object.keys(edits).length === 1 ? "" : "s"}`}
      </p>
    </div>
  );
}
