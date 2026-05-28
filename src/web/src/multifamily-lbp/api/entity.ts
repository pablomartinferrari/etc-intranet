import { apiGet, apiPatch, apiPost, apiDelete } from "./client";

export interface EntityDashboard {
  jobId: string;
  entitySlug: string;
  entityDisplayName: string;
  uploadedFilesCount: number;
  unitsRowCount: number;
  commonAreasRowCount: number;
  validationWarningCount: number;
  validationErrorCount: number;
  pendingNormalizationCount: number;
  normalizationStatus: string | null;
  lastReportGeneratedAt: string | null;
  hasRows: boolean;
}

export interface InspectionRow {
  id: string;
  readingId: string;
  sourceFileName: string;
  dataType: string;
  location: string;
  roomOrArea?: string;
  component: string;
  normalizedComponent?: string;
  substrate?: string;
  normalizedSubstrate?: string;
  shotCount: number;
  notes?: string;
  validationStatus: string;
  color: string;
  leadContent: number;
  isPositive: boolean;
  side?: string;
}

export interface UploadBatch {
  id: string;
  sourceFileName: string;
  dataType: string;
  status: string;
  importedRowCount: number;
  buildingProperty?: string;
  batchName?: string;
  createdAt: string;
  warnings?: string[];
}

export interface UploadResult {
  batchId: string;
  sourceFileName: string;
  dataType: string;
  status: string;
  rowCount: number;
  warnings: string[];
  errors: string[];
}

export interface NormalizationSuggestion {
  id: string;
  fieldName: string;
  originalValue: string;
  suggestedValue: string;
  approvedValue?: string;
  affectedRowCount: number;
  dataType: string;
  confidence: string;
  status: string;
}

export interface ReportConfig {
  dataType: string;
  sections: string[];
  uniformThreshold: number;
  groupBy: string;
  useNormalizedValues: boolean;
}

export interface ReportSnapshot {
  id: string;
  dataType: string;
  uniformThreshold: number;
  useNormalizedValues: boolean;
  generatedAt: string;
  generatedBy?: string;
  result: Record<string, unknown>;
}

function base(jobId: string, entitySlug: string): string {
  return `/jobs/${encodeURIComponent(jobId)}/${encodeURIComponent(entitySlug)}`;
}

export function fetchEntityDashboard(jobId: string, entitySlug: string): Promise<EntityDashboard> {
  return apiGet<EntityDashboard>(base(jobId, entitySlug));
}

export function fetchRows(
  jobId: string,
  entitySlug: string,
  params?: Record<string, string>
): Promise<InspectionRow[]> {
  const q = params ? `?${new URLSearchParams(params)}` : "";
  return apiGet<InspectionRow[]>(`${base(jobId, entitySlug)}/rows${q}`);
}

export function patchRows(
  jobId: string,
  entitySlug: string,
  rows: Array<{ id: string; [key: string]: unknown }>
): Promise<{ updated: number }> {
  return apiPatch(`${base(jobId, entitySlug)}/rows`, { rows });
}

export function fetchUploadBatches(jobId: string, entitySlug: string): Promise<UploadBatch[]> {
  return apiGet<UploadBatch[]>(`${base(jobId, entitySlug)}/uploads`);
}

export async function uploadFile(
  jobId: string,
  entitySlug: string,
  file: File,
  dataType: string,
  meta?: { buildingProperty?: string; batchName?: string }
): Promise<UploadResult> {
  const form = new FormData();
  form.append("file", file);
  form.append("dataType", dataType);
  if (meta?.buildingProperty) form.append("buildingProperty", meta.buildingProperty);
  if (meta?.batchName) form.append("batchName", meta.batchName);
  const apiBase = import.meta.env.VITE_API_BASE ?? "/api";
  const res = await fetch(`${apiBase}${base(jobId, entitySlug)}/uploads`, { method: "POST", body: form });
  if (!res.ok) throw new Error(await res.text());
  return res.json() as Promise<UploadResult>;
}

export function fetchUploadResults(
  jobId: string,
  entitySlug: string,
  batchId: string
): Promise<UploadResult> {
  return apiGet<UploadResult>(`${base(jobId, entitySlug)}/uploads/${batchId}/results`);
}

export function deleteUploadBatch(
  jobId: string,
  entitySlug: string,
  batchId: string
): Promise<void> {
  return apiDelete(`${base(jobId, entitySlug)}/uploads/${batchId}`);
}

export function importLegacy(jobId: string, entitySlug: string, overwrite = false): Promise<{ imported: number }> {
  return apiPost(`${base(jobId, entitySlug)}/import-legacy`, { overwrite });
}

export function runNormalization(
  jobId: string,
  entitySlug: string,
  body: {
    fields: string[];
    scope: string;
    dataType?: string;
    rowIds?: string[];
  }
): Promise<NormalizationSuggestion[]> {
  return apiPost(`${base(jobId, entitySlug)}/normalize`, body);
}

export function fetchNormalizations(
  jobId: string,
  entitySlug: string,
  status?: string
): Promise<NormalizationSuggestion[]> {
  const q = status ? `?status=${encodeURIComponent(status)}` : "";
  return apiGet<NormalizationSuggestion[]>(`${base(jobId, entitySlug)}/normalizations${q}`);
}

export function patchNormalization(
  jobId: string,
  entitySlug: string,
  id: string,
  status: string,
  approvedValue?: string
): Promise<NormalizationSuggestion> {
  return apiPatch(`${base(jobId, entitySlug)}/normalizations/${id}`, { status, approvedValue });
}

export function applyNormalizations(
  jobId: string,
  entitySlug: string,
  suggestionIds: string[]
): Promise<{ rowsUpdated: number }> {
  return apiPost(`${base(jobId, entitySlug)}/normalizations/apply`, { suggestionIds });
}

export function generateReport(
  jobId: string,
  entitySlug: string,
  config: ReportConfig
): Promise<ReportSnapshot> {
  return apiPost(`${base(jobId, entitySlug)}/reports`, config);
}

export function fetchLatestReport(
  jobId: string,
  entitySlug: string,
  dataType?: string
): Promise<ReportSnapshot> {
  const q = dataType ? `?dataType=${encodeURIComponent(dataType)}` : "";
  return apiGet<ReportSnapshot>(`${base(jobId, entitySlug)}/reports/latest${q}`);
}

export function fetchReport(
  jobId: string,
  entitySlug: string,
  reportId: string
): Promise<ReportSnapshot> {
  return apiGet<ReportSnapshot>(`${base(jobId, entitySlug)}/reports/${reportId}`);
}
