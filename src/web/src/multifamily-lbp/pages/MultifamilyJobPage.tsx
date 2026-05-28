import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link, useParams } from "react-router-dom";
import {
  Button,
  Input,
  MessageBar,
  MessageBarBody,
  Spinner,
  Tab,
  TabList,
  Text,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { ArrowLeftRegular } from "@fluentui/react-icons";
import { fetchJob, type JobDto } from "@mf/api/jobs";
import { fetchUnitsReadings, fetchCommonAreasReadings } from "@mf/api/multifamily";
import type { AreaType, XrfReading } from "@mf/types/xrfReading";
import { buildShotIdMap } from "@mf/utils/shotIdUtils";
import { getDisplayUnit } from "@mf/utils/displayUnitUtils";

const useStyles = makeStyles({
  toolbar: {
    display: "flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalM,
    alignItems: "flex-end",
    marginBottom: tokens.spacingVerticalM,
  },
  stats: {
    display: "flex",
    gap: tokens.spacingHorizontalXL,
    marginBottom: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
  },
  gridWrap: {
    maxHeight: "560px",
    overflow: "auto",
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
  },
  table: {
    width: "100%",
    borderCollapse: "collapse" as const,
    fontSize: tokens.fontSizeBase200,
  },
  th: {
    textAlign: "left" as const,
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground3,
    position: "sticky" as const,
    top: 0,
    zIndex: 1,
  },
  td: {
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
});

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
  const styles = useStyles();
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
      <div className={styles.toolbar}>
        <FieldSmall label="Search">
          <Input value={searchText} onChange={(_, d) => setSearchText(d.value)} placeholder="Shot ID, component, location…" />
        </FieldSmall>
        <FieldSmall label="Result">
          <select
            value={filterResult}
            onChange={(e) => setFilterResult(e.target.value)}
            style={{ minHeight: 32, padding: "0 8px", borderRadius: 4, border: "1px solid #c4c4c4" }}
          >
            <option value="all">All</option>
            <option value="positive">Positive</option>
            <option value="negative">Negative</option>
          </select>
        </FieldSmall>
        <FieldSmall label="Side">
          <select
            value={filterSide}
            onChange={(e) => setFilterSide(e.target.value)}
            style={{ minHeight: 32, padding: "0 8px", borderRadius: 4, border: "1px solid #c4c4c4" }}
          >
            <option value="all">All</option>
            {uniqueSides.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </FieldSmall>
      </div>
      <div className={styles.stats}>
        <div>
          <Text size={200}>Total shots</Text>
          <Title1>{stats.total}</Title1>
        </div>
        <div>
          <Text size={200}>Positive</Text>
          <Title1>{stats.positive}</Title1>
        </div>
        <div>
          <Text size={200}>Shown (filtered)</Text>
          <Title1>{stats.filtered}</Title1>
        </div>
      </div>
      <div className={styles.gridWrap}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th className={styles.th}>Shot ID</th>
              <th className={styles.th}>Reading #</th>
              <th className={styles.th}>Component (Substrate)</th>
              <th className={styles.th}>Unit #</th>
              <th className={styles.th}>Room Type</th>
              <th className={styles.th}>Room #</th>
              <th className={styles.th}>Side</th>
              <th className={styles.th}>Substrate</th>
              <th className={styles.th}>Color</th>
              <th className={styles.th}>PbC (mg/cm²)</th>
              <th className={styles.th}>Result</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((item) => {
              const c = item.normalizedComponent || item.component;
              const sub = item.normalizedSubstrate || item.substrate;
              const compDisp = sub ? `${c} (${sub})` : c;
              return (
                <tr key={item.readingId}>
                  <td className={styles.td}>{shotIdMap.get(item.readingId) ?? "—"}</td>
                  <td className={styles.td}>{item.readingId}</td>
                  <td className={styles.td}>{compDisp}</td>
                  <td className={styles.td}>{getDisplayUnit(item, props.areaType)}</td>
                  <td className={styles.td}>{item.roomType || "—"}</td>
                  <td className={styles.td}>{item.roomNumber || "—"}</td>
                  <td className={styles.td}>{item.side || "—"}</td>
                  <td className={styles.td}>{item.normalizedSubstrate || item.substrate || "—"}</td>
                  <td className={styles.td}>{item.color}</td>
                  <td className={styles.td}>
                    <Text style={{ color: item.isPositive ? tokens.colorPaletteRedForeground1 : tokens.colorPaletteGreenForeground1 }}>
                      {item.leadContent.toFixed(2)}
                    </Text>
                  </td>
                  <td className={styles.td}>
                    <Text weight="semibold" style={{ color: item.isPositive ? tokens.colorPaletteRedForeground1 : tokens.colorPaletteGreenForeground1 }}>
                      {item.isPositive ? "POSITIVE" : "Negative"}
                    </Text>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function FieldSmall(props: { label: string; children: ReactNode }): React.JSX.Element {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXXS, minWidth: 140 }}>
      <Text size={200} weight="semibold">
        {props.label}
      </Text>
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
      <div style={{ display: "flex", gap: tokens.spacingHorizontalM, alignItems: "center", marginBottom: tokens.spacingVerticalM }}>
        <Link to="/" style={{ textDecoration: "none" }}>
          <Button icon={<ArrowLeftRegular />} appearance="subtle">
            Back
          </Button>
        </Link>
      </div>
      <Title1 block>Job {decoded}</Title1>
      {job && (
        <MessageBar intent="info" style={{ marginTop: tokens.spacingVerticalM }}>
          <MessageBarBody>
            {job.clientName}
            {job.facilityAddress || job.facilityName ? ` · ${job.facilityAddress ?? job.facilityName}` : ""}
          </MessageBarBody>
        </MessageBar>
      )}
      {!job && job !== undefined && (
        <MessageBar intent="warning" style={{ marginTop: tokens.spacingVerticalM }}>
          <MessageBarBody>Could not load job metadata from API.</MessageBarBody>
        </MessageBar>
      )}
      {loadError && (
        <MessageBar intent="error" style={{ marginTop: tokens.spacingVerticalM }}>
          <MessageBarBody>{loadError}</MessageBarBody>
        </MessageBar>
      )}
      <TabList selectedValue={tab} onTabSelect={(_, d) => setTab(d.value as "units" | "common")} style={{ marginTop: tokens.spacingVerticalL }}>
        <Tab value="units">Units — All shots</Tab>
        <Tab value="common">Common areas — All shots</Tab>
      </TabList>
      {loadingReadings ? (
        <div style={{ marginTop: tokens.spacingVerticalL }}>
          <Spinner label="Loading shots…" />
        </div>
      ) : (
        <div style={{ marginTop: tokens.spacingVerticalL }}>
          <ShotsGrid readings={activeReadings} areaType={areaType} />
        </div>
      )}
    </div>
  );
}
