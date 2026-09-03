import { getApiAuthHeaders } from "../../multifamily-lbp/api/client";

/** Home capture areas plus legacy Sales page values kept for existing tickets. */
export type FeatureRequestPage =
  | "chat"
  | "lead"
  | "sales"
  | "general"
  | "opportunities"
  | "pipeline";
export type FeatureRequestStatus = "new" | "planned" | "done";

export const FEATURE_REQUEST_AREAS: { value: FeatureRequestPage; label: string }[] = [
  { value: "chat", label: "Chat" },
  { value: "lead", label: "Lead" },
  { value: "sales", label: "Sales" },
  { value: "general", label: "General" },
];

export const FEATURE_REQUEST_PAGE_LABEL: Record<FeatureRequestPage, string> = {
  chat: "Chat",
  lead: "Lead",
  sales: "Sales",
  general: "General",
  opportunities: "Bids",
  pipeline: "Pipeline",
};

export function featureRequestPageLabel(page: string): string {
  return FEATURE_REQUEST_PAGE_LABEL[page as FeatureRequestPage] ?? page;
}

export type FeatureRequest = {
  id: number;
  page: FeatureRequestPage;
  createdBy: string;
  createdAt: string;
  rawText: string;
  title: string;
  problem: string;
  desiredBehavior: string;
  dataInvolved: string;
  acceptanceCriteria: string;
  status: FeatureRequestStatus;
  structuredBy: "llm" | "fallback";
};

export class FeatureRequestApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "FeatureRequestApiError";
    this.status = status;
  }
}

export async function createFeatureRequest(
  page: FeatureRequestPage,
  rawText: string,
): Promise<FeatureRequest> {
  return request<FeatureRequest>("/api/feature-requests", {
    method: "POST",
    body: JSON.stringify({ page, rawText }),
  });
}

export async function listFeatureRequests(): Promise<FeatureRequest[]> {
  const result = await request<{ items: FeatureRequest[] }>("/api/feature-requests");
  return result.items ?? [];
}

export async function updateFeatureRequestStatus(
  id: number,
  status: FeatureRequestStatus,
): Promise<FeatureRequest> {
  return request<FeatureRequest>(`/api/feature-requests/${id}`, {
    method: "PATCH",
    body: JSON.stringify({ status }),
  });
}

async function request<T>(url: string, init: RequestInit = {}): Promise<T> {
  const auth = await getApiAuthHeaders();
  const response = await fetch(url, {
    ...init,
    headers: {
      ...auth,
      ...(init.body ? { "Content-Type": "application/json" } : {}),
      ...init.headers,
    },
  });
  const body = await readBody(response);
  if (!response.ok) {
    const message =
      typeof body === "object" && body && "message" in body && typeof body.message === "string"
        ? body.message
        : `Request failed (${response.status})`;
    throw new FeatureRequestApiError(response.status, message);
  }
  return body as T;
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) {
    return {};
  }
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return { message: text };
  }
}
