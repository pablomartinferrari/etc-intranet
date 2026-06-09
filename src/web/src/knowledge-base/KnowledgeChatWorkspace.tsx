import {
  Badge,
  Body1,
  Button,
  Caption1,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Input,
  Spinner,
  Subtitle1,
  Tab,
  TabList,
  Textarea,
  Title3,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import type { SelectTabEvent, SelectTabData } from "@fluentui/react-components";
import {
  Add24Regular,
  ArrowLeft24Regular,
  ArrowUpload24Regular,
  Chat24Regular,
  Delete24Regular,
  Document24Regular,
  Edit24Regular,
  Folder24Regular,
  Lightbulb24Regular,
} from "@fluentui/react-icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
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
  const styles = useStyles();
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

  const onTabSelect = (_: SelectTabEvent, data: SelectTabData) => {
    setPanelTab(data.value as ProjectPanelTab);
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
    <div className={styles.app}>
      <nav className={styles.projectRail}>
        <RouterLink className={styles.homeLink} to="/" title="Back to home">
          <ArrowLeft24Regular />
        </RouterLink>
        <div className={styles.railProjects}>
          {(projectsQuery.data ?? []).map((p) => (
            <button
              key={p.id}
              type="button"
              className={`${styles.railProjectCard} ${selectedProjectId === p.id ? styles.railProjectCardActive : ""}`}
              onClick={() => setSelectedProjectId(p.id)}
              title={p.name}
            >
              <span className={styles.railProjectInitial}>{p.name.charAt(0).toUpperCase()}</span>
              <div className={styles.railProjectInfo}>
                <span className={styles.railProjectName}>{p.name}</span>
                <Caption1 className={styles.railProjectSubtitle}>
                  {projectSubtitle(p)}
                </Caption1>
              </div>
            </button>
          ))}
        </div>
        <button
          type="button"
          className={styles.railNewBtn}
          onClick={() => setNewProjectOpen(true)}
          title="New project"
        >
          <Add24Regular />
        </button>
      </nav>

      {!hasProjects ? (
        <main className={styles.onboarding}>
          <Folder24Regular className={styles.onboardingIcon} />
          <Title3>Create your first project</Title3>
          <Body1 className={styles.onboardingText}>
            Projects keep documents, chats, and prompts together — like ChatGPT projects. Each
            project is a separate knowledge space.
          </Body1>
          <Button appearance="primary" size="large" onClick={() => setNewProjectOpen(true)}>
            New project
          </Button>
        </main>
      ) : (
        <>
          <aside className={styles.projectPanel}>
            <div className={styles.projectHeader}>
              <div className={styles.projectHeaderText}>
                <Subtitle1 className={styles.projectName}>{selectedProject?.name}</Subtitle1>
                {selectedProject?.instructions && (
                  <Caption1 className={styles.projectMeta}>Custom instructions</Caption1>
                )}
              </div>
              <Button
                appearance="subtle"
                icon={<Edit24Regular />}
                size="small"
                onClick={openEditProject}
                title="Edit project"
              />
            </div>

            <TabList selectedValue={panelTab} onTabSelect={onTabSelect} className={styles.tabs}>
              <Tab icon={<Chat24Regular />} value="chats">
                Chats
              </Tab>
              <Tab icon={<Document24Regular />} value="files">
                Files
              </Tab>
              <Tab icon={<Lightbulb24Regular />} value="prompts">
                Prompts
              </Tab>
            </TabList>

            <div className={styles.panelBody}>
              {panelTab === "chats" && (
                <>
                  <Button
                    appearance="primary"
                    icon={<Add24Regular />}
                    className={styles.panelAction}
                    onClick={startNewChat}
                  >
                    New chat
                  </Button>
                  <div className={styles.panelList}>
                    {(sessionsQuery.data ?? []).length === 0 && (
                      <Caption1 className={styles.emptyPanel}>No chats in this project yet</Caption1>
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
                    className={styles.uploadZone}
                    onDragOver={(e) => e.preventDefault()}
                    onDrop={(e) => {
                      e.preventDefault();
                      void processFiles(e.dataTransfer.files);
                    }}
                    onClick={() => fileInputRef.current?.click()}
                    role="button"
                    tabIndex={0}
                  >
                    <ArrowUpload24Regular />
                    <Body1>Add files to this project</Body1>
                    <Caption1>PDF, Word, Excel, text — indexes in background</Caption1>
                    <input
                      ref={fileInputRef}
                      type="file"
                      multiple
                      accept=".pdf,.docx,.xlsx,.txt,.md,.html,.csv"
                      className={styles.hiddenInput}
                      onChange={(e) => {
                        if (e.target.files) void processFiles(e.target.files);
                        e.target.value = "";
                      }}
                    />
                  </div>
                  {hasActiveIngest && (
                    <div className={styles.indexingNote}>
                      <Spinner size="tiny" />
                      <Caption1>Indexing… ready files are searchable now</Caption1>
                    </div>
                  )}
                  <div className={styles.panelList}>
                    {projectDocuments.length === 0 && (
                      <Caption1 className={styles.emptyPanel}>No files in this project</Caption1>
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
                    appearance="secondary"
                    icon={<Add24Regular />}
                    className={styles.panelAction}
                    onClick={() => {
                      setNewPromptContent(input);
                      setNewPromptOpen(true);
                    }}
                  >
                    Save prompt
                  </Button>
                  <div className={styles.panelList}>
                    {(promptsQuery.data ?? []).length === 0 && (
                      <Caption1 className={styles.emptyPanel}>
                        Save reusable questions for this project
                      </Caption1>
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

          <main className={styles.chatMain}>
            <header className={styles.chatHeader}>
              <div className={styles.chatHeaderTitle}>
                <Title3>
                  {activeSession?.title ?? (sessionId ? "Chat" : "New conversation")}
                </Title3>
                {sessionId && (
                  <Button
                    appearance="subtle"
                    icon={<Edit24Regular />}
                    size="small"
                    title="Rename chat"
                    onClick={() => activeSession && openRenameSession(activeSession)}
                  />
                )}
              </div>
              {contextDocId && (
                <Button appearance="subtle" size="small" onClick={() => setContextDocId(undefined)}>
                  Focused on one file — clear
                </Button>
              )}
            </header>

            <div className={styles.messages} ref={messagesContainerRef}>
              {messages.length === 0 && (
                <div className={styles.chatEmpty}>
                  <Body1 className={styles.chatEmptyTitle}>
                    {hasReadyDocs
                      ? "Ask about this project's documents"
                      : webSearchEnabled
                        ? "Ask a question — we'll search your project files and the web when needed"
                        : "Add files to this project to get started"}
                  </Body1>
                  {hasReadyDocs && (
                    <Caption1>
                      Say &quot;export to Excel&quot; or &quot;create a Word summary&quot; to generate a
                      downloadable file from your project sources.
                    </Caption1>
                  )}
                  {!hasReadyDocs && (
                    <Button appearance="outline" onClick={() => setPanelTab("files")}>
                      Go to Files
                    </Button>
                  )}
                  {(promptsQuery.data ?? []).length > 0 && canChat && (
                    <div className={styles.starterPrompts}>
                      <Caption1>Try a saved prompt</Caption1>
                      <div className={styles.starterPromptRow}>
                        {(promptsQuery.data ?? []).slice(0, 3).map((p) => (
                          <Button
                            key={p.id}
                            appearance="outline"
                            size="small"
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
                <div className={styles.assistantRow}>
                  <div className={`${styles.bubble} ${styles.assistantBubble}`}>
                    <Spinner size="tiny" label="Thinking..." />
                  </div>
                </div>
              )}
              <div ref={messagesEndRef} />
            </div>

            <footer className={styles.composer}>
              {!selectedProjectId && (
                <Caption1 className={styles.composerHint}>Select a project to continue</Caption1>
              )}
              {selectedProjectId && !canChat && (
                <Caption1 className={styles.composerHint}>
                  {webSearchEnabled
                    ? "Upload a file and wait for indexing, or ask a general question"
                    : "Upload a file and wait for completed status, or enable web search in API settings"}
                </Caption1>
              )}
              {canChat && hasActiveIngest && (
                <Caption1 className={styles.composerHint}>
                  Some files still indexing — answers use completed files only
                </Caption1>
              )}
              <div className={styles.composerBox}>
                <Textarea
                  className={styles.composerInput}
                  resize="none"
                  rows={3}
                  placeholder={
                    canChat
                      ? `Message ${selectedProject?.name ?? "project"}…`
                      : "Add project files or enable web search to chat"
                  }
                  value={input}
                  disabled={!canChat || chatMutation.isPending}
                  onChange={(_, d) => setInput(d.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" && !e.shiftKey) {
                      e.preventDefault();
                      sendMessage();
                    }
                  }}
                />
                <Button
                  appearance="primary"
                  className={styles.sendBtn}
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

      <Dialog open={newPromptOpen} onOpenChange={(_, d) => setNewPromptOpen(d.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Save prompt for {selectedProject?.name}</DialogTitle>
            <DialogContent className={styles.dialogStack}>
              <Input
                placeholder="Short title"
                value={newPromptTitle}
                onChange={(_, d) => setNewPromptTitle(d.value)}
              />
              <Textarea
                placeholder="Full prompt text"
                value={newPromptContent}
                onChange={(_, d) => setNewPromptContent(d.value)}
                rows={4}
              />
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setNewPromptOpen(false)}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                disabled={
                  !newPromptTitle.trim() ||
                  !newPromptContent.trim() ||
                  createPromptMutation.isPending
                }
                onClick={() => void createPromptMutation.mutateAsync()}
              >
                Save
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={renameSessionOpen} onOpenChange={(_, d) => !d.open && setRenameSessionOpen(false)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Rename chat</DialogTitle>
            <DialogContent className={styles.dialogStack}>
              <Input
                placeholder="Chat name"
                value={renameSessionTitle}
                onChange={(_, d) => setRenameSessionTitle(d.value)}
              />
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setRenameSessionOpen(false)}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                disabled={!renameSessionTitle.trim() || renameSessionMutation.isPending}
                onClick={() => void renameSessionMutation.mutateAsync()}
              >
                Save
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
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
  const styles = useStyles();

  return (
    <div className={`${styles.sessionRow} ${active ? styles.sessionRowActive : ""}`}>
      <button type="button" className={styles.sessionRowMain} onClick={onSelect}>
        <Chat24Regular />
        <span>{session.title ?? "Untitled chat"}</span>
      </button>
      <Button
        appearance="subtle"
        size="small"
        icon={<Edit24Regular />}
        title="Rename chat"
        onClick={(e) => {
          e.stopPropagation();
          onRename();
        }}
      />
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
  const styles = useStyles();
  return (
    <Dialog open={open} onOpenChange={(_, d) => !d.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{title}</DialogTitle>
          <DialogContent className={styles.dialogStack}>
            <Input placeholder="Project name" value={name} onChange={(_, d) => onNameChange(d.value)} />
            <Textarea
              placeholder="How should the assistant behave in this project? (optional)"
              value={instructions}
              onChange={(_, d) => onInstructionsChange(d.value)}
              rows={4}
            />
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button appearance="primary" disabled={disabled || pending} onClick={onSubmit}>
              {submitLabel}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
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
  const styles = useStyles();
  return (
    <div className={styles.promptRow}>
      <button type="button" className={styles.panelItem} onClick={onUse} title={prompt.content}>
        <Lightbulb24Regular />
        <span>{prompt.title}</span>
      </button>
      <Button appearance="subtle" icon={<Delete24Regular />} size="small" onClick={onDelete} />
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
  const styles = useStyles();
  const badgeColor =
    status === "completed"
      ? "success"
      : status === "failed"
        ? "danger"
        : status === "processing"
          ? "informative"
          : "warning";
  const statusLabel =
    detail && status !== "completed" && status !== "failed" ? detail : status;

  return (
    <button
      type="button"
      className={`${styles.panelItem} ${selected ? styles.panelItemActive : ""}`}
      onClick={onSelect}
      title={detail ? `${status}: ${detail}` : selected ? "Chat uses this file only" : "Focus chat on this file"}
    >
      <FileTypeIcon kind={fileKindFromName(title)} className={styles.fileTypeIcon} />
      <span className={styles.docTitle}>{title}</span>
      <Badge appearance="outline" color={badgeColor} size="small">
        {statusLabel}
      </Badge>
    </button>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const styles = useStyles();
  const isUser = message.role === "user";

  return (
    <div className={isUser ? styles.userRow : styles.assistantRow}>
      <div className={`${styles.bubble} ${isUser ? styles.userBubble : styles.assistantBubble}`}>
        {isUser ? (
          <Body1 className={styles.messageText}>{message.content}</Body1>
        ) : (
          <ChatMarkdown content={message.content} className={styles.messageMarkdown} />
        )}
        {!isUser && message.attachments && message.attachments.length > 0 && (
          <div className={styles.attachments}>
            {message.attachments.map((attachment) => (
              <AttachmentDownload key={attachment.id} attachment={attachment} />
            ))}
          </div>
        )}
        {!isUser && message.citations && message.citations.length > 0 && (
          <div className={styles.citations}>
            <Caption1 className={styles.citationsLabel}>Sources</Caption1>
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
  const styles = useStyles();
  const kind =
    fileKindFromFormat(attachment.format) !== "generic"
      ? fileKindFromFormat(attachment.format)
      : fileKindFromName(attachment.filename);

  return (
    <Button
      appearance="secondary"
      size="small"
      icon={<FileTypeIcon kind={kind} className={styles.fileTypeIcon} />}
      className={styles.attachmentButton}
      onClick={() => void downloadGeneratedFile(attachment.id, attachment.filename)}
    >
      {attachment.filename}
    </Button>
  );
}

function CitationChip({ citation }: { citation: Citation }) {
  const styles = useStyles();
  const isWeb = citation.type === "web" || !!citation.url;
  const iconKind = isWeb ? "web" : fileKindFromName(citation.title);

  return (
    <div className={styles.citation}>
      <div className={styles.citationHeader}>
        <FileTypeIcon kind={iconKind} className={styles.fileTypeIcon} />
        {isWeb && citation.url ? (
          <a
            href={citation.url}
            target="_blank"
            rel="noopener noreferrer"
            className={styles.citationLink}
          >
            {citation.title}
          </a>
        ) : (
          <Body1 className={styles.citationTitle}>{citation.title}</Body1>
        )}
      </div>
      <Caption1 className={styles.citationSnippet}>{citation.snippet}</Caption1>
    </div>
  );
}

const useStyles = makeStyles({
  app: {
    display: "flex",
    height: "calc(100vh - 48px)",
    minHeight: "560px",
    backgroundColor: tokens.colorNeutralBackground1,
  },
  projectRail: {
    width: "220px",
    flexShrink: 0,
    display: "flex",
    flexDirection: "column",
    alignItems: "stretch",
    gap: "12px",
    padding: "12px 10px",
    backgroundColor: tokens.colorNeutralBackground3,
    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  homeLink: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    width: "40px",
    height: "40px",
    margin: "0 auto",
    borderRadius: tokens.borderRadiusMedium,
    color: tokens.colorNeutralForeground2,
    textDecoration: "none",
    ":hover": { backgroundColor: tokens.colorNeutralBackground1 },
  },
  railProjects: {
    flex: 1,
    display: "flex",
    flexDirection: "column",
    gap: "8px",
    width: "100%",
    overflowY: "auto",
  },
  railProjectCard: {
    width: "100%",
    minHeight: "64px",
    padding: "10px 12px",
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusLarge,
    backgroundColor: tokens.colorNeutralBackground1,
    cursor: "pointer",
    display: "flex",
    alignItems: "flex-start",
    gap: "10px",
    textAlign: "left",
    ":hover": { backgroundColor: tokens.colorNeutralBackground2 },
  },
  railProjectCardActive: {
    backgroundColor: tokens.colorBrandBackground2,
    borderTopColor: tokens.colorBrandStroke1,
    borderRightColor: tokens.colorBrandStroke1,
    borderBottomColor: tokens.colorBrandStroke1,
    borderLeftColor: tokens.colorBrandStroke1,
  },
  railProjectInfo: {
    minWidth: 0,
    flex: 1,
    display: "flex",
    flexDirection: "column",
    gap: "4px",
  },
  railProjectName: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase300,
    lineHeight: tokens.lineHeightBase300,
    display: "-webkit-box",
    WebkitLineClamp: 2,
    WebkitBoxOrient: "vertical",
    overflow: "hidden",
  },
  railProjectSubtitle: {
    color: tokens.colorNeutralForeground3,
    display: "-webkit-box",
    WebkitLineClamp: 2,
    WebkitBoxOrient: "vertical",
    overflow: "hidden",
    lineHeight: tokens.lineHeightBase200,
  },
  railProjectInitial: {
    flexShrink: 0,
    width: "36px",
    height: "36px",
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground3,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  railNewBtn: {
    width: "100%",
    height: "44px",
    border: `1px dashed ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusLarge,
    backgroundColor: "transparent",
    cursor: "pointer",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    color: tokens.colorNeutralForeground2,
    ":hover": { backgroundColor: tokens.colorNeutralBackground1 },
  },
  onboarding: {
    flex: 1,
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    justifyContent: "center",
    gap: "16px",
    padding: "40px",
    textAlign: "center",
  },
  onboardingIcon: { width: "56px", height: "56px", opacity: 0.5 },
  onboardingText: { maxWidth: "420px", color: tokens.colorNeutralForeground2 },
  projectPanel: {
    width: "300px",
    flexShrink: 0,
    display: "flex",
    flexDirection: "column",
    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  projectHeader: {
    display: "flex",
    alignItems: "flex-start",
    justifyContent: "space-between",
    padding: "16px 16px 8px",
    gap: "8px",
  },
  projectHeaderText: { minWidth: 0, flex: 1 },
  projectName: {
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  },
  projectMeta: { color: tokens.colorNeutralForeground3 },
  tabs: { paddingLeft: "8px", paddingRight: "8px" },
  panelBody: {
    flex: 1,
    display: "flex",
    flexDirection: "column",
    minHeight: 0,
    padding: "12px",
    gap: "10px",
  },
  panelAction: { width: "100%" },
  panelList: {
    flex: 1,
    overflowY: "auto",
    display: "flex",
    flexDirection: "column",
    gap: "4px",
  },
  panelItem: {
    display: "flex",
    alignItems: "center",
    gap: "10px",
    width: "100%",
    padding: "10px 12px",
    border: "none",
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: "transparent",
    cursor: "pointer",
    textAlign: "left",
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground1,
    ":hover": { backgroundColor: tokens.colorNeutralBackground1 },
    "& span": {
      flex: 1,
      overflow: "hidden",
      textOverflow: "ellipsis",
      whiteSpace: "nowrap",
    },
  },
  panelItemActive: {
    backgroundColor: tokens.colorNeutralBackground1,
    outline: `1px solid ${tokens.colorBrandStroke1}`,
  },
  emptyPanel: {
    padding: "12px 8px",
    color: tokens.colorNeutralForeground3,
    textAlign: "center",
  },
  uploadZone: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: "6px",
    padding: "20px 12px",
    borderRadius: tokens.borderRadiusMedium,
    border: `2px dashed ${tokens.colorNeutralStroke2}`,
    cursor: "pointer",
    textAlign: "center",
    backgroundColor: tokens.colorNeutralBackground1,
    ":hover": { borderTopColor: tokens.colorBrandStroke1 },
  },
  hiddenInput: { display: "none" },
  indexingNote: {
    display: "flex",
    alignItems: "center",
    gap: "8px",
    padding: "8px 10px",
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  docTitle: { flex: 1 },
  promptRow: {
    display: "flex",
    alignItems: "center",
    gap: "4px",
  },
  chatMain: {
    flex: 1,
    display: "flex",
    flexDirection: "column",
    minWidth: 0,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  chatHeader: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    padding: "16px 24px",
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  chatHeaderTitle: {
    display: "flex",
    alignItems: "center",
    gap: "4px",
    minWidth: 0,
    flex: 1,
  },
  sessionRow: {
    display: "flex",
    alignItems: "center",
    gap: "4px",
    borderRadius: tokens.borderRadiusMedium,
    ":hover": { backgroundColor: tokens.colorNeutralBackground1 },
  },
  sessionRowActive: {
    backgroundColor: tokens.colorNeutralBackground1,
    outline: `1px solid ${tokens.colorBrandStroke1}`,
  },
  sessionRowMain: {
    flex: 1,
    display: "flex",
    alignItems: "center",
    gap: "10px",
    minWidth: 0,
    padding: "10px 8px 10px 12px",
    border: "none",
    backgroundColor: "transparent",
    cursor: "pointer",
    textAlign: "left",
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground1,
    "& span": {
      flex: 1,
      overflow: "hidden",
      textOverflow: "ellipsis",
      whiteSpace: "nowrap",
    },
  },
  messages: {
    flex: 1,
    overflowY: "auto",
    padding: "24px",
    display: "flex",
    flexDirection: "column",
    gap: "20px",
  },
  chatEmpty: {
    margin: "auto",
    maxWidth: "480px",
    textAlign: "center",
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: "16px",
  },
  chatEmptyTitle: {
    fontSize: tokens.fontSizeBase500,
    fontWeight: tokens.fontWeightSemibold,
  },
  starterPrompts: {
    display: "flex",
    flexDirection: "column",
    gap: "8px",
    marginTop: "8px",
  },
  starterPromptRow: {
    display: "flex",
    flexWrap: "wrap",
    gap: "8px",
    justifyContent: "center",
  },
  userRow: { display: "flex", justifyContent: "flex-end" },
  assistantRow: { display: "flex", justifyContent: "flex-start" },
  bubble: {
    maxWidth: "720px",
    padding: "14px 18px",
    borderRadius: "18px",
    lineHeight: "1.5",
  },
  userBubble: {
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
  },
  assistantBubble: {
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.border("1px", "solid", tokens.colorNeutralStroke2),
  },
  messageText: { whiteSpace: "pre-wrap" },
  messageMarkdown: { width: "100%" },
  citations: { marginTop: "14px", display: "flex", flexDirection: "column", gap: "8px" },
  attachments: {
    marginTop: "12px",
    display: "flex",
    flexWrap: "wrap",
    gap: "8px",
  },
  attachmentButton: {
    maxWidth: "100%",
  },
  citationsLabel: { fontWeight: tokens.fontWeightSemibold },
  citation: {
    padding: "10px 12px",
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  citationTitle: { fontWeight: tokens.fontWeightSemibold, fontSize: tokens.fontSizeBase200 },
  composer: {
    padding: "16px 24px 24px",
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  composerHint: {
    display: "block",
    marginBottom: "8px",
    color: tokens.colorNeutralForeground3,
    textAlign: "center",
  },
  composerBox: {
    maxWidth: "800px",
    margin: "0 auto",
    display: "flex",
    flexDirection: "column",
    gap: "10px",
    padding: "12px",
    borderRadius: tokens.borderRadiusXLarge,
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.border("1px", "solid", tokens.colorNeutralStroke2),
  },
  fileTypeIcon: {
    flexShrink: 0,
    width: "20px",
    height: "20px",
    color: tokens.colorNeutralForeground2,
  },
  citationHeader: {
    display: "flex",
    alignItems: "center",
    gap: "8px",
    minWidth: 0,
  },
  citationSnippet: {
    marginLeft: "28px",
  },
  citationLink: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorBrandForeground1,
  },
  composerInput: { width: "100%" },
  sendBtn: { alignSelf: "flex-end" },
  dialogStack: { display: "flex", flexDirection: "column", gap: "12px" },
});
