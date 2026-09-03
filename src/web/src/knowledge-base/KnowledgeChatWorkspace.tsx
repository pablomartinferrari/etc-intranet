import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  FileText,
  Folder,
  Lightbulb,
  MessageSquare,
  Pencil,
  Plus,
  Trash2,
  Upload,
} from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link as RouterLink } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import {
  chatKnowledge,
  createProject,
  createPrompt,
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
  type ChatMessage,
  type ChatAttachment,
  type ChatSession,
  type Citation,
  type Project,
  type Prompt,
  type UploadQueueItem,
} from "./api/knowledge";
import { FileTypeIcon, fileKindFromFormat, fileKindFromName } from "./fileTypeIcon";
import { ChatMarkdown } from "./ChatMarkdown";

const ACTIVE_INGEST = new Set(["queued", "processing", "pending"]);

type ProjectPanelTab = "chats" | "files" | "prompts";

export default function KnowledgeChatWorkspace() {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const [selectedProjectId, setSelectedProjectId] = useState<string | undefined>();
  const [panelTab, setPanelTab] = useState<ProjectPanelTab>("chats");
  const [sessionId, setSessionId] = useState<string | undefined>();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [uploadQueue, setUploadQueue] = useState<UploadQueueItem[]>([]);
  const [contextDocId, setContextDocId] = useState<string | undefined>();
  const [newProjectOpen, setNewProjectOpen] = useState(false);
  const [editProjectOpen, setEditProjectOpen] = useState(false);
  const [newProjectName, setNewProjectName] = useState("");
  const [newProjectInstructions, setNewProjectInstructions] = useState("");
  const [newPromptOpen, setNewPromptOpen] = useState(false);
  const [newPromptTitle, setNewPromptTitle] = useState("");
  const [newPromptContent, setNewPromptContent] = useState("");
  const [renameSessionOpen, setRenameSessionOpen] = useState(false);
  const [renameSessionId, setRenameSessionId] = useState<string | undefined>();
  const [renameSessionTitle, setRenameSessionTitle] = useState("");

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
    if (!selectedProjectId && projectsQuery.data && projectsQuery.data.length > 0) {
      setSelectedProjectId(projectsQuery.data[0].id);
    }
  }, [projectsQuery.data, selectedProjectId]);

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

  const canChat = !!selectedProjectId && (hasReadyDocs || webSearchEnabled);

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
    mutationFn: async (text: string) => {
      if (!selectedProjectId) throw new Error("Select a project first.");
      const userMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: "user",
        content: text,
        createdAt: new Date().toISOString(),
      };
      setMessages((prev) => [...prev, userMsg]);
      setInput("");
      return chatKnowledge(text, sessionId, contextDocId, selectedProjectId, "auto");
    },
    onSuccess: (data) => {
      setSessionId(data.sessionId);
      setMessages((prev) => [
        ...prev,
        {
          id: crypto.randomUUID(),
          role: "assistant",
          content: data.answer,
          citations: data.citations,
          attachments: data.attachments?.length ? data.attachments : undefined,
          createdAt: new Date().toISOString(),
          generation: data.generation ?? undefined,
        },
      ]);
      void queryClient.invalidateQueries({ queryKey: ["kb-sessions", selectedProjectId] });
    },
    onError: (err: Error) => {
      setMessages((prev) => [
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
    if (messages.length === 0) return;
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
      createProject(newProjectName, undefined, newProjectInstructions || undefined),
    onSuccess: (project) => {
      setNewProjectOpen(false);
      setNewProjectName("");
      setNewProjectInstructions("");
      setSelectedProjectId(project.id);
      void queryClient.invalidateQueries({ queryKey: ["kb-projects"] });
    },
  });

  const updateProjectMutation = useMutation({
    mutationFn: () =>
      updateProject(selectedProjectId!, {
        name: newProjectName || undefined,
        instructions: newProjectInstructions,
      }),
    onSuccess: () => {
      setEditProjectOpen(false);
      void queryClient.invalidateQueries({ queryKey: ["kb-projects"] });
    },
  });

  const createPromptMutation = useMutation({
    mutationFn: () =>
      createPrompt(newPromptTitle, newPromptContent, selectedProjectId!),
    onSuccess: () => {
      setNewPromptOpen(false);
      setNewPromptTitle("");
      setNewPromptContent("");
      void queryClient.invalidateQueries({ queryKey: ["kb-prompts", selectedProjectId] });
    },
  });

  const processFiles = useCallback(
    async (files: FileList | File[]) => {
      if (!selectedProjectId) return;
      const list = Array.from(files);
      if (list.length === 0) return;
      setPanelTab("files");
      await uploadDocumentsAsync(list, selectedProjectId, setUploadQueue);
      void queryClient.invalidateQueries({ queryKey: ["kb-documents", selectedProjectId] });
    },
    [queryClient, selectedProjectId],
  );

  const loadSession = async (id: string) => {
    setSessionId(id);
    const history = await getChatMessages(id);
    setMessages(history);
    setContextDocId(undefined);
  };

  const startNewChat = () => {
    setSessionId(undefined);
    setMessages([]);
    setInput("");
    setContextDocId(undefined);
  };

  useEffect(() => {
    setSessionId(undefined);
    setMessages([]);
    setInput("");
    setContextDocId(undefined);
    setUploadQueue([]);
    setPanelTab("chats");
  }, [selectedProjectId]);

  const openRenameSession = (session: ChatSession) => {
    setRenameSessionId(session.id);
    setRenameSessionTitle(session.title ?? "");
    setRenameSessionOpen(true);
  };

  const openEditProject = () => {
    if (!selectedProject) return;
    setNewProjectName(selectedProject.name);
    setNewProjectInstructions(selectedProject.instructions ?? "");
    setEditProjectOpen(true);
  };

  const sendMessage = () => {
    const text = input.trim();
    if (!text || !canChat || chatMutation.isPending) return;
    void chatMutation.mutateAsync(text);
  };

  const hasProjects = (projectsQuery.data?.length ?? 0) > 0;
  const activeSession = useMemo(
    () => sessionsQuery.data?.find((s) => s.id === sessionId),
    [sessionsQuery.data, sessionId],
  );

  return (
    <div className="flex h-[calc(100vh-48px)] min-h-[560px] bg-background">
      <nav className="flex w-[220px] shrink-0 flex-col items-stretch gap-3 border-r bg-muted px-2.5 py-3">
        <RouterLink
          className="mx-auto flex size-10 items-center justify-center rounded-md text-muted-foreground no-underline hover:bg-card"
          to="/"
          title="Back to home"
        >
          <ArrowLeft />
        </RouterLink>
        <div className="flex w-full flex-1 flex-col gap-2 overflow-y-auto">
          {(projectsQuery.data ?? []).map((p) => (
            <button
              key={p.id}
              type="button"
              className={`flex min-h-16 w-full cursor-pointer items-start gap-2.5 rounded-lg border bg-card px-3 py-2.5 text-left hover:bg-muted ${
                selectedProjectId === p.id ? "border-primary bg-primary/10" : ""
              }`}
              onClick={() => setSelectedProjectId(p.id)}
              title={p.name}
            >
              <span className="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted text-base font-semibold">
                {p.name.charAt(0).toUpperCase()}
              </span>
              <div className="flex min-w-0 flex-1 flex-col gap-1">
                <span className="line-clamp-2 text-sm font-semibold leading-5">{p.name}</span>
                <span className="line-clamp-2 text-xs leading-4 text-muted-foreground">
                  {projectSubtitle(p)}
                </span>
              </div>
            </button>
          ))}
        </div>
        <button
          type="button"
          className="flex h-11 w-full cursor-pointer items-center justify-center rounded-lg border border-dashed bg-transparent text-muted-foreground hover:bg-card"
          onClick={() => setNewProjectOpen(true)}
          title="New project"
        >
          <Plus />
        </button>
      </nav>

      {!hasProjects ? (
        <main className="flex flex-1 flex-col items-center justify-center gap-4 p-10 text-center">
          <Folder className="size-14 opacity-50" />
          <h3 className="text-lg font-semibold">Create your first project</h3>
          <p className="max-w-[420px] text-sm text-muted-foreground">
            Projects keep documents, chats, and prompts together — like ChatGPT projects. Each
            project is a separate knowledge space.
          </p>
          <Button size="lg" onClick={() => setNewProjectOpen(true)}>
            New project
          </Button>
        </main>
      ) : (
        <>
          <aside className="flex w-[300px] shrink-0 flex-col border-r bg-muted">
            <div className="flex items-start justify-between gap-2 px-4 pt-4 pb-2">
              <div className="min-w-0 flex-1">
                <h2 className="truncate text-base font-semibold">{selectedProject?.name}</h2>
                {selectedProject?.instructions && (
                  <span className="text-xs text-muted-foreground">Custom instructions</span>
                )}
              </div>
              <Button variant="ghost" size="icon-sm" onClick={openEditProject} title="Edit project">
                <Pencil />
              </Button>
            </div>

            <Tabs
              value={panelTab}
              onValueChange={(value) => {
                if (value === "chats" || value === "files" || value === "prompts") {
                  setPanelTab(value);
                }
              }}
              className="px-2"
            >
              <TabsList className="w-full">
                <TabsTrigger value="chats">
                  <MessageSquare />
                  Chats
                </TabsTrigger>
                <TabsTrigger value="files">
                  <FileText />
                  Files
                </TabsTrigger>
                <TabsTrigger value="prompts">
                  <Lightbulb />
                  Prompts
                </TabsTrigger>
              </TabsList>
            </Tabs>

            <div className="flex min-h-0 flex-1 flex-col gap-2.5 p-3">
              {panelTab === "chats" && (
                <>
                  <Button className="w-full" onClick={startNewChat}>
                    <Plus />
                    New chat
                  </Button>
                  <div className="flex flex-1 flex-col gap-1 overflow-y-auto">
                    {(sessionsQuery.data ?? []).length === 0 && (
                      <span className="px-2 py-3 text-center text-xs text-muted-foreground">
                        No chats in this project yet
                      </span>
                    )}
                    {(sessionsQuery.data ?? []).map((s) => (
                      <ChatSessionRow
                        key={s.id}
                        session={s}
                        active={sessionId === s.id}
                        onSelect={() => void loadSession(s.id)}
                        onRename={() => openRenameSession(s)}
                      />
                    ))}
                  </div>
                </>
              )}

              {panelTab === "files" && (
                <>
                  <div
                    className="flex cursor-pointer flex-col items-center gap-1.5 rounded-md border-2 border-dashed bg-card px-3 py-5 text-center hover:border-primary"
                    onDragOver={(e) => e.preventDefault()}
                    onDrop={(e) => {
                      e.preventDefault();
                      void processFiles(e.dataTransfer.files);
                    }}
                    onClick={() => fileInputRef.current?.click()}
                    role="button"
                    tabIndex={0}
                  >
                    <Upload />
                    <p className="text-sm">Add files to this project</p>
                    <span className="text-xs text-muted-foreground">
                      PDF, Word, Excel, text — indexes in background
                    </span>
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
                  </div>
                  {hasActiveIngest && (
                    <div className="flex items-center gap-2 rounded-md bg-card px-2.5 py-2">
                      <Spinner size="sm" label="Indexing… ready files are searchable now" />
                    </div>
                  )}
                  <div className="flex flex-1 flex-col gap-1 overflow-y-auto">
                    {projectDocuments.length === 0 && (
                      <span className="px-2 py-3 text-center text-xs text-muted-foreground">
                        No files in this project
                      </span>
                    )}
                    {projectDocuments.map((doc) => (
                      <DocumentRow
                        key={doc.id}
                        title={doc.title}
                        status={doc.ingestStatus}
                        detail={doc.ingestDetail}
                        selected={contextDocId === doc.id}
                        onSelect={() =>
                          setContextDocId(contextDocId === doc.id ? undefined : doc.id)
                        }
                      />
                    ))}
                  </div>
                </>
              )}

              {panelTab === "prompts" && (
                <>
                  <Button
                    variant="secondary"
                    className="w-full"
                    onClick={() => {
                      setNewPromptContent(input);
                      setNewPromptOpen(true);
                    }}
                  >
                    <Plus />
                    Save prompt
                  </Button>
                  <div className="flex flex-1 flex-col gap-1 overflow-y-auto">
                    {(promptsQuery.data ?? []).length === 0 && (
                      <span className="px-2 py-3 text-center text-xs text-muted-foreground">
                        Save reusable questions for this project
                      </span>
                    )}
                    {(promptsQuery.data ?? []).map((p) => (
                      <PromptItem
                        key={p.id}
                        prompt={p}
                        onUse={() => {
                          setInput(p.content);
                          setPanelTab("chats");
                        }}
                        onDelete={() => {
                          void deletePrompt(p.id).then(() =>
                            queryClient.invalidateQueries({
                              queryKey: ["kb-prompts", selectedProjectId],
                            }),
                          );
                        }}
                      />
                    ))}
                  </div>
                </>
              )}
            </div>
          </aside>

          <main className="flex min-w-0 flex-1 flex-col bg-background">
            <header className="flex items-center justify-between border-b px-6 py-4">
              <div className="flex min-w-0 flex-1 items-center gap-1">
                <h3 className="text-lg font-semibold">
                  {activeSession?.title ?? (sessionId ? "Chat" : "New conversation")}
                </h3>
                {sessionId && (
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    title="Rename chat"
                    onClick={() => activeSession && openRenameSession(activeSession)}
                  >
                    <Pencil />
                  </Button>
                )}
              </div>
              {contextDocId && (
                <Button variant="ghost" size="sm" onClick={() => setContextDocId(undefined)}>
                  Focused on one file — clear
                </Button>
              )}
            </header>

            <div className="flex flex-1 flex-col gap-5 overflow-y-auto p-6" ref={messagesContainerRef}>
              {messages.length === 0 && (
                <div className="m-auto flex max-w-[480px] flex-col items-center gap-4 text-center">
                  <p className="text-lg font-semibold">
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
                  {!hasReadyDocs && (
                    <Button variant="outline" onClick={() => setPanelTab("files")}>
                      Go to Files
                    </Button>
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

              {messages.map((msg) => (
                <MessageBubble key={msg.id} message={msg} />
              ))}

              {chatMutation.isPending && (
                <div className="flex justify-start">
                  <div className="max-w-[720px] rounded-[18px] border bg-muted px-[18px] py-3.5 leading-normal">
                    <Spinner size="sm" label="Thinking..." />
                  </div>
                </div>
              )}
              <div ref={messagesEndRef} />
            </div>

            <footer className="border-t bg-background px-6 pt-4 pb-6">
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
              <div className="mx-auto flex max-w-[800px] flex-col gap-2.5 rounded-xl border bg-muted p-3">
                <Textarea
                  className="min-h-[72px] w-full resize-none"
                  rows={3}
                  placeholder={
                    canChat
                      ? `Message ${selectedProject?.name ?? "project"}…`
                      : "Add project files or enable web search to chat"
                  }
                  value={input}
                  disabled={!canChat || chatMutation.isPending}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" && !e.shiftKey) {
                      e.preventDefault();
                      sendMessage();
                    }
                  }}
                />
                <Button
                  className="self-end"
                  disabled={!canChat || !input.trim() || chatMutation.isPending}
                  onClick={sendMessage}
                >
                  Send
                </Button>
              </div>
            </footer>
          </main>
        </>
      )}

      <ProjectDialog
        open={newProjectOpen}
        title="New project"
        name={newProjectName}
        instructions={newProjectInstructions}
        onNameChange={setNewProjectName}
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
        instructions={newProjectInstructions}
        onNameChange={setNewProjectName}
        onInstructionsChange={setNewProjectInstructions}
        onClose={() => setEditProjectOpen(false)}
        onSubmit={() => void updateProjectMutation.mutateAsync()}
        submitLabel="Save"
        pending={updateProjectMutation.isPending}
        disabled={!newProjectName.trim()}
      />

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

function projectSubtitle(project: Project): string {
  const description = project.description?.trim();
  if (description) return description;
  const instructions = project.instructions?.trim();
  if (instructions) {
    return instructions.length > 72 ? `${instructions.slice(0, 72)}…` : instructions;
  }
  return "No description yet";
}

function ChatSessionRow({
  session,
  active,
  onSelect,
  onRename,
}: {
  session: ChatSession;
  active: boolean;
  onSelect: () => void;
  onRename: () => void;
}) {
  return (
    <div
      className={`flex items-center gap-1 rounded-md hover:bg-card ${
        active ? "bg-card outline outline-1 outline-primary" : ""
      }`}
    >
      <button
        type="button"
        className="flex min-w-0 flex-1 cursor-pointer items-center gap-2.5 border-0 bg-transparent py-2.5 pr-2 pl-3 text-left text-sm"
        onClick={onSelect}
      >
        <MessageSquare className="size-4 shrink-0" />
        <span className="flex-1 truncate">{session.title ?? "Untitled chat"}</span>
      </button>
      <Button
        variant="ghost"
        size="icon-sm"
        title="Rename chat"
        onClick={(e) => {
          e.stopPropagation();
          onRename();
        }}
      >
        <Pencil />
      </Button>
    </div>
  );
}

function ProjectDialog({
  open,
  title,
  name,
  instructions,
  onNameChange,
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
  instructions: string;
  onNameChange: (v: string) => void;
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
          <Input
            placeholder="Project name"
            value={name}
            onChange={(e) => onNameChange(e.target.value)}
          />
          <Textarea
            placeholder="How should the assistant behave in this project? (optional)"
            value={instructions}
            onChange={(e) => onInstructionsChange(e.target.value)}
            rows={4}
          />
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

function PromptItem({
  prompt,
  onUse,
  onDelete,
}: {
  prompt: Prompt;
  onUse: () => void;
  onDelete: () => void;
}) {
  return (
    <div className="flex items-center gap-1">
      <button
        type="button"
        className="flex w-full cursor-pointer items-center gap-2.5 rounded-md border-0 bg-transparent px-3 py-2.5 text-left text-sm hover:bg-card"
        onClick={onUse}
        title={prompt.content}
      >
        <Lightbulb className="size-4 shrink-0" />
        <span className="flex-1 truncate">{prompt.title}</span>
      </button>
      <Button variant="ghost" size="icon-sm" onClick={onDelete}>
        <Trash2 />
      </Button>
    </div>
  );
}

function DocumentRow({
  title,
  status,
  detail,
  selected,
  onSelect,
}: {
  title: string;
  status: string;
  detail?: string | null;
  selected: boolean;
  onSelect: () => void;
}) {
  const statusLabel =
    detail && status !== "completed" && status !== "failed" ? detail : status;
  const badgeVariant =
    status === "failed" ? "destructive" : status === "processing" ? "secondary" : "outline";
  const badgeClass =
    status === "completed"
      ? "border-green-600/40 text-green-700 dark:text-green-400"
      : status === "failed"
        ? ""
        : status === "processing"
          ? ""
          : "border-amber-500/40 text-amber-700 dark:text-amber-400";

  return (
    <button
      type="button"
      className={`flex w-full cursor-pointer items-center gap-2.5 rounded-md border-0 bg-transparent px-3 py-2.5 text-left text-sm hover:bg-card ${
        selected ? "bg-card outline outline-1 outline-primary" : ""
      }`}
      onClick={onSelect}
      title={detail ? `${status}: ${detail}` : selected ? "Chat uses this file only" : "Focus chat on this file"}
    >
      <FileTypeIcon kind={fileKindFromName(title)} className="size-5 shrink-0 text-muted-foreground" />
      <span className="flex-1 truncate">{title}</span>
      <Badge variant={badgeVariant} className={badgeClass}>
        {statusLabel}
      </Badge>
    </button>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const isUser = message.role === "user";

  return (
    <div className={isUser ? "flex justify-end" : "flex justify-start"}>
      <div
        className={`max-w-[720px] rounded-[18px] px-[18px] py-3.5 leading-normal ${
          isUser ? "bg-primary text-primary-foreground" : "border bg-muted"
        }`}
      >
        {isUser ? (
          <p className="whitespace-pre-wrap text-sm">{message.content}</p>
        ) : (
          <ChatMarkdown content={message.content} className="w-full" />
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
            className="text-xs font-semibold text-primary"
          >
            {citation.title}
          </a>
        ) : (
          <p className="text-xs font-semibold">{citation.title}</p>
        )}
      </div>
      <span className="ml-7 text-xs text-muted-foreground">{citation.snippet}</span>
    </div>
  );
}
