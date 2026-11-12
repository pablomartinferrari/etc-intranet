import { getApiAuthHeaders, hasApiAuth } from "@mf/api/client";

const base = import.meta.env.VITE_API_BASE ?? "/api";

export type WorkspaceRole = "owner" | "editor" | "viewer";

export type WorkspaceCapabilities = {
  enabled: boolean;
  allowAnonymous: boolean;
  useGraphCalendar: boolean;
  useGraphMail: boolean;
  seedDemoData: boolean;
  shareDemoCatalog: boolean;
  hostedLlmConfigured: boolean;
  directorySearchConfigured: boolean;
  searchProvider: string;
  realtimeProvider: string;
  chatModel: string;
  repositoryRoot: string;
  demoUserOid: string;
  demoUserName: string;
};

export type DayItem = {
  id: string;
  title: string;
  detail?: string | null;
  when?: string | null;
  href?: string | null;
  end?: string | null;
  organizer?: string | null;
  attendees?: string[] | null;
  location?: string | null;
  teamsLink?: string | null;
  bodyPreview?: string | null;
};

export type DaySection = { id: string; title: string; items: DayItem[] };

export type MeetingBrief = {
  text: string;
  source: string;
  highlights: string[];
};

export type CalendarStatus = {
  state: string;
  message?: string | null;
};

export type MailStatus = {
  state: string;
  message?: string | null;
};

export type DaySummary = {
  greeting: string;
  userDisplayName: string;
  date: string;
  timeZone: string;
  sections: DaySection[];
  sources: string[];
  brief?: MeetingBrief | null;
  calendar: CalendarStatus;
  mail: MailStatus;
};

export type WorkspaceProject = {
  id: string;
  name: string;
  instructions?: string | null;
  role: WorkspaceRole;
  chatCount: number;
  fileCount: number;
  updatedAt: string;
};

export type WorkspaceChat = {
  id: string;
  projectId?: string | null;
  projectName?: string | null;
  folderId?: string | null;
  folderName?: string | null;
  folderPath?: string | null;
  documentId?: string | null;
  documentName?: string | null;
  title: string;
  visibility: string;
  realtimeCollaboration: boolean;
  role: WorkspaceRole;
  shareCount: number;
  updatedAt: string;
  lastActivityLabel?: string | null;
  myShareId?: string | null;
};

export type RepoFolder = {
  id: string;
  parentId?: string | null;
  name: string;
  path: string;
  isPublic: boolean;
  isIndexed: boolean;
  childCount: number;
  chatCount?: number;
  createdByName?: string | null;
  updatedAt?: string | null;
};

export type WorkspaceMe = { oid: string; displayName: string; email?: string | null };

export type SidebarPayload = {
  me: WorkspaceMe;
  projects: WorkspaceProject[];
  myChats: WorkspaceChat[];
  sharedWithMe: WorkspaceChat[];
  sharedByMe: WorkspaceChat[];
  scopedChats: WorkspaceChat[];
  repository: RepoFolder[];
};

export type Citation = {
  type: string;
  title: string;
  snippet: string;
  documentId?: string | null;
  path?: string | null;
  url?: string | null;
};

export type ChatMessage = {
  id: string;
  chatId: string;
  role: "user" | "assistant";
  authorOid: string;
  authorName: string;
  content: string;
  citations: Citation[];
  createdAt: string;
};

export type ChatShare = {
  id: string;
  principalType: string;
  principalOid: string;
  principalDisplayName: string;
  principalEmail?: string | null;
  role: WorkspaceRole;
  createdAt: string;
};

export type DirectoryPrincipal = {
  type: string;
  oid: string;
  displayName: string;
  email?: string | null;
  memberCount?: number | null;
};

export type RepoDocument = {
  id: string;
  folderId: string;
  name: string;
  fileType: string;
  mimeType: string;
  sizeBytes: number;
  ownerName: string;
  isIndexed: boolean;
  isPublic: boolean;
  path: string;
  modifiedAt: string;
  previewText?: string | null;
  extractionStatus?: string | null;
  extractionNote?: string | null;
};

export type RepoBrowse = {
  folder: RepoFolder;
  breadcrumbs: RepoFolder[];
  children: RepoFolder[];
  documents: RepoDocument[];
  selected?: RepoDocument | null;
};

export type RepoSearchHit = {
  documentId: string;
  folderId: string;
  title: string;
  fileType: string;
  path: string;
  snippet: string;
  highlights: string[];
};

export type RepoSearchResponse = {
  query: string;
  folderId?: string | null;
  folderPath?: string | null;
  scoped?: boolean;
  total: number;
  hits: RepoSearchHit[];
};

export type PresenceUser = {
  oid: string;
  displayName: string;
  initial: string;
  typing: boolean;
};

function userTimezone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  } catch {
    return "UTC";
  }
}

async function authHeaders(extra?: HeadersInit): Promise<HeadersInit> {
  const defaults = {
    Accept: "application/json",
    "X-User-Timezone": userTimezone(),
    ...extra,
  };
  if (hasApiAuth()) {
    try {
      const auth = await getApiAuthHeaders();
      return { ...defaults, ...auth, ...extra };
    } catch {
      /* fall through to anonymous POC mode */
    }
  }
  return defaults;
}

async function readError(res: Response): Promise<string> {
  const text = await res.text();
  try {
    const json = JSON.parse(text) as { message?: string };
    return json.message || text || `${res.status}`;
  } catch {
    return text || `${res.status} ${res.statusText}`;
  }
}

async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(`${base}${path}`, { headers: await authHeaders(), signal });
  if (!res.ok) throw new Error(await readError(res));
  return res.json() as Promise<T>;
}

async function apiSend<T>(method: string, path: string, body?: object): Promise<T> {
  const res = await fetch(`${base}${path}`, {
    method,
    headers: await authHeaders({ "Content-Type": "application/json" }),
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(await readError(res));
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const workspaceApi = {
  capabilities: () => apiGet<WorkspaceCapabilities>("/workspace/capabilities"),
  day: () => apiGet<DaySummary>("/workspace/day"),
  sidebar: () => apiGet<SidebarPayload>("/workspace/sidebar"),
  project: (id: string) => apiGet<WorkspaceProject>(`/workspace/projects/${id}`),
  projectFiles: (id: string) => apiGet<RepoDocument[]>(`/workspace/projects/${id}/files`),
  projectChats: (id: string) => apiGet<WorkspaceChat[]>(`/workspace/projects/${id}/chats`),
  createProject: (body: { name: string; instructions?: string }) =>
    apiSend<WorkspaceProject>("POST", "/workspace/projects", body),
  updateProject: (id: string, body: { name?: string; instructions?: string; linkedDocumentIds?: string[] }) =>
    apiSend<WorkspaceProject>("PATCH", `/workspace/projects/${id}`, body),
  chat: (id: string) => apiGet<WorkspaceChat>(`/workspace/chats/${id}`),
  messages: (id: string) => apiGet<ChatMessage[]>(`/workspace/chats/${id}/messages`),
  createChat: (body: {
    title?: string;
    projectId?: string | null;
    folderId?: string | null;
    documentId?: string | null;
    realtimeCollaboration?: boolean;
  }) => apiSend<WorkspaceChat>("POST", "/workspace/chats", body),
  updateChat: (
    id: string,
    body: {
      title?: string;
      projectId?: string | null;
      folderId?: string | null;
      documentId?: string | null;
      realtimeCollaboration?: boolean;
    },
  ) => apiSend<WorkspaceChat>("PATCH", `/workspace/chats/${id}`, body),
  deleteChat: async (id: string) => {
    const res = await fetch(`${base}/workspace/chats/${id}`, {
      method: "DELETE",
      headers: await authHeaders(),
    });
    if (!res.ok) throw new Error(await readError(res));
  },
  leaveChat: async (id: string) => {
    const res = await fetch(`${base}/workspace/chats/${id}/shares/me`, {
      method: "DELETE",
      headers: await authHeaders(),
    });
    if (!res.ok) throw new Error(await readError(res));
  },
  send: (id: string, text: string) =>
    apiSend<{ user: ChatMessage; assistant: ChatMessage }>("POST", `/workspace/chats/${id}/messages`, { text }),
  shares: (id: string) => apiGet<ChatShare[]>(`/workspace/chats/${id}/shares`),
  addShare: (id: string, body: { principalType: string; principalOid: string; role: string; displayName?: string; email?: string }) =>
    apiSend<ChatShare>("POST", `/workspace/chats/${id}/shares`, body),
  removeShare: async (chatId: string, shareId: string) => {
    const res = await fetch(`${base}/workspace/chats/${chatId}/shares/${shareId}`, {
      method: "DELETE",
      headers: await authHeaders(),
    });
    if (!res.ok) throw new Error(await readError(res));
  },
  directory: (q: string) => apiGet<DirectoryPrincipal[]>(`/workspace/directory/search?q=${encodeURIComponent(q)}`),
  browse: (folderId?: string, documentId?: string) => {
    const params = new URLSearchParams();
    if (folderId) params.set("folderId", folderId);
    if (documentId) params.set("documentId", documentId);
    const qs = params.toString();
    return apiGet<RepoBrowse>(`/workspace/repo${qs ? `?${qs}` : ""}`);
  },
  search: (q: string, folderId?: string, types?: string[], modified?: string) => {
    const params = new URLSearchParams({ q });
    if (folderId) params.set("folderId", folderId);
    if (types?.length) params.set("types", types.join(","));
    if (modified) params.set("modified", modified);
    return apiGet<RepoSearchResponse>(`/workspace/search?${params}`);
  },
  upload: async (folderId: string, file: File, onProgress?: (percent: number) => void) => {
    const data = new FormData();
    data.append("file", file);
    const headers = await authHeaders();
    return new Promise<RepoDocument>((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open("POST", `${base}/workspace/repo/${folderId}/upload`);
      const record = headers as Record<string, string>;
      for (const [key, value] of Object.entries(record)) {
        if (!value || key.toLowerCase() === "content-type") continue;
        xhr.setRequestHeader(key, value);
      }
      xhr.upload.onprogress = (event) => {
        if (!onProgress || !event.lengthComputable) return;
        onProgress(Math.round((event.loaded / event.total) * 100));
      };
      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          try {
            resolve(JSON.parse(xhr.responseText) as RepoDocument);
          } catch {
            reject(new Error("Upload succeeded but the response was not JSON."));
          }
          return;
        }
        try {
          const json = JSON.parse(xhr.responseText) as { message?: string };
          reject(new Error(json.message || xhr.responseText || `${xhr.status}`));
        } catch {
          reject(new Error(xhr.responseText || `${xhr.status} ${xhr.statusText}`));
        }
      };
      xhr.onerror = () => reject(new Error("Upload failed."));
      xhr.send(data);
    });
  },
  folderChats: (folderId: string) => apiGet<WorkspaceChat[]>(`/workspace/repo/${folderId}/chats`),
  createFolder: (parentId: string, name: string) =>
    apiSend<RepoFolder>("POST", `/workspace/repo/${parentId}/folders`, { name }),
  renameFolder: (folderId: string, name: string) =>
    apiSend<RepoFolder>("PATCH", `/workspace/repo/folders/${folderId}`, { name }),
  moveFolder: (folderId: string, parentId: string) =>
    apiSend<RepoFolder>("PATCH", `/workspace/repo/folders/${folderId}`, { parentId }),
  deleteFolder: async (folderId: string, recursive = false) => {
    const res = await fetch(`${base}/workspace/repo/folders/${folderId}?recursive=${recursive}`, {
      method: "DELETE",
      headers: await authHeaders(),
    });
    if (!res.ok) throw new Error(await readError(res));
  },
  renameDocument: (documentId: string, name: string) =>
    apiSend<RepoDocument>("PATCH", `/workspace/repo/documents/${documentId}`, { name }),
  moveDocument: (documentId: string, folderId: string) =>
    apiSend<RepoDocument>("PATCH", `/workspace/repo/documents/${documentId}`, { folderId }),
  deleteDocument: async (documentId: string) => {
    const res = await fetch(`${base}/workspace/repo/documents/${documentId}`, {
      method: "DELETE",
      headers: await authHeaders(),
    });
    if (!res.ok) throw new Error(await readError(res));
  },
  reindexDocument: (documentId: string) =>
    apiSend<RepoDocument>("POST", `/workspace/repo/documents/${documentId}/reindex`),
};

export function isAgenticEnabled(): boolean {
  return import.meta.env.VITE_FEATURE_AGENTIC_WORKSPACE !== "false";
}
