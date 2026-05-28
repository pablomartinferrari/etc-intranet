import type { AreaType, XrfReading } from "@mf/types/xrfReading";

export function getDisplayUnit(r: XrfReading, areaType: AreaType): string {
  if (areaType !== "Units") return "-";
  const parts = [r.multifamily, r.siteAddress].filter(Boolean).map((s) => String(s).trim());
  return parts.length > 0 ? parts.join(" ") : r.unitNumber || "-";
}
