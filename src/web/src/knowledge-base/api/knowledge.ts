import { apiDelete, apiDownload, apiGet, apiPatch, apiPost } from "../../multifamily-lbp/api/client";

export type SearchResultItem = {
  documentId: string;
  title: string;
  sourceUri: string | null;
  docType: string | null;
  sourceType: string;
  score: number;
  snippet: string;
};

export type Citation = {
  type?: "document" | "web";
  documentId?: string | null;
  title: string;
  sourceUri?: string | null;
  url?: string | null;
  snippet: string;
};

export type SearchMode = "auto" | "documents" | "web" | "both";

export type ChatCapabilities = {
  webSearchEnabled: boolean;
  searchModes: SearchMode[];
  fileExportEnabled: boolean;
  exportFormats: string[];
};

export type ChatAttachment = {
  id: string;
  filename: string;
  mimeType: string;
  format: string;
};

export type ChatResponse = {
  sessionId: string;
  answer: string;
  citations: Citation[];
  sourcesUsed: string;
  attachments: ChatAttachment[];
};

export type ChatMessage = {
  id: string;
  role: "user" | "assistant";
  content: string;
  citations?: Citation[];
  attachments?: ChatAttachment[];
  createdAt: string;
};

export type ChatSession = {
  id: string;
  projectId: string | null;
  title: string | null;
  createdAt: string;
  updatedAt: string;
};

export type DocumentListItem = {
  id: string;
  title: string;
  sourceType: string;
  docType: string | null;
  ingestStatus: string;
  ingestDetail: string | null;
  createdAt: string;
  projectId?: string | null;
};

export type UploadEnqueueResponse = {
  documentId: string;
  jobId: string;
  status: string;
  message: string;
};

export type Project = {
  id: string;
  name: string;
  description: string | null;
  instructions: string | null;
  createdAt: string;
  updatedAt: string;
};

export type Prompt = {
  id: string;
  projectId: string | null;
  title: string;
  content: string;
  createdAt: string;
  updatedAt: string;
};

export type UploadQueueItem = {
  id: string;
  fileName: string;
  documentId?: string;
  status: "uploading" | "queued" | "processing" | "completed" | "failed";
  error?: string;
};

export function getChatCapabilities(): Promise<ChatCapabilities> {
  return apiGet<ChatCapabilities>("/kb/chat/capabilities");
}

export function chatKnowledge(
  query: string,
  sessionId?: string,
  documentId?: string,
  projectId?: string,
  searchMode: SearchMode = "auto",
): Promise<ChatResponse> {
  return apiPost<ChatResponse>("/kb/chat", {
    query,
    sessionId,
    documentId,
    projectId,
    searchMode,
  });
}

export function listChatSessions(projectId?: string): Promise<ChatSession[]> {
  const q = projectId ? `?projectId=${encodeURIComponent(projectId)}` : "";
  return apiGet<ChatSession[]>(`/kb/chat/sessions${q}`);
}

export function updateChatSession(
  sessionId: string,
  patch: { title: string },
): Promise<ChatSession> {
  return apiPatch<ChatSession>(`/kb/chat/sessions/${sessionId}`, patch);
}

export function getChatMessages(sessionId: string): Promise<ChatMessage[]> {
  return apiGet<ChatMessage[]>(`/kb/chat/sessions/${sessionId}/messages`);
}

export async function downloadGeneratedFile(
  fileId: string,
  fallbackFilename: string,
): Promise<void> {
  const { blob, fileName } = await apiDownload(`/kb/generated/${fileId}/download`);
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName || fallbackFilename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function listDocuments(projectId?: string): Promise<DocumentListItem[]> {
  const q = projectId ? `?projectId=${encodeURIComponent(projectId)}` : "";
  return apiGet<DocumentListItem[]>(`/kb/documents${q}`);
}

export function uploadDocument(file: File, projectId?: string): Promise<UploadEnqueueResponse> {
  const form = new FormData();
  form.append("file", file);
  const q = projectId ? `?projectId=${encodeURIComponent(projectId)}` : "";
  return apiPost<UploadEnqueueResponse>(`/kb/ingest/upload${q}`, form);
}

export function listProjects(): Promise<Project[]> {
  return apiGet<Project[]>("/kb/projects");
}

export function createProject(
  name: string,
  description?: string,
  instructions?: string,
): Promise<Project> {
  return apiPost<Project>("/kb/projects", { name, description, instructions });
}

export function updateProject(
  id: string,
  patch: { name?: string; description?: string; instructions?: string },
): Promise<Project> {
  return apiPatch<Project>(`/kb/projects/${id}`, patch);
}

export function deleteProject(id: string): Promise<void> {
  return apiDelete(`/kb/projects/${id}`);
}

export function listPrompts(projectId?: string): Promise<Prompt[]> {
  const q = projectId ? `?projectId=${encodeURIComponent(projectId)}` : "";
  return apiGet<Prompt[]>(`/kb/prompts${q}`);
}

export function createPrompt(
  title: string,
  content: string,
  projectId?: string,
): Promise<Prompt> {
  return apiPost<Prompt>("/kb/prompts", { title, content, projectId });
}

export function deletePrompt(id: string): Promise<void> {
  return apiDelete(`/kb/prompts/${id}`);
}

export async function uploadDocumentsAsync(
  files: File[],
  projectId: string | undefined,
  onItemUpdate: (items: UploadQueueItem[]) => void,
): Promise<UploadQueueItem[]> {
  const items: UploadQueueItem[] = files.map((file) => ({
    id: crypto.randomUUID(),
    fileName: file.name,
    status: "uploading",
  }));
  onItemUpdate([...items]);

  await Promise.all(
    files.map(async (file, index) => {
      try {
        const result = await uploadDocument(file, projectId);
        items[index] = {
          ...items[index],
          documentId: result.documentId,
          status: "queued",
        };
      } catch (err) {
        items[index] = {
          ...items[index],
          status: "failed",
          error: err instanceof Error ? err.message : "upload failed",
        };
      }
      onItemUpdate([...items]);
    }),
  );

  return items;
}
