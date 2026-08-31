export type Opportunity = {
  id: string;
  title: string | null;
  agency: string | null;
  naics: string | null;
  score: number | null;
  postedDate: string | null;
  deadlineDate: string | null;
  solicitationNumber: string | null;
  setAside: string | null;
  summary: string | null;
  overview: string | null;
  description: string | null;
  responseType: string | null;
  opportunityType: string | null;
  placeOfPerformance: string | null;
  matchReason: string | null;
  inPipeline: boolean | null;
  cleatusUrl: string | null;
  sourceUrl: string | null;
};

export type RecommendationList = {
  items: Opportunity[];
  hasMore: boolean;
  nextCursor: string | null;
};

export type CleatErrorBody = {
  error?: string;
  message?: string;
};

export type Closeout = {
  pursuitId: string;
  opportunityId: string | null;
  outcome: string;
  reasonCode: string | null;
  note: string | null;
  updatedAt: string;
  cleatusSyncedAt: string | null;
};

export type CloseoutResponse = {
  error?: string | null;
  message?: string | null;
  cleatusUpdated: boolean;
  closeout: Closeout;
};

export class CleatApiError extends Error {
  status: number;
  errorCode: string | null;

  constructor(status: number, body: CleatErrorBody | null) {
    super(body?.message ?? `Request failed (${status})`);
    this.name = "CleatApiError";
    this.status = status;
    this.errorCode = body?.error ?? null;
  }

  get isMissingKey(): boolean {
    return this.status === 503 && this.errorCode === "cleat_api_key_missing";
  }
}

export class CloseoutSyncError extends CleatApiError {
  closeout: Closeout;

  constructor(status: number, body: CloseoutResponse) {
    super(status, { error: body.error ?? undefined, message: body.message ?? undefined });
    this.name = "CloseoutSyncError";
    this.closeout = body.closeout;
  }
}

export async function fetchRecommendations(
  minScore = 80,
): Promise<RecommendationList> {
  return getJson<RecommendationList>(
    `/api/cleat/recommendations?minScore=${encodeURIComponent(String(minScore))}`,
  );
}

export async function fetchOpportunity(id: string): Promise<Opportunity> {
  return getJson<Opportunity>(
    `/api/cleat/opportunities/${encodeURIComponent(id)}`,
  );
}

export type Pursuit = {
  id: string;
  opportunityId: string | null;
  title: string | null;
  agency: string | null;
  phase: string | null;
  columnTitle: string | null;
  archived: boolean;
  favorite: boolean | null;
  deadlineDate: string | null;
  postedDate: string | null;
  solicitationNumber: string | null;
  naics: string | null;
  setAside: string | null;
  summary: string | null;
  overview: string | null;
  description: string | null;
  assignee: string | null;
  createdAt: string | null;
  lastActivityAt: string | null;
  lastActivityAvailable: boolean;
  cleatusUrl: string | null;
  sourceUrl: string | null;
};

export type PipelineItem = {
  pursuit: Pursuit;
  needsCloseOut: boolean;
  closeOutReasons: string[];
  closeout: Closeout | null;
};

export type PipelineDashboard = {
  items: PipelineItem[];
  needsCloseOut: PipelineItem[];
  counts: {
    triage: number;
    preparing: number;
    submitted: number;
    won: number;
    lost: number;
    archived: number;
    other: number;
    total: number;
  };
  lastActivityFieldFound: boolean;
  assigneeFieldFound: boolean;
};

export async function fetchPipeline(): Promise<PipelineDashboard> {
  return getJson<PipelineDashboard>("/api/cleat/pipeline");
}

export async function closeOutPursuit(
  pursuitId: string,
  body: { outcome: string; reasonCode?: string; note?: string; opportunityId?: string },
): Promise<CloseoutResponse> {
  const response = await fetch(
    `/api/cleat/pursuits/${encodeURIComponent(pursuitId)}/close-out`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    },
  );
  const parsed = (await readBody(response)) as CloseoutResponse | CleatErrorBody | null;
  if (!response.ok) {
    if (parsed && typeof parsed === "object" && "closeout" in parsed && parsed.closeout) {
      throw new CloseoutSyncError(response.status, parsed as CloseoutResponse);
    }
    throw new CleatApiError(response.status, parsed as CleatErrorBody);
  }
  return parsed as CloseoutResponse;
}

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  const body = await readBody(response);

  if (!response.ok) {
    throw new CleatApiError(response.status, body as CleatErrorBody);
  }

  return body as T;
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) {
    return null;
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return { message: text };
  }
}
