import { useMemo, useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

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
import { DATA_TYPE_FILTER_OPTIONS, RESULT_FILTER_OPTIONS } from "@mf/config/reportOptions";
import {
  displayComponentWithEdits,
  displaySubstrate,
} from "@mf/utils/readingGroups";
import { ReadingResultBadge } from "@mf/utils/readingResult";

export function DataGridPage(): React.JSX.Element {
  const tableStyles = useDataTableStyles();
  const { jobId, entitySlug, refetchDashboard, dashboard } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;
  const qc = useQueryClient();
  const [dataType, setDataType] = useState<string>("");
  const [resultFilter, setResultFilter] = useState<string>("");
  const [search, setSearch] = useState("");
  const [edits, setEdits] = useState<Record<string, Partial<InspectionRow>>>({});
  const [saveMsg, setSaveMsg] = useState<string | null>(null);

  const params = useMemo(() => {
    const p: Record<string, string> = {};
    if (dataType) p.dataType = dataType;
    if (resultFilter) p.result = resultFilter;
    if (search) p.search = search;
    return p;
  }, [dataType, resultFilter, search]);

  const { data: rows = [], isLoading } = useQuery({
    queryKey: ["rows", jobId, entitySlug, params],
    queryFn: () => fetchRows(jobId, entitySlug, params),
  });

  useEffect(() => {
    void refetchDashboard();
  }, [jobId, entitySlug, refetchDashboard]);

  const totalRows =
    dashboard != null ? dashboard.unitsRowCount + dashboard.commonAreasRowCount : null;
  const hasFilters = Boolean(dataType || resultFilter || search);

  const saveMut = useMutation({
    mutationFn: () =>
      patchRows(
        jobId,
        entitySlug,
        Object.entries(edits).map(([id, e]) => ({
          id,
          location: e.location as string | undefined,
          normalizedComponent: e.normalizedComponent as string | undefined,
          notes: e.notes as string | undefined,
        }))
      ),
    onSuccess: (r) => {
      setEdits({});
      setSaveMsg(`Saved ${r.updated} rows`);
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
      refetchDashboard();
    },
  });

  const update = (id: string, field: keyof InspectionRow, value: string): void => {
    setEdits((prev) => ({ ...prev, [id]: { ...prev[id], [field]: value } }));
  };

  const display = (row: InspectionRow, field: keyof InspectionRow): string => {
    const e = edits[row.id];
    const v = e && field in e ? (e as Record<string, unknown>)[field] : row[field];
    return v == null ? "" : String(v);
  };

  const displayComponent = (row: InspectionRow): string =>
    displayComponentWithEdits(row, edits[row.id]);

  const rowClass = (row: InspectionRow): string | undefined => {
    if (edits[row.id]) return "bg-muted";
    if (row.validationStatus === "error") return "bg-red-50";
    if (row.validationStatus === "warning") return "bg-amber-50";
    return tableStyles.zebra;
  };

  return (
    <div>
      <h1 className="mb-2 text-2xl font-semibold tracking-tight">
        Data grid
        {totalRows != null && (
          <span className="mt-1 block text-base font-normal text-muted-foreground">
            {hasFilters
              ? `Showing ${rows.length.toLocaleString()} of ${totalRows.toLocaleString()} rows`
              : `${totalRows.toLocaleString()} rows total`}
          </span>
        )}
      </h1>
      <p className="mb-6 text-muted-foreground">
        Edit locations and components inline. Component shows the normalized name when set, otherwise the imported
        value; edits are saved to the normalized field. Changes are highlighted until you save.{" "}
        <Link to={`${base}/normalize`}>Next: AI normalization</Link>
      </p>

      <div className="mb-4 flex flex-wrap items-end gap-4 rounded-md border bg-card p-4">
        <div className="grid w-full min-w-0 gap-1.5 sm:w-auto sm:min-w-[180px]">
          <Label>Data type</Label>
          <Select value={dataType || "all"} onValueChange={(v) => setDataType(v === "all" ? "" : v)}>
            <SelectTrigger className="w-full sm:min-w-[180px]">
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
        <div className="grid w-full min-w-0 gap-1.5 sm:w-auto sm:min-w-[180px]">
          <Label>Result</Label>
          <Select
            value={resultFilter || "all-results"}
            onValueChange={(v) => setResultFilter(v === "all-results" ? "" : v)}
          >
            <SelectTrigger className="w-full sm:min-w-[180px]">
              <SelectValue placeholder="All results" />
            </SelectTrigger>
            <SelectContent>
              {RESULT_FILTER_OPTIONS.map((opt) => (
                <SelectItem key={opt.value || "all-results"} value={opt.value || "all-results"}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid w-full min-w-0 gap-1.5 sm:w-auto sm:min-w-[180px]">
          <Label>Search</Label>
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Component, location…"
          />
        </div>
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
        <Spinner label="Loading rows…" />
      ) : rows.length === 0 ? (
        <div className="py-12 text-center text-muted-foreground">
          <p>No rows match your filters. Import from SharePoint or adjust filters.</p>
        </div>
      ) : (
        <DataTablePanel>
          <Table className={tableStyles.table} aria-label="Inspection data grid">
            <TableHeader className={tableStyles.stickyHead}>
              <TableRow>
                {["Reading", "Type", "Location", "Component", "Substrate", "Pb (mg/cm²)", "Result"].map((h) => (
                  <TableHead key={h} className={tableStyles.headCell}>
                    {h}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id} className={rowClass(row)}>
                  <TableCell className={cn(tableStyles.bodyCell, "font-mono text-xs")}>{row.readingId}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <Badge variant="outline">{row.dataType === "commonAreas" ? "Common" : "Units"}</Badge>
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <Input
                      className="h-7"
                      value={display(row, "location")}
                      onChange={(e) => update(row.id, "location", e.target.value)}
                    />
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <Input
                      className="h-7"
                      value={displayComponent(row)}
                      onChange={(e) => update(row.id, "normalizedComponent", e.target.value)}
                    />
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>{displaySubstrate(row)}</TableCell>
                  <TableCell className={cn(tableStyles.bodyCell, "font-mono text-xs")}>
                    {row.leadContent.toFixed(2)}
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <ReadingResultBadge row={row} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </DataTablePanel>
      )}

      <p className="mt-2 text-xs text-muted-foreground">
        {hasFilters && totalRows != null
          ? `${rows.length.toLocaleString()} of ${totalRows.toLocaleString()} rows shown`
          : `${rows.length.toLocaleString()} row${rows.length === 1 ? "" : "s"}`}
        {totalRows != null && !hasFilters && ` · ${totalRows.toLocaleString()} total`}
      </p>
    </div>
  );
}
