import type { InspectionRow } from "@mf/api/entity";
import { isReadingPositive } from "@mf/utils/readingResult";

export function displayComponent(row: InspectionRow): string {
  const v = row.normalizedComponent?.trim() || row.component?.trim();
  return v || "—";
}

export function displaySubstrate(row: InspectionRow): string {
  const v = row.normalizedSubstrate?.trim() || row.substrate?.trim();
  return v || "—";
}

/** Value shown in component editor (normalized when present, else original). */
export function editableComponentValue(row: InspectionRow): string {
  return row.normalizedComponent?.trim() || row.component?.trim() || "";
}

/** Value shown in substrate editor (normalized when present, else original). */
export function editableSubstrateValue(row: InspectionRow): string {
  return row.normalizedSubstrate?.trim() || row.substrate?.trim() || "";
}

export function displayComponentWithEdits(
  row: InspectionRow,
  edits?: Partial<Pick<InspectionRow, "normalizedComponent">>
): string {
  if (edits && "normalizedComponent" in edits) return edits.normalizedComponent ?? "";
  return editableComponentValue(row);
}

export function displaySubstrateWithEdits(
  row: InspectionRow,
  edits?: Partial<Pick<InspectionRow, "normalizedSubstrate">>
): string {
  if (edits && "normalizedSubstrate" in edits) return edits.normalizedSubstrate ?? "";
  return editableSubstrateValue(row);
}

export interface ReadingGroup {
  key: string;
  component: string;
  substrate: string;
  rows: InspectionRow[];
  readingCount: number;
  positiveCount: number;
  avgLeadContent: number;
}

export function groupReadingsByComponentSubstrate(rows: InspectionRow[]): ReadingGroup[] {
  const map = new Map<string, InspectionRow[]>();

  for (const row of rows) {
    const component = displayComponent(row);
    const substrate = displaySubstrate(row);
    const key = `${component.toLowerCase()}\0${substrate.toLowerCase()}`;
    const list = map.get(key);
    if (list) list.push(row);
    else map.set(key, [row]);
  }

  return [...map.entries()]
    .map(([, groupRows]) => {
      const component = displayComponent(groupRows[0]);
      const substrate = displaySubstrate(groupRows[0]);
      const positiveCount = groupRows.filter((r) => isReadingPositive(r)).length;
      const avgLeadContent =
        groupRows.length > 0
          ? groupRows.reduce((sum, r) => sum + r.leadContent, 0) / groupRows.length
          : 0;

      return {
        key: `${component}|${substrate}`,
        component,
        substrate,
        rows: groupRows.sort((a, b) => a.readingId.localeCompare(b.readingId)),
        readingCount: groupRows.length,
        positiveCount,
        avgLeadContent,
      };
    })
    .sort((a, b) =>
      a.component.localeCompare(b.component) || a.substrate.localeCompare(b.substrate)
    );
}
