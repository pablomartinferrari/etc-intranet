import { AuthRequiredError } from "@mf/auth/AuthRequiredError";

const base = import.meta.env.VITE_API_BASE ?? "/api";

let authHeadersProvider: (() => Promise<HeadersInit>) | null = null;

export function hasApiAuth(): boolean {
  return authHeadersProvider !== null;
}

export function setApiAuthHeadersProvider(
  provider: (() => Promise<HeadersInit>) | null,
): void {
  authHeadersProvider = provider;
}

async function mergeAuthHeaders(headers: HeadersInit): Promise<HeadersInit> {
  if (!authHeadersProvider) {
    throw new AuthRequiredError();
  }

  const auth = await authHeadersProvider();
  return { ...auth, ...headers };
}

export async function apiGet<T>(
  path: string,
  extraHeaders?: HeadersInit,
): Promise<T> {
  const headers = await mergeAuthHeaders({
    Accept: "application/json",
    ...extraHeaders,
  });
  const res = await fetch(`${base}${path}`, { headers });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}

export async function apiPost<T>(
  path: string,
  body?: FormData | object,
  extraHeaders?: HeadersInit,
): Promise<T> {
  const isForm = body instanceof FormData;
  const headers = await mergeAuthHeaders(
    isForm
      ? { Accept: "application/json", ...extraHeaders }
      : {
          "Content-Type": "application/json",
          Accept: "application/json",
          ...extraHeaders,
        },
  );
  const res = await fetch(`${base}${path}`, {
    method: "POST",
    headers,
    body: isForm ? body : body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `${res.status}`);
  }
  return res.json() as Promise<T>;
}

export async function apiPatch<T>(
  path: string,
  body: object,
  extraHeaders?: HeadersInit,
): Promise<T> {
  const headers = await mergeAuthHeaders({
    "Content-Type": "application/json",
    Accept: "application/json",
    ...extraHeaders,
  });
  const res = await fetch(`${base}${path}`, {
    method: "PATCH",
    headers,
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json() as Promise<T>;
}

export async function apiDelete(
  path: string,
  extraHeaders?: HeadersInit,
): Promise<void> {
  const headers = await mergeAuthHeaders(extraHeaders ?? {});
  const res = await fetch(`${base}${path}`, { method: "DELETE", headers });
  if (!res.ok) throw new Error(await res.text());
}

/** Download a file (e.g. Excel export). Returns blob and optional filename from Content-Disposition. */
export async function apiDownload(
  path: string,
): Promise<{ blob: Blob; fileName: string }> {
  const headers = await mergeAuthHeaders({
    Accept: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
  const res = await fetch(`${base}${path}`, { headers });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `${res.status} ${res.statusText}`);
  }
  const blob = await res.blob();
  const disposition = res.headers.get("Content-Disposition");
  const match = disposition?.match(/filename="?([^";\n]+)"?/i);
  const fileName = match?.[1] ?? "download.xlsx";
  return { blob, fileName };
}
