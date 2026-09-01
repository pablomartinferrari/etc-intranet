import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeftIcon } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
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
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";
import { fetchJob, type JobDto } from "@mf/api/jobs";
import { fetchUnitsReadings, fetchCommonAreasReadings } from "@mf/api/multifamily";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";
import type { AreaType, XrfReading } from "@mf/types/xrfReading";
import { buildShotIdMap } from "@mf/utils/shotIdUtils";
import { getDisplayUnit } from "@mf/utils/displayUnitUtils";

function useFilteredReadings(
  readings: XrfReading[],
  areaType: AreaType,
  searchText: string,
  filterResult: string,
  filterSide: string
): XrfReading[] {
  const shotIdMap = useMemo(() => buildShotIdMap(readings, areaType), [readings, areaType]);
  return useMemo(() => {
    let result = readings;
    if (searchText) {
      const s = searchText.toLowerCase();
      result = result.filter((r) => {
        const shot = shotIdMap.get(r.readingId)?.toLowerCase() ?? "";
        return (
          shot.includes(s) ||
          r.readingId.toLowerCase().includes(s) ||
          r.component.toLowerCase().includes(s) ||
          (r.normalizedComponent?.toLowerCase().includes(s) ?? false) ||
          (r.location?.toLowerCase().includes(s) ?? false) ||
          getDisplayUnit(r, areaType).toLowerCase().includes(s) ||
          (r.roomType?.toLowerCase().includes(s) ?? false) ||
          (r.roomNumber?.toLowerCase().includes(s) ?? false)
        );
      });
    }
    if (filterResult === "positive") result = result.filter((r) => r.isPositive);
    else if (filterResult === "negative") result = result.filter((r) => !r.isPositive);
    if (filterSide !== "all") result = result.filter((r) => r.side === filterSide);
    return result;
  }, [readings, searchText, filterResult, filterSide, shotIdMap, areaType]);
}

function ShotsGrid(props: { readings: XrfReading[]; areaType: AreaType }): React.JSX.Element {
  const tableStyles = useDataTableStyles();
  const [searchText, setSearchText] = useState("");
  const [filterResult, setFilterResult] = useState("all");
  const [filterSide, setFilterSide] = useState("all");

  const uniqueSides = useMemo(() => {
    const set = new Set<string>();
    props.readings.forEach((r) => {
      if (r.side) set.add(r.side);
    });
    return Array.from(set).sort();
  }, [props.readings]);

  const filtered = useFilteredReadings(props.readings, props.areaType, searchText, filterResult, filterSide);
  const shotIdMap = useMemo(() => buildShotIdMap(props.readings, props.areaType), [props.readings, props.areaType]);

  const stats = useMemo(() => {
    const total = props.readings.length;
    const positive = props.readings.filter((r) => r.isPositive).length;
    return { total, positive, filtered: filtered.length };
  }, [props.readings, filtered.length]);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end gap-4">
        <FieldSmall label="Search">
          <Input
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder="Shot ID, component, location…"
          />
        </FieldSmall>
        <FieldSmall label="Result">
          <Select value={filterResult} onValueChange={setFilterResult}>
            <SelectTrigger className="min-w-[140px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All</SelectItem>
              <SelectItem value="positive">Positive</SelectItem>
              <SelectItem value="negative">Negative</SelectItem>
            </SelectContent>
          </Select>
        </FieldSmall>
        <FieldSmall label="Side">
          <Select value={filterSide} onValueChange={setFilterSide}>
            <SelectTrigger className="min-w-[140px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All</SelectItem>
              {uniqueSides.map((s) => (
                <SelectItem key={s} value={s}>
                  {s}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FieldSmall>
      </div>
      <div className="mb-4 flex gap-8 rounded-md bg-muted p-4">
        <div>
          <p className="text-xs text-muted-foreground">Total shots</p>
          <p className="text-3xl font-bold">{stats.total}</p>
        </div>
        <div>
          <p className="text-xs text-muted-foreground">Positive</p>
          <p className="text-3xl font-bold">{stats.positive}</p>
        </div>
        <div>
          <p className="text-xs text-muted-foreground">Shown (filtered)</p>
          <p className="text-3xl font-bold">{stats.filtered}</p>
        </div>
      </div>
      <DataTablePanel maxHeight="560px">
        <Table className={tableStyles.table}>
          <TableHeader className={tableStyles.stickyHead}>
            <TableRow>
              {[
                "Shot ID",
                "Reading #",
                "Component (Substrate)",
                "Unit #",
                "Room Type",
                "Room #",
                "Side",
                "Substrate",
                "Color",
                "PbC (mg/cm²)",
                "Result",
              ].map((h) => (
                <TableHead key={h} className={tableStyles.headCell}>
                  {h}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.map((item) => {
              const c = item.normalizedComponent || item.component;
              const sub = item.normalizedSubstrate || item.substrate;
              const compDisp = sub ? `${c} (${sub})` : c;
              const tone = item.isPositive ? "text-destructive" : "text-green-600";
              return (
                <TableRow key={item.readingId} className={tableStyles.zebra}>
                  <TableCell className={tableStyles.bodyCell}>{shotIdMap.get(item.readingId) ?? "—"}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{item.readingId}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{compDisp}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{getDisplayUnit(item, props.areaType)}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{item.roomType || "—"}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{item.roomNumber || "—"}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{item.side || "—"}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{item.normalizedSubstrate || item.substrate || "—"}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{item.color}</TableCell>
                  <TableCell className={cn(tableStyles.bodyCell, tone)}>{item.leadContent.toFixed(2)}</TableCell>
                  <TableCell className={cn(tableStyles.bodyCell, "font-semibold", tone)}>
                    {item.isPositive ? "POSITIVE" : "Negative"}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </DataTablePanel>
    </div>
  );
}

function FieldSmall(props: { label: string; children: ReactNode }): React.JSX.Element {
  return (
    <div className="grid min-w-[140px] gap-1.5">
      <Label>{props.label}</Label>
      {props.children}
    </div>
  );
}

export function MultifamilyJobPage(): React.JSX.Element {
  const { jobNumber = "" } = useParams<{ jobNumber: string }>();
  const decoded = decodeURIComponent(jobNumber);
  const [tab, setTab] = useState<"units" | "common">("units");
  const [job, setJob] = useState<JobDto | null | undefined>(undefined);
  const [units, setUnits] = useState<XrfReading[] | undefined>(undefined);
  const [common, setCommon] = useState<XrfReading[] | undefined>(undefined);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const j = await fetchJob(decoded);
      if (!cancelled) setJob(j);
    })();
    return () => {
      cancelled = true;
    };
  }, [decoded]);

  useEffect(() => {
    let cancelled = false;
    setLoadError(null);
    setUnits(undefined);
    setCommon(undefined);
    (async () => {
      try {
        const [u, c] = await Promise.all([fetchUnitsReadings(decoded), fetchCommonAreasReadings(decoded)]);
        if (!cancelled) {
          setUnits(u);
          setCommon(c);
        }
      } catch (e) {
        if (!cancelled) {
          setLoadError(e instanceof Error ? e.message : "Failed to load readings");
          setUnits([]);
          setCommon([]);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [decoded]);

  const loadingReadings = units === undefined || common === undefined;
  const areaType: AreaType = tab === "units" ? "Units" : "Common Areas";
  const activeReadings = tab === "units" ? (units ?? []) : (common ?? []);

  return (
    <div>
      <div className="mb-4 flex items-center gap-4">
        <Button variant="ghost" asChild>
          <Link to="/">
            <ArrowLeftIcon />
            Back
          </Link>
        </Button>
      </div>
      <h1 className="text-2xl font-semibold tracking-tight">Job {decoded}</h1>
      {job && (
        <Alert className="mt-4">
          <AlertDescription>
            {job.clientName}
            {job.facilityAddress || job.facilityName ? ` · ${job.facilityAddress ?? job.facilityName}` : ""}
          </AlertDescription>
        </Alert>
      )}
      {!job && job !== undefined && (
        <Alert className="mt-4">
          <AlertDescription>Could not load job metadata from API.</AlertDescription>
        </Alert>
      )}
      {loadError && (
        <Alert variant="destructive" className="mt-4">
          <AlertDescription>{loadError}</AlertDescription>
        </Alert>
      )}
      <Tabs
        value={tab}
        onValueChange={(v) => setTab(v as "units" | "common")}
        className="mt-6"
      >
        <TabsList>
          <TabsTrigger value="units">Units — All shots</TabsTrigger>
          <TabsTrigger value="common">Common areas — All shots</TabsTrigger>
        </TabsList>
      </Tabs>
      {loadingReadings ? (
        <div className="mt-6">
          <Spinner label="Loading shots…" />
        </div>
      ) : (
        <div className="mt-6">
          <ShotsGrid readings={activeReadings} areaType={areaType} />
        </div>
      )}
    </div>
  );
}
