/** Mirrors SPFx IXrfReading / API JSON (camelCase). */
export type AreaType = "Units" | "Common Areas";

export interface XrfReading {
  readingId: string;
  component: string;
  color: string;
  leadContent: number;
  normalizedComponent?: string;
  normalizedSubstrate?: string;
  isPositive: boolean;
  location: string;
  unitNumber?: string;
  multifamily?: string;
  siteAddress?: string;
  roomType?: string;
  roomNumber?: string;
  substrate?: string;
  side?: string;
  condition?: string;
  timestamp?: string;
  areaType?: AreaType;
}
