import { apiGet, apiPost } from "./client";

export interface JobDto {
  jobId: number;
  clientName: string;
  facilityName: string | null;
  facilityAddress: string | null;
  jobStatus: string | null;
}

export interface RecentJob {
  jobIdentifier: string;
  clientName?: string;
  facilityName?: string;
  updatedAt: string;
}

export async function fetchJob(jobNumber: string): Promise<JobDto | null> {
  try {
    return await apiGet<JobDto>(`/jobs/${encodeURIComponent(jobNumber.trim())}`);
  } catch {
    return null;
  }
}

export function fetchRecentJobs(limit = 10): Promise<RecentJob[]> {
  return apiGet<RecentJob[]>(`/jobs/recent?limit=${limit}`);
}

export function ensureJob(jobNumber: string): Promise<{ jobIdentifier: string }> {
  return apiPost(`/jobs/${encodeURIComponent(jobNumber)}/ensure`);
}
