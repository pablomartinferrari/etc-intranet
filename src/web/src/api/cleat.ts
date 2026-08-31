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
