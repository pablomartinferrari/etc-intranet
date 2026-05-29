import {
  Badge,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  tokens,
} from "@fluentui/react-components";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";
import { REPORT_SECTION_COLUMNS, STATISTICAL_SAMPLE_SIZE } from "@mf/config/reportOptions";

const COLUMN_LABELS: Record<string, string> = {
  readingId: "Reading",
  component: "Component",
  substrate: "Substrate",
  location: "Location",
  leadContent: "Pb (mg/cm²)",
  isPositive: "Positive",
  color: "Color",
  totalReadings: "Total Readings",
  positiveCount: "Positive Count",
  negativeCount: "Negative Count",
  positivePercent: "Positive %",
  negativePercent: "Negative %",
  result: "Result",
  shotCount: "Shot count",
  count: "Count",
  validationStatus: "Validation",
};

const COLUMN_ORDER = [
  "readingId",
  "component",
  "substrate",
  "location",
  "leadContent",
  "isPositive",
  "color",
  "totalReadings",
  "positiveCount",
  "negativeCount",
  "positivePercent",
  "negativePercent",
  "result",
  "shotCount",
  "count",
  "validationStatus",
];

function headerLabel(key: string): string {
  return COLUMN_LABELS[key] ?? key.replace(/([A-Z])/g, " $1").replace(/^./, (s) => s.toUpperCase());
}

function formatCell(key: string, value: unknown): React.ReactNode {
  if (value == null || value === "") return "—";
  if (key === "isPositive") {
    const positive =
      typeof value === "boolean" ? value : value === "true" || value === "POSITIVE";
    return (
      <Badge appearance="filled" color={positive ? "danger" : "success"}>
        {positive ? "Positive" : "Negative"}
      </Badge>
    );
  }
  if (key === "result" && typeof value === "string") {
    const positive = value.toUpperCase() === "POSITIVE";
    return (
      <Badge appearance="filled" color={positive ? "danger" : "success"}>
        {positive ? "Positive" : "Negative"}
      </Badge>
    );
  }
  if (key === "leadContent" && typeof value === "number") return value.toFixed(2);
  if (key === "positivePercent" && typeof value === "number") return `${value.toFixed(2)}%`;
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

function resolveColumns(sectionKey: string, rows: Record<string, unknown>[]): string[] {
  const fixed = REPORT_SECTION_COLUMNS[sectionKey as keyof typeof REPORT_SECTION_COLUMNS];
  if (fixed) return [...fixed];

  const keys = new Set<string>();
  for (const row of rows) {
    Object.keys(row).forEach((k) => {
      if (k !== "readings") keys.add(k);
    });
  }
  const ordered = COLUMN_ORDER.filter((k) => keys.has(k));
  const rest = [...keys].filter((k) => !ordered.includes(k)).sort();
  return [...ordered, ...rest];
}

function isPositiveReading(value: unknown): boolean {
  if (value === true || value === "true") return true;
  if (typeof value === "number") return value >= 1.0;
  return false;
}

/** Legacy reports stored per-shot rows under nonUniformShots.readings — roll up to summary. */
function aggregateLegacyNonUniformGroup(row: Record<string, unknown>): Record<string, unknown> {
  const readings = row.readings as Array<Record<string, unknown>> | undefined;
  if (!Array.isArray(readings) || readings.length === 0) {
    return row;
  }

  const total = readings.length;
  const positives = readings.filter((r) =>
    isPositiveReading(r.isPositive ?? r.IsPositive) ||
    (typeof r.leadContent === "number" && r.leadContent >= 1) ||
    (typeof r.LeadContent === "number" && r.LeadContent >= 1)
  ).length;
  const pct = total > 0 ? Math.round((positives * 10000) / total) / 100 : 0;

  return {
    component: row.component,
    substrate: row.substrate,
    positiveCount: positives,
    negativeCount: total - positives,
    positivePercent: pct,
    totalReadings: total,
  };
}

/** Legacy uniform rows may lack result — derive from counts when possible. */
function normalizeLegacyUniformRow(row: Record<string, unknown>): Record<string, unknown> {
  if (row.result != null && row.result !== "") return row;

  const total = Number(row.totalReadings ?? row.shotCount ?? 0);
  const positives = Number(row.positiveCount ?? 0);
  const pct = total > 0 ? (positives * 100) / total : 0;

  return {
    ...row,
    result: pct >= 2.5 ? "POSITIVE" : "NEGATIVE",
    totalReadings: total,
  };
}

/** Per-shot rows saved directly in nonUniformShots (oldest format). */
function isLegacyFlatNonUniformRows(rows: Record<string, unknown>[]): boolean {
  if (rows.length === 0) return false;
  const first = rows[0];
  return (
    (first.readingId != null || first.ReadingId != null) &&
    first.totalReadings == null &&
    first.positiveCount == null &&
    !Array.isArray(first.readings)
  );
}

function aggregateFlatNonUniformReadings(rows: Record<string, unknown>[]): Record<string, unknown>[] {
  const map = new Map<string, Record<string, unknown>[]>();

  for (const row of rows) {
    const key = String(row.component ?? "");
    const list = map.get(key);
    if (list) list.push(row);
    else map.set(key, [row]);
  }

  return [...map.values()].map((readings) => {
    const total = readings.length;
    const positives = readings.filter(
      (r) =>
        isPositiveReading(r.isPositive ?? r.IsPositive) ||
        (typeof r.leadContent === "number" && r.leadContent >= 1) ||
        (typeof r.LeadContent === "number" && r.LeadContent >= 1)
    ).length;
    const pct = total > 0 ? Math.round((positives * 10000) / total) / 100 : 0;
    const substrates = [
      ...new Set(
        readings
          .map((r) => String(r.substrate ?? "").trim())
          .filter(Boolean)
      ),
    ];

    return {
      component: readings[0].component,
      substrate: substrates.length === 0 ? "" : substrates.length === 1 ? substrates[0] : "Multiple",
      positiveCount: positives,
      negativeCount: total - positives,
      positivePercent: pct,
      totalReadings: total,
      readings,
    };
  });
}

function hasNonUniformSummary(row: Record<string, unknown>): boolean {
  return row.positiveCount != null || row.totalReadings != null;
}

function normalizeNonUniformRow(row: Record<string, unknown>): Record<string, unknown> {
  if (Array.isArray(row.readings) && !hasNonUniformSummary(row)) {
    return aggregateLegacyNonUniformGroup(row);
  }
  if (Array.isArray(row.readings)) {
    const { readings: _readings, ...summary } = row;
    return summary;
  }
  return row;
}

/** Older snapshots stored uniform shots (or per-shot rows) under nonUniformShots — fix tabs for display. */
export function reconcileReportResultForViewer(
  result: Record<string, unknown>
): Record<string, unknown> {
  const nonUniformRaw = result.nonUniformShots;
  if (!Array.isArray(nonUniformRaw) || nonUniformRaw.length === 0) return result;

  const rows = nonUniformRaw as Record<string, unknown>[];
  const isFlatLegacy = isLegacyFlatNonUniformRows(rows);
  const hasNestedLegacy = rows.some(
    (r) => Array.isArray(r.readings) && !hasNonUniformSummary(r)
  );
  if (!isFlatLegacy && !hasNestedLegacy) return result;

  const summaries: Record<string, unknown>[] = isFlatLegacy
    ? aggregateFlatNonUniformReadings(rows)
    : rows.map((r) => {
        if (Array.isArray(r.readings) && !hasNonUniformSummary(r)) {
          return { ...aggregateLegacyNonUniformGroup(r), readings: r.readings };
        }
        return r;
      });

  const uniformShots = Array.isArray(result.uniformShots)
    ? [...(result.uniformShots as Record<string, unknown>[])]
    : [];
  const nonUniformShots: Record<string, unknown>[] = [];

  for (const row of summaries) {
    const total = Number(row.totalReadings ?? 0);
    const positives = Number(row.positiveCount ?? 0);
    if (total >= STATISTICAL_SAMPLE_SIZE) continue;
    if (positives === 0 || positives === total) {
      uniformShots.push({
        component: row.component,
        substrate: row.substrate,
        result: positives === total ? "POSITIVE" : "NEGATIVE",
        totalReadings: total,
      });
    } else {
      nonUniformShots.push(row);
    }
  }

  return { ...result, uniformShots, nonUniformShots };
}

export function getNonUniformReadingDetails(data: unknown): Array<{
  component: string;
  readings: Record<string, unknown>[];
}> {
  if (!Array.isArray(data)) return [];

  const rows = data as Record<string, unknown>[];
  const sourceRows = isLegacyFlatNonUniformRows(rows) ? aggregateFlatNonUniformReadings(rows) : rows;

  return sourceRows
    .map((row) => {
      const details = Array.isArray(row.readings) ? (row.readings as Record<string, unknown>[]) : [];
      return {
        component: String(row.component ?? ""),
        readings: details,
      };
    })
    .filter((g) => g.readings.length > 0);
}

export function normalizeReportSectionRows(sectionKey: string, data: unknown): Record<string, unknown>[] {
  if (!Array.isArray(data)) return [];

  if (sectionKey === "nonUniformShots") {
    const rows = data as Record<string, unknown>[];
    if (isLegacyFlatNonUniformRows(rows)) {
      return aggregateFlatNonUniformReadings(rows).map(normalizeNonUniformRow);
    }
    return rows.map(normalizeNonUniformRow);
  }

  if (sectionKey === "uniformShots") {
    return (data as Record<string, unknown>[]).map(normalizeLegacyUniformRow);
  }

  return data as Record<string, unknown>[];
}

export function ReportSectionGrid({
  sectionKey,
  data,
  emptyMessage = "No rows in this section.",
}: {
  sectionKey: string;
  data: unknown;
  emptyMessage?: string;
}): React.JSX.Element {
  const tableStyles = useDataTableStyles();
  const rows = normalizeReportSectionRows(sectionKey, data);

  if (rows.length === 0) {
    return (
      <Text block style={{ color: tokens.colorNeutralForeground3, marginTop: tokens.spacingVerticalM }}>
        {emptyMessage}
      </Text>
    );
  }

  const columns = resolveColumns(sectionKey, rows);
  const readingGroups = sectionKey === "nonUniformShots" ? getNonUniformReadingDetails(data) : [];

  return (
    <>
      <DataTablePanel>
        <Table className={tableStyles.table} size="small" aria-label="Report section">
          <TableHeader className={tableStyles.stickyHead}>
            <TableRow>
              {columns.map((col) => (
                <TableHeaderCell key={col} className={tableStyles.headCell}>
                  {headerLabel(col)}
                </TableHeaderCell>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row, idx) => (
              <TableRow key={idx} className={tableStyles.zebra}>
                {columns.map((col) => (
                  <TableCell key={col} className={tableStyles.bodyCell}>
                    {formatCell(col, row[col])}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </DataTablePanel>

      {readingGroups.map((group) => (
        <div key={group.component} style={{ marginTop: tokens.spacingVerticalL }}>
          <Text weight="semibold" block style={{ marginBottom: tokens.spacingVerticalS }}>
            {group.component} — individual readings
          </Text>
          <DataTablePanel maxHeight="min(40vh, 320px)">
            <Table className={tableStyles.table} size="small" aria-label={`Readings for ${group.component}`}>
              <TableHeader className={tableStyles.stickyHead}>
                <TableRow>
                  {["readingId", "substrate", "location", "leadContent", "isPositive"].map((col) => (
                    <TableHeaderCell key={col} className={tableStyles.headCell}>
                      {headerLabel(col)}
                    </TableHeaderCell>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {group.readings.map((reading, idx) => (
                  <TableRow key={`${group.component}-${idx}`} className={tableStyles.zebra}>
                    {["readingId", "substrate", "location", "leadContent", "isPositive"].map((col) => (
                      <TableCell key={col} className={tableStyles.bodyCell}>
                        {formatCell(col, reading[col])}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </DataTablePanel>
        </div>
      ))}
    </>
  );
}
