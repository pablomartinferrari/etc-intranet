import { useMsal } from "@azure/msal-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Folder, Menu, Pencil } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link as RouterLink } from "react-router-dom";

import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { AddSharePointFolderButton } from "./AddSharePointFolderSheet";
import { ChatMarkdown } from "./ChatMarkdown";
import {
  lastSessionForProject,
  readChatSelection,
  writeChatSelection,
} from "./chatSelectionStorage";
import { FileTypeIcon, fileKindFromFormat, fileKindFromName } from "./fileTypeIcon";
import { ProjectRail, shareBadgeLabel } from "./ProjectRail";
import { ProjectSidePanel, type ProjectPanelTab } from "./ProjectSidePanel";
import { ShareProjectDialog } from "./ShareProjectDialog";
import {
  canEditProject,
  chatKnowledge,
  createProject,
  createPrompt,
  deleteProject,
  deletePrompt,
  downloadGeneratedFile,
  getChatCapabilities,
  getChatMessages,
  listChatSessions,
  listDocuments,
  listProjects,
  listPrompts,
  updateProject,
  updateChatSession,
  uploadDocumentsAsync,
  type ChatAttachment,
  type ChatMessage,
  type ChatSession,
  type Citation,
  type Prompt,
  type UploadQueueItem,
} from "./api/knowledge";

const ACTIVE_INGEST = new Set(["queued", "processing", "pending"]);

export default function KnowledgeChatWorkspace() {
  const queryClient = useQueryClient();
  const { accounts } = useMsal();
  const userKey =
    (accounts[0]?.idTokenClaims?.oid as string | undefined) ??
    accounts[0]?.localAccountId ??
    "anon";

  const fileInputRef = useRef<HTMLInputElement>(null);
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const hydratedRef = useRef(false);

  const [selectedProjectId, setSelectedProjectId] = useState<string | undefined>();
  const [panelTab, setPanelTab] = useState<ProjectPanelTab>("chats");
  const [sessionId, setSessionId] = useState<string | undefined>();
  const [pendingMessages, setPendingMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [uploadQueue, setUploadQueue] = useState<UploadQueueItem[]>([]);
  const [contextDocId, setContextDocId] = useState<string | undefined>();
  const [newProjectOpen, setNewProjectOpen] = useState(false);
  const [editProjectOpen, setEditProjectOpen] = useState(false);
  const [shareOpen, setShareOpen] = useState(false);
  const [deleteProjectOpen, setDeleteProjectOpen] = useState(false);
  const [newProjectName, setNewProjectName] = useState("");
  const [newProjectArea, setNewProjectArea] = useState("");
  const [newProjectInstructions, setNewProjectInstructions] = useState("");
  const [newPromptOpen, setNewPromptOpen] = useState(false);
  const [newPromptTitle, setNewPromptTitle] = useState("");
  const [newPromptContent, setNewPromptContent] = useState("");
  const [renameSessionOpen, setRenameSessionOpen] = useState(false);
  const [renameSessionId, setRenameSessionId] = useState<string | undefined>();
  const [renameSessionTitle, setRenameSessionTitle] = useState("");
  const [projectsSheetOpen, setProjectsSheetOpen] = useState(false);
  const [panelSheetOpen, setPanelSheetOpen] = useState(false);

  const scrollChatToBottom = useCallback((behavior: ScrollBehavior = "smooth") => {
    const container = messagesContainerRef.current;
    if (!container) return;
    container.scrollTo({ top: container.scrollHeight, behavior });
  }, []);

  const projectsQuery = useQuery({ queryKey: ["kb-projects"], queryFn: listProjects });
  const capabilitiesQuery = useQuery({
    queryKey: ["kb-chat-capabilities"],
    queryFn: getChatCapabilities,
  });

  useEffect(() => {
    if (!projectsQuery.data) return;
    const ids = new Set(projectsQuery.data.map((p) => p.id));
    if (!hydratedRef.current) {
      hydratedRef.current = true;
      const stored = readChatSelection(userKey);
      if (stored.selectedProjectId && ids.has(stored.selectedProjectId)) {
        setSelectedProjectId(stored.selectedProjectId);
        setSessionId(lastSessionForProject(stored, stored.selectedProjectId));
        return;
      }
      if (projectsQuery.data[0]) {
        setSelectedProjectId(projectsQuery.data[0].id);
      }
      return;
    }
    if (selectedProjectId && !ids.has(selectedProjectId)) {
      const next = projectsQuery.data[0];
      setSelectedProjectId(next?.id);
      setSessionId(next ? lastSessionForProject(readChatSelection(userKey), next.id) : undefined);
      setPendingMessages([]);
    }
  }, [projectsQuery.data, userKey, selectedProjectId]);

  useEffect(() => {
    if (!hydratedRef.current || !selectedProjectId) return;
    const current = readChatSelection(userKey);
    writeChatSelection(userKey, {
      selectedProjectId,
      sessionsByProject: {
        ...current.sessionsByProject,
        [selectedProjectId]: sessionId,
      },
    });
  }, [userKey, selectedProjectId, sessionId]);

  const selectedProject = useMemo(
    () => projectsQuery.data?.find((p) => p.id === selectedProjectId),
    [projectsQuery.data, selectedProjectId],
  );

  const sessionsQuery = useQuery({
    queryKey: ["kb-sessions", selectedProjectId],
    queryFn: () => listChatSessions(selectedProjectId!),
    enabled: !!selectedProjectId,
  });

  const promptsQuery = useQuery({
    queryKey: ["kb-prompts", selectedProjectId],
    queryFn: () => listPrompts(selectedProjectId),
    enabled: !!selectedProjectId,
  });

  const documentsQuery = useQuery({
    queryKey: ["kb-documents", selectedProjectId],
    queryFn: () => listDocuments(selectedProjectId!),
    enabled: !!selectedProjectId,
    refetchInterval: (query) => {
      const docs = query.state.data ?? [];
      const queueActive = uploadQueue.some(
        (u) => u.status === "uploading" || ACTIVE_INGEST.has(u.status),
      );
      const docsActive = docs.some((d) => ACTIVE_INGEST.has(d.ingestStatus));
      return queueActive || docsActive ? 3000 : false;
    },
  });

  const messagesQuery = useQuery({
    queryKey: ["kb-messages", sessionId],
    queryFn: ({ signal }) => getChatMessages(sessionId!, signal),
    enabled: !!sessionId,
  });

  const projectDocuments = useMemo(() => {
    const docs = documentsQuery.data ?? [];
    const knownIds = new Set(docs.map((d) => d.id));
    const pendingUploads = uploadQueue
      .filter((u) => u.documentId && !knownIds.has(u.documentId))
      .map((u) => ({
        id: u.documentId!,
        title: u.fileName,
        ingestStatus: u.status === "uploading" ? "queued" : u.status,
        ingestDetail: null as string | null,
        sourceType: "upload",
        docType: null as string | null,
        createdAt: new Date().toISOString(),
      }));
    return [...pendingUploads, ...docs];
  }, [documentsQuery.data, uploadQueue]);

  const readyDocs = useMemo(
    () => projectDocuments.filter((d) => d.ingestStatus === "completed"),
    [projectDocuments],
  );

  const hasActiveIngest = useMemo(
    () => projectDocuments.some((d) => ACTIVE_INGEST.has(d.ingestStatus)),
    [projectDocuments],
  );

  const webSearchEnabled = capabilitiesQuery.data?.webSearchEnabled ?? false;
  const hasReadyDocs = readyDocs.length > 0;
  const canEdit = canEditProject(selectedProject);
  const canChat = !!selectedProjectId && (hasReadyDocs || webSearchEnabled);

  const messages = useMemo(() => {
    const history = sessionId ? (messagesQuery.data ?? []) : [];
    return [...history, ...pendingMessages];
  }, [sessionId, messagesQuery.data, pendingMessages]);

  const messagesLoading = !!sessionId && messagesQuery.isPending;

  useEffect(() => {
    const docs = documentsQuery.data ?? [];
    setUploadQueue((prev) =>
      prev.filter((item) => {
        if (!item.documentId) return item.status === "uploading";
        return !docs.some((d) => d.id === item.documentId);
      }),
    );
  }, [documentsQuery.data]);

  const chatMutation = useMutation({
    mutationFn: async (vars: {
      text: string;
      projectId: string;
      sessionId?: string;
      contextDocId?: string;
    }) => {
      const result = await chatKnowledge(
        vars.text,
        vars.sessionId,
        vars.contextDocId,
        vars.projectId,
        "auto",
      );
      return { ...result, projectId: vars.projectId };
    },
    onMutate: (vars) => {
      const userMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: "user",
        content: vars.text,
        createdAt: new Date().toISOString(),
      };
      setPendingMessages((prev) => [...prev, userMsg]);
      setInput("");
      return { userMsg };
    },
    onSuccess: (data, _vars, ctx) => {
      const assistantMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: "assistant",
        content: data.answer,
        citations: data.citations,
        attachments: data.attachments?.length ? data.attachments : undefined,
        createdAt: new Date().toISOString(),
        generation: data.generation ?? undefined,
      };
      queryClient.setQueryData<ChatMessage[]>(["kb-messages", data.sessionId], (old) => {
        const prior = old ?? [];
        const withoutPendingUser = prior.filter((m) => m.id !== ctx?.userMsg.id);
        return [...withoutPendingUser, ...(ctx ? [ctx.userMsg] : []), assistantMsg];
      });
      setPendingMessages([]);
      if (selectedProjectId === data.projectId) {
        setSessionId(data.sessionId);
      }
      void queryClient.invalidateQueries({ queryKey: ["kb-sessions", data.projectId] });
    },
    onError: (err: Error) => {
      setPendingMessages((prev) => [
        ...prev,
        {
          id: crypto.randomUUID(),
          role: "assistant",
          content: `Sorry, something went wrong: ${err.message}`,
          createdAt: new Date().toISOString(),
        },
      ]);
    },
  });

  useEffect(() => {
    if (messages.length === 0 && !chatMutation.isPending) return;
    const frame = requestAnimationFrame(() => {
      requestAnimationFrame(() => scrollChatToBottom("smooth"));
    });
    return () => cancelAnimationFrame(frame);
  }, [messages, chatMutation.isPending, scrollChatToBottom]);

  const renameSessionMutation = useMutation({
    mutationFn: () => updateChatSession(renameSessionId!, { title: renameSessionTitle.trim() }),
    onSuccess: () => {
      setRenameSessionOpen(false);
      setRenameSessionId(undefined);
      setRenameSessionTitle("");
      void queryClient.invalidateQueries({ queryKey: ["kb-sessions", selectedProjectId] });
    },
  });

  const createProjectMutation = useMutation({
    mutationFn: () =>
      createProject(
        newProjectName,
        undefined,
        newProjectInstructions || undefined,
        newProjectArea || undefined,
      ),
    onSuccess: (project) => {
      setNewProjectOpen(false);
      setNewProjectName("");
      setNewProjectArea("");
      setNewProjectInstructions("");
      setSelectedProjectId(project.id);
      setSessionId(undefined);
      setPendingMessages([]);
      setInput("");
      setContextDocId(undefined);
      setUploadQueue([]);
      setPanelTab("chats");
      void queryClient.invalidateQueries({ queryKey: ["kb-projects"] });
    },
  });

  const updateProjectMutation = useMutation({
    mutationFn: () =>
      updateProject(selectedProjectId!, {
        name: newProjectName || undefined,
        instructions: newProjectInstructions,
        area: newProjectArea,
      }),
    onSuccess: () => {
      setEditProjectOpen(false);
      void queryClient.invalidateQueries({ queryKey: ["kb-projects"] });
    },
  });

  const deleteProjectMutation = useMutation({
    mutationFn: () => deleteProject(selectedProjectId!),
    onSuccess: () => {
      setDeleteProjectOpen(false);
      setSelectedProjectId(undefined);
      setSessionId(undefined);
      setPendingMessages([]);
      hydratedRef.current = true;
      void queryClient.invalidateQueries({ queryKey: ["kb-projects"] });
    },
  });

  const createPromptMutation = useMutation({
    mutationFn: () => createPrompt(newPromptTitle, newPromptContent, selectedProjectId!),
    onSuccess: () => {
      setNewPromptOpen(false);
      setNewPromptTitle("");
      setNewPromptContent("");
      void queryClient.invalidateQueries({ queryKey: ["kb-prompts", selectedProjectId] });
    },
  });

  const isMobileViewport = () =>
    typeof window !== "undefined" && window.matchMedia("(max-width: 767px)").matches;

  const openMobilePanel = (tab?: ProjectPanelTab) => {
    if (tab) setPanelTab(tab);
    if (isMobileViewport()) setPanelSheetOpen(true);
  };

  const processFiles = useCallback(
    async (files: FileList | File[]) => {
      if (!selectedProjectId || !canEdit) return;
      const list = Array.from(files);
      if (list.length === 0) return;
      setPanelTab("files");
      if (typeof window !== "undefined" && window.matchMedia("(max-width: 767px)").matches) {
        setPanelSheetOpen(true);
      }
      await uploadDocumentsAsync(list, selectedProjectId, setUploadQueue);
      void queryClient.invalidateQueries({ queryKey: ["kb-documents", selectedProjectId] });
    },
    [canEdit, queryClient, selectedProjectId],
  );

  const selectProject = (id: string) => {
    setProjectsSheetOpen(false);
    if (id === selectedProjectId) return;
    setSelectedProjectId(id);
    setInput("");
    setContextDocId(undefined);
    setUploadQueue([]);
    setPanelTab("chats");
    setPendingMessages([]);
    setSessionId(lastSessionForProject(readChatSelection(userKey), id));
  };

  const selectSession = (id: string) => {
    setPendingMessages([]);
    setSessionId(id);
    setContextDocId(undefined);
    setPanelSheetOpen(false);
  };

  const startNewChat = () => {
    setSessionId(undefined);
    setPendingMessages([]);
    setInput("");
    setContextDocId(undefined);
    setPanelSheetOpen(false);
  };

  useEffect(() => {
    const media = window.matchMedia("(min-width: 768px)");
    const onChange = () => {
      if (media.matches) {
        setProjectsSheetOpen(false);
        setPanelSheetOpen(false);
      }
    };
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, []);

  const openRenameSession = (session: ChatSession) => {
    setRenameSessionId(session.id);
    setRenameSessionTitle(session.title ?? "");
    setRenameSessionOpen(true);
  };

  const openEditProject = () => {
    if (!selectedProject) return;
    setNewProjectName(selectedProject.name);
    setNewProjectArea(selectedProject.area ?? "");
    setNewProjectInstructions(selectedProject.instructions ?? "");
    setEditProjectOpen(true);
  };

  const sendMessage = () => {
    const text = input.trim();
    if (!text || !canChat || !selectedProjectId || chatMutation.isPending || messagesLoading) return;
    void chatMutation.mutateAsync({
      text,
      projectId: selectedProjectId,
      sessionId,
      contextDocId,
    });
  };

  const hasProjects = (projectsQuery.data?.length ?? 0) > 0;
  const activeSession = useMemo(
    () => sessionsQuery.data?.find((s) => s.id === sessionId),
    [sessionsQuery.data, sessionId],
  );
  const shareLabel = selectedProject ? shareBadgeLabel(selectedProject) : null;

  const sidePanelProps = {
    selectedProject,
    panelTab,
    onPanelTabChange: setPanelTab,
    onEditProject: openEditProject,
    onShareProject: () => setShareOpen(true),
    onDeleteProject: () => setDeleteProjectOpen(true),
    onNewChat: startNewChat,
    sessions: sessionsQuery.data ?? [],
    sessionsLoading: sessionsQuery.isPending,
    sessionId,
    onSelectSession: selectSession,
    onRenameSession: openRenameSession,
    onPickFiles: () => fileInputRef.current?.click(),
    onDropFiles: (files: FileList) => void processFiles(files),
    hasActiveIngest,
    documents: projectDocuments,
    documentsLoading: documentsQuery.isPending,
    contextDocId,
    onSelectDocument: (id: string) => setContextDocId(contextDocId === id ? undefined : id),
    onSavePrompt: () => {
      setNewPromptContent(input);
      setNewPromptOpen(true);
    },
    prompts: promptsQuery.data ?? [],
    promptsLoading: promptsQuery.isPending,
    onUsePrompt: (prompt: Prompt) => {
      setInput(prompt.content);
      setPanelTab("chats");
      setPanelSheetOpen(false);
    },
    onDeletePrompt: (id: string) => {
      void deletePrompt(id).then(() =>
        queryClient.invalidateQueries({
          queryKey: ["kb-prompts", selectedProjectId],
        }),
      );
    },
  };

  return (
    <div className="flex h-dvh min-h-0 flex-col overflow-hidden bg-background md:flex-row">
      <input
        ref={fileInputRef}
        type="file"
        multiple
        accept=".pdf,.docx,.xlsx,.txt,.md,.html,.csv"
        className="hidden"
        onChange={(e) => {
          if (e.target.files) void processFiles(e.target.files);
          e.target.value = "";
        }}
      />

      <ProjectRail
        className="hidden md:flex"
        projects={projectsQuery.data ?? []}
        selectedProjectId={selectedProjectId}
        onSelectProject={selectProject}
        onNewProject={() => setNewProjectOpen(true)}
        showHomeLink
        loading={projectsQuery.isPending}
      />

      {!hasProjects ? (
        <main className="flex min-h-0 min-w-0 flex-1 flex-col">
          <div className="flex items-center gap-2 border-b px-3 py-2 md:hidden">
            <Button variant="ghost" size="icon" asChild>
              <RouterLink to="/" aria-label="Back to home">
                <ArrowLeft />
              </RouterLink>
            </Button>
            <h1 className="truncate text-base font-semibold">Chat</h1>
          </div>
          <div className="flex flex-1 flex-col items-center justify-center gap-4 p-6 text-center md:p-10">
            <Folder className="size-14 opacity-50" />
            <h3 className="text-lg font-semibold">Create your first project</h3>
            <p className="max-w-[420px] text-sm text-muted-foreground">
              Projects keep documents, chats, and prompts together. Group them by area and share
              with Entra users or groups when you are ready.
            </p>
            <Button size="lg" className="h-11 md:h-9" onClick={() => setNewProjectOpen(true)}>
              New project
            </Button>
          </div>
        </main>
      ) : (
        <>
          <ProjectSidePanel {...sidePanelProps} className="hidden md:flex" />

          <main className="flex min-h-0 min-w-0 flex-1 flex-col bg-background">
            <header className="sticky top-0 z-10 flex flex-col gap-2 border-b bg-background px-3 py-2 md:flex-row md:items-center md:justify-between md:px-6 md:py-3">
              <div className="flex items-center gap-2 md:hidden">
                <Button variant="ghost" size="icon" asChild>
                  <RouterLink to="/" aria-label="Back to home">
                    <ArrowLeft />
                  </RouterLink>
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  className="min-w-0 flex-1 justify-start"
                  onClick={() => setProjectsSheetOpen(true)}
                >
                  <Folder />
                  <span className="truncate">{selectedProject?.name ?? "Projects"}</span>
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  aria-label="Open chats, files, and prompts"
                  onClick={() => setPanelSheetOpen(true)}
                >
                  <Menu />
                </Button>
              </div>
              <div className="hidden min-w-0 md:flex md:flex-col">
                <div className="flex items-center gap-2">
                  <p className="truncate text-sm font-semibold">{selectedProject?.name}</p>
                  {shareLabel && (
                    <span className="rounded-full border px-2 py-0.5 text-[10px] text-muted-foreground">
                      {shareLabel}
                    </span>
                  )}
                </div>
                {selectedProject?.area && (
                  <p className="truncate text-xs text-muted-foreground">{selectedProject.area}</p>
                )}
              </div>
              <div className="flex min-w-0 flex-1 items-center gap-1">
                <h3 className="truncate text-base font-semibold md:text-lg">
                  {sessionId
                    ? (activeSession?.title ?? (messagesLoading ? "Loading chat" : "Chat"))
                    : "New chat"}
                </h3>
                {sessionId && (
                  <Button
                    variant="ghost"
                    size="icon"
                    title="Rename chat"
                    onClick={() => activeSession && openRenameSession(activeSession)}
                  >
                    <Pencil />
                  </Button>
                )}
              </div>
              <div className="hidden shrink-0 items-center gap-2 md:flex">
                {contextDocId && (
                  <Button variant="ghost" size="sm" onClick={() => setContextDocId(undefined)}>
                    Focused on one file — clear
                  </Button>
                )}
                <Button variant="ghost" size="sm" asChild>
                  <RouterLink to="/knowledge/sources">Manage sources</RouterLink>
                </Button>
              </div>
            </header>

            <div
              className="flex min-h-0 flex-1 flex-col gap-5 overflow-x-hidden overflow-y-auto p-4 md:p-6"
              ref={messagesContainerRef}
            >
              {messagesLoading && (
                <div className="flex flex-col gap-3">
                  <Skeleton className="h-16 w-[min(100%,420px)] self-end rounded-[18px]" />
                  <Skeleton className="h-24 w-[min(100%,560px)] rounded-[18px]" />
                  <Skeleton className="h-16 w-[min(100%,480px)] rounded-[18px]" />
                </div>
              )}

              {!messagesLoading && messages.length === 0 && (
                <div className="m-auto flex max-w-[480px] flex-col items-center gap-4 px-2 text-center">
                  <p className="text-base font-semibold md:text-lg">
                    {hasReadyDocs
                      ? "Ask about this project's documents"
                      : webSearchEnabled
                        ? "Ask a question — we'll search your project files and the web when needed"
                        : "Add files to this project to get started"}
                  </p>
                  {hasReadyDocs && (
                    <span className="text-xs text-muted-foreground">
                      Say &quot;export to Excel&quot; or &quot;create a Word summary&quot; to generate a
                      downloadable file from your project sources.
                    </span>
                  )}
                  {!hasReadyDocs && canEdit && (
                    <div className="flex flex-wrap justify-center gap-2">
                      <Button variant="outline" onClick={() => openMobilePanel("files")}>
                        Go to project files
                      </Button>
                      <AddSharePointFolderButton variant="outline">
                        Add SharePoint folder
                      </AddSharePointFolderButton>
                    </div>
                  )}
                  {(promptsQuery.data ?? []).length > 0 && canChat && (
                    <div className="mt-2 flex flex-col gap-2">
                      <span className="text-xs text-muted-foreground">Try a saved prompt</span>
                      <div className="flex flex-wrap justify-center gap-2">
                        {(promptsQuery.data ?? []).slice(0, 3).map((p) => (
                          <Button
                            key={p.id}
                            variant="outline"
                            size="sm"
                            onClick={() => setInput(p.content)}
                          >
                            {p.title}
                          </Button>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}

              {!messagesLoading &&
                messages.map((msg) => <MessageBubble key={msg.id} message={msg} />)}

              {chatMutation.isPending && (
                <div className="flex justify-start">
                  <div className="max-w-[min(100%,720px)] rounded-[18px] border bg-muted px-3.5 py-3 leading-normal md:px-[18px] md:py-3.5">
                    <Spinner size="sm" label="Thinking..." />
                  </div>
                </div>
              )}
              <div ref={messagesEndRef} />
            </div>

            <footer className="sticky bottom-0 z-10 border-t bg-background px-3 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] md:px-6 md:pt-4 md:pb-6">
              {contextDocId && (
                <div className="mb-2 flex justify-center md:hidden">
                  <Button variant="ghost" size="sm" onClick={() => setContextDocId(undefined)}>
                    Focused on one file — clear
                  </Button>
                </div>
              )}
              {!selectedProjectId && (
                <span className="mb-2 block text-center text-xs text-muted-foreground">
                  Select a project to continue
                </span>
              )}
              {selectedProjectId && !canChat && (
                <span className="mb-2 block text-center text-xs text-muted-foreground">
                  {webSearchEnabled
                    ? "Upload a file and wait for indexing, or ask a general question"
                    : "Upload a file and wait for completed status, or enable web search in API settings"}
                </span>
              )}
              {canChat && hasActiveIngest && (
                <span className="mb-2 block text-center text-xs text-muted-foreground">
                  Some files still indexing — answers use completed files only
                </span>
              )}
              <div className="mx-auto flex w-full max-w-[800px] items-end gap-2 rounded-xl border bg-muted p-2.5 md:p-3">
                <Textarea
                  className="min-h-11 min-w-0 flex-1 resize-none md:min-h-[72px]"
                  rows={2}
                  placeholder={
                    canChat
                      ? `Message ${selectedProject?.name ?? "project"}…`
                      : "Add project files or enable web search to chat"
                  }
                  value={input}
                  disabled={!canChat || chatMutation.isPending || messagesLoading}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" && !e.shiftKey) {
                      e.preventDefault();
                      sendMessage();
                    }
                  }}
                />
                <Button
                  className="h-11 shrink-0 md:h-8"
                  disabled={!canChat || !input.trim() || chatMutation.isPending || messagesLoading}
                  onClick={sendMessage}
                >
                  Send
                </Button>
              </div>
            </footer>
          </main>
        </>
      )}

      <Sheet open={projectsSheetOpen} onOpenChange={setProjectsSheetOpen}>
        <SheetContent side="left" className="w-full gap-0 p-0 sm:max-w-xs" showCloseButton>
          <SheetHeader>
            <SheetTitle>Projects</SheetTitle>
          </SheetHeader>
          <ProjectRail
            className="min-h-0 flex-1"
            projects={projectsQuery.data ?? []}
            selectedProjectId={selectedProjectId}
            onSelectProject={selectProject}
            onNewProject={() => {
              setProjectsSheetOpen(false);
              setNewProjectOpen(true);
            }}
            showHomeLink={false}
            loading={projectsQuery.isPending}
          />
        </SheetContent>
      </Sheet>

      <Sheet open={panelSheetOpen} onOpenChange={setPanelSheetOpen}>
        <SheetContent side="left" className="w-full gap-0 p-0 sm:max-w-sm" showCloseButton>
          <SheetHeader className="sr-only">
            <SheetTitle>Chats, files, and prompts</SheetTitle>
          </SheetHeader>
          <ProjectSidePanel {...sidePanelProps} compactHeader className="h-full min-h-0" />
        </SheetContent>
      </Sheet>

      <ProjectDialog
        open={newProjectOpen}
        title="New project"
        name={newProjectName}
        area={newProjectArea}
        instructions={newProjectInstructions}
        onNameChange={setNewProjectName}
        onAreaChange={setNewProjectArea}
        onInstructionsChange={setNewProjectInstructions}
        onClose={() => setNewProjectOpen(false)}
        onSubmit={() => void createProjectMutation.mutateAsync()}
        submitLabel="Create"
        pending={createProjectMutation.isPending}
        disabled={!newProjectName.trim()}
      />

      <ProjectDialog
        open={editProjectOpen}
        title="Edit project"
        name={newProjectName}
        area={newProjectArea}
        instructions={newProjectInstructions}
        onNameChange={setNewProjectName}
        onAreaChange={setNewProjectArea}
        onInstructionsChange={setNewProjectInstructions}
        onClose={() => setEditProjectOpen(false)}
        onSubmit={() => void updateProjectMutation.mutateAsync()}
        submitLabel="Save"
        pending={updateProjectMutation.isPending}
        disabled={!newProjectName.trim()}
      />

      <ShareProjectDialog
        open={shareOpen}
        project={selectedProject}
        onClose={() => setShareOpen(false)}
      />

      <AlertDialog open={deleteProjectOpen} onOpenChange={setDeleteProjectOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete {selectedProject?.name}?</AlertDialogTitle>
            <AlertDialogDescription>
              This removes the project for everyone it was shared with. Chat history in the project
              is no longer listed from the sidebar.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              disabled={deleteProjectMutation.isPending}
              onClick={() => void deleteProjectMutation.mutateAsync()}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <Dialog open={newPromptOpen} onOpenChange={setNewPromptOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Save prompt for {selectedProject?.name}</DialogTitle>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <Input
              placeholder="Short title"
              value={newPromptTitle}
              onChange={(e) => setNewPromptTitle(e.target.value)}
            />
            <Textarea
              placeholder="Full prompt text"
              value={newPromptContent}
              onChange={(e) => setNewPromptContent(e.target.value)}
              rows={4}
            />
          </div>
          <DialogFooter>
            <Button variant="secondary" onClick={() => setNewPromptOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={
                !newPromptTitle.trim() ||
                !newPromptContent.trim() ||
                createPromptMutation.isPending
              }
              onClick={() => void createPromptMutation.mutateAsync()}
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={renameSessionOpen}
        onOpenChange={(open) => {
          if (!open) setRenameSessionOpen(false);
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Rename chat</DialogTitle>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <Input
              placeholder="Chat name"
              value={renameSessionTitle}
              onChange={(e) => setRenameSessionTitle(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="secondary" onClick={() => setRenameSessionOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={!renameSessionTitle.trim() || renameSessionMutation.isPending}
              onClick={() => void renameSessionMutation.mutateAsync()}
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ProjectDialog({
  open,
  title,
  name,
  area,
  instructions,
  onNameChange,
  onAreaChange,
  onInstructionsChange,
  onClose,
  onSubmit,
  submitLabel,
  pending,
  disabled,
}: {
  open: boolean;
  title: string;
  name: string;
  area: string;
  instructions: string;
  onNameChange: (v: string) => void;
  onAreaChange: (v: string) => void;
  onInstructionsChange: (v: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  submitLabel: string;
  pending: boolean;
  disabled: boolean;
}) {
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="project-name">Name</Label>
            <Input
              id="project-name"
              placeholder="Project name"
              value={name}
              onChange={(e) => onNameChange(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="project-area">Area (optional)</Label>
            <Input
              id="project-area"
              placeholder="e.g. Finance, Sales, Operations"
              maxLength={80}
              value={area}
              onChange={(e) => onAreaChange(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="project-instructions">Instructions</Label>
            <Textarea
              id="project-instructions"
              placeholder="How should the assistant behave in this project?"
              value={instructions}
              onChange={(e) => onInstructionsChange(e.target.value)}
              rows={4}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button disabled={disabled || pending} onClick={onSubmit}>
            {submitLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const isUser = message.role === "user";

  return (
    <div className={isUser ? "flex justify-end" : "flex justify-start"}>
      <div
        className={`min-w-0 max-w-[min(100%,720px)] break-words rounded-[18px] px-3.5 py-3 leading-normal md:px-[18px] md:py-3.5 ${
          isUser ? "bg-primary text-primary-foreground" : "border bg-muted"
        }`}
      >
        {isUser ? (
          <p className="whitespace-pre-wrap break-words text-sm">{message.content}</p>
        ) : (
          <ChatMarkdown content={message.content} className="w-full min-w-0" />
        )}
        {!isUser && message.generation?.isFallback && (
          <p className="mt-2 text-[11px] leading-4 text-muted-foreground">
            Answered by hosted {message.generation.provider} ({message.generation.model}) —
            local KB model was offline
          </p>
        )}
        {!isUser && message.attachments && message.attachments.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-2">
            {message.attachments.map((attachment) => (
              <AttachmentDownload key={attachment.id} attachment={attachment} />
            ))}
          </div>
        )}
        {!isUser && message.citations && message.citations.length > 0 && (
          <div className="mt-3.5 flex flex-col gap-2">
            <span className="text-xs font-semibold">Sources</span>
            {message.citations.map((c, i) => (
              <CitationChip
                key={c.documentId ?? c.url ?? `${c.title}-${i}`}
                citation={c}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function AttachmentDownload({ attachment }: { attachment: ChatAttachment }) {
  const kind =
    fileKindFromFormat(attachment.format) !== "generic"
      ? fileKindFromFormat(attachment.format)
      : fileKindFromName(attachment.filename);

  return (
    <Button
      variant="secondary"
      size="sm"
      className="max-w-full"
      onClick={() => void downloadGeneratedFile(attachment.id, attachment.filename)}
    >
      <FileTypeIcon kind={kind} className="size-5 shrink-0 text-muted-foreground" />
      {attachment.filename}
    </Button>
  );
}

function CitationChip({ citation }: { citation: Citation }) {
  const isWeb = citation.type === "web" || !!citation.url;
  const iconKind = isWeb ? "web" : fileKindFromName(citation.title);

  return (
    <div className="rounded-md bg-card px-3 py-2.5">
      <div className="flex min-w-0 items-center gap-2">
        <FileTypeIcon kind={iconKind} className="size-5 shrink-0 text-muted-foreground" />
        {isWeb && citation.url ? (
          <a
            href={citation.url}
            target="_blank"
            rel="noopener noreferrer"
            className="min-w-0 break-words text-xs font-semibold text-primary"
          >
            {citation.title}
          </a>
        ) : (
          <p className="min-w-0 break-words text-xs font-semibold">{citation.title}</p>
        )}
      </div>
      <span className="ml-7 break-words text-xs text-muted-foreground">{citation.snippet}</span>
    </div>
  );
}
