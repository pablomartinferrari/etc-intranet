import { apiGet } from "./client";
import type { XrfReading } from "@mf/types/xrfReading";

export async function fetchUnitsReadings(jobNumber: string): Promise<XrfReading[]> {
  const j = encodeURIComponent(jobNumber);
  return apiGet<XrfReading[]>(`/multifamily/${j}/units`);
}

export async function fetchCommonAreasReadings(jobNumber: string): Promise<XrfReading[]> {
  const j = encodeURIComponent(jobNumber);
  return apiGet<XrfReading[]>(`/multifamily/${j}/common-areas`);
}
