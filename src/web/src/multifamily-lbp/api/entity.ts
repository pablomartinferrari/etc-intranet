import { apiGet, apiPatch, apiPost, apiDelete, apiDownload } from "./client";

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

export interface SharePointSourceFile {
  id: string;
  fileName: string;
  areaType: string;
  processedStatus: string;
  createdAt: string | null;
  modifiedAt: string | null;
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

export interface RunNormalizationResult {
  needsReview: NormalizationSuggestion[];
  autoAppliedCount: number;
}

/** True when the suggestion still needs Approve/Reject (non-exact or user-edited). */
export function normalizationNeedsReview(s: NormalizationSuggestion): boolean {
  if (s.status === "rejected") return false;
  if (s.status === "edited" || s.status === "pending" || s.status === "approved") return true;
  if (s.status === "applied") return differsFromAutoApplied(s);
  return true;
}

function differsFromAutoApplied(s: NormalizationSuggestion): boolean {
  if (s.approvedValue?.trim()) return true;

  const effective = (s.approvedValue ?? s.suggestedValue).trim().toLowerCase();
  const suggested = s.suggestedValue.trim().toLowerCase();
  const originals = parseOriginalVariants(s.originalValue);

  if (effective !== suggested) return true;
  return !originals.every((o) => o.trim().toLowerCase() === effective);
}

/** Original column may join spellings with a middle dot (e.g. Cabinet Casing · Cabinet Casings). */
export function parseOriginalVariants(originalValue: string): string[] {
  return originalValue
    .split("·")
    .map((v) => v.trim())
    .filter(Boolean);
}

export function formatOriginalDisplay(originalValue: string): string {
  const parts = parseOriginalVariants(originalValue);
  return parts.length > 1 ? parts.join(", ") : originalValue;
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

export function fetchSourceFiles(jobId: string, entitySlug: string): Promise<SharePointSourceFile[]> {
  return apiGet<SharePointSourceFile[]>(`${base(jobId, entitySlug)}/source-files`);
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

export function importLegacy(
  jobId: string,
  entitySlug: string,
  overwrite = false
): Promise<{ imported: number; filesAdded: number; filesSkipped: number }> {
  return apiPost(`${base(jobId, entitySlug)}/import-legacy`, { overwrite });
}

export interface ClearWorkspaceResult {
  rowsRemoved: number;
  batchesRemoved: number;
  normalizationsRemoved: number;
  reportsRemoved: number;
}

export function clearWorkspace(jobId: string, entitySlug: string): Promise<ClearWorkspaceResult> {
  return apiPost(`${base(jobId, entitySlug)}/workspace/clear`, {});
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
): Promise<RunNormalizationResult> {
  return apiPost(`${base(jobId, entitySlug)}/normalize`, body);
}

export function fetchNormalizations(
  jobId: string,
  entitySlug: string,
  status?: string,
  fields?: string[]
): Promise<NormalizationSuggestion[]> {
  const params = new URLSearchParams();
  if (status) params.set("status", status);
  if (fields?.length) params.set("fields", fields.join(","));
  const q = params.toString();
  return apiGet<NormalizationSuggestion[]>(
    `${base(jobId, entitySlug)}/normalizations${q ? `?${q}` : ""}`
  );
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

export async function downloadReportExcel(
  jobId: string,
  entitySlug: string,
  reportId: string
): Promise<void> {
  const { blob, fileName } = await apiDownload(`${base(jobId, entitySlug)}/reports/${reportId}/export`);
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
