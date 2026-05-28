import type { AreaType, XrfReading } from "@mf/types/xrfReading";

const SHOT_ID_PREFIX: Record<AreaType, string> = {
  Units: "U",
  "Common Areas": "CA",
};

export function buildShotIdMap(readings: XrfReading[], areaType: AreaType): Map<string, string> {
  const prefix = SHOT_ID_PREFIX[areaType];
  const map = new Map<string, string>();
  readings.forEach((r, i) => {
    map.set(r.readingId, `${prefix}-${i + 1}`);
  });
  return map;
}
