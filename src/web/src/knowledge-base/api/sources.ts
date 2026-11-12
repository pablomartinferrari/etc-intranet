import { apiDelete, apiGet, apiPost } from "../../multifamily-lbp/api/client";

export type AgentSourceCapabilities = {
  graphConfigured: boolean;
  embeddingsConfigured: boolean;
  softMaxFiles: number;
  softMaxBytes: number;
  mediumMaxFiles: number;
  mediumMaxBytes: number;
  maxFileBytes: number;
  maxDepth: number;
};

export type AgentSourceProbe = {
  siteUrl: string;
  folderPath: string;
  displayPath: string;
  fileCount: number;
  totalBytes: number;
  totalBytesLabel: string;
  allowedFiles: number;
  allowedBytes: number;
  allowedBytesLabel: string;
  skippedFiles: number;
  maxDepth: number;
  sampleExtensions: string[];
  truncated: boolean;
  limitTier: "soft" | "medium" | "hard";
  canAutoRun: boolean;
  requiresConfirm: boolean;
  requiresApproval: boolean;
  summary: string;
};

export type AgentSourceJob = {
  id: string;
  sourceId: string;
  status: "queued" | "probing" | "running" | "done" | "failed" | "awaiting_approval";
  limitTier: string;
  probeAllowedFiles: number;
  probeAllowedBytes: number;
  probeSkippedFiles: number;
  sampleExtensions: string[];
  probeTruncated: boolean;
  errorMessage: string | null;
  filesProcessed: number;
  filesFailed: number;
  filesSkipped: number;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
};

export type AgentSource = {
  id: string;
  label: string | null;
  siteUrl: string;
  folderPath: string;
  displayPath: string;
  status: "connected" | "disconnected" | "awaiting_approval";
  createdBy: string;
  createdAt: string;
  disconnectedAt: string | null;
  approvalRequestId: number | null;
  latestJob: AgentSourceJob | null;
};

export class AgentSourceApiError extends Error {
  status: number;
  code?: string;
  probe?: AgentSourceProbe;

  constructor(status: number, message: string, code?: string, probe?: AgentSourceProbe) {
    super(message);
    this.name = "AgentSourceApiError";
    this.status = status;
    this.code = code;
    this.probe = probe;
  }
}

export function getAgentSourceCapabilities(): Promise<AgentSourceCapabilities> {
  return apiGet<AgentSourceCapabilities>("/kb/sources/capabilities");
}

export function listAgentSources(): Promise<AgentSource[]> {
  return apiGet<AgentSource[]>("/kb/sources");
}

export function probeAgentSource(siteUrl: string, folderPath?: string): Promise<AgentSourceProbe> {
  return request("/kb/sources/probe", {
    method: "POST",
    body: { siteUrl, folderPath: folderPath || undefined },
  });
}

export function connectAgentSource(
  siteUrl: string,
  folderPath: string | undefined,
  label: string | undefined,
  confirmMedium: boolean,
): Promise<AgentSource> {
  return request("/kb/sources", {
    method: "POST",
    body: { siteUrl, folderPath: folderPath || undefined, label: label || undefined, confirmMedium },
  });
}

export function disconnectAgentSource(id: string): Promise<void> {
  return apiDelete(`/kb/sources/${id}`);
}

export function getAgentSourceJob(jobId: string): Promise<AgentSourceJob> {
  return apiGet<AgentSourceJob>(`/kb/sources/jobs/${jobId}`);
}

async function request<T>(path: string, init: { method: string; body?: object }): Promise<T> {
  try {
    return await apiPost<T>(path, init.body);
  } catch (err) {
    throw parseError(err);
  }
}

function parseError(err: unknown): AgentSourceApiError {
  if (err instanceof AgentSourceApiError) {
    return err;
  }
  const message = err instanceof Error ? err.message : "Request failed";
  try {
    const parsed = JSON.parse(message) as {
      message?: string;
      code?: string;
      probe?: AgentSourceProbe;
    };
    if (parsed && typeof parsed.message === "string") {
      return new AgentSourceApiError(0, parsed.message, parsed.code, parsed.probe);
    }
  } catch {
    /* raw text */
  }
  return new AgentSourceApiError(0, message);
}
