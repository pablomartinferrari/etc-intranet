import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Check,
  FileSpreadsheet,
  FileText,
  Folder,
  Image as ImageIcon,
  Loader2,
  MessageSquare,
  MoreHorizontal,
  Plus,
  Search,
  Sparkles,
  Upload,
} from "lucide-react";
import { useMemo, useRef, useState, type DragEvent, type FormEvent } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";
import { workspaceApi, type RepoDocument, type RepoFolder } from "../api";

function FileIcon({ type }: { type: string }) {
  if (type === "spreadsheet") return <FileSpreadsheet className="size-4" />;
  if (type === "image") return <ImageIcon className="size-4" />;
  return <FileText className="size-4" />;
}

const FOLDER_MIME = "application/x-workspace-folder";
const DOC_MIME = "application/x-workspace-document";

type MoveTarget = { kind: "folder" | "document"; id: string; name: string };
type RenameTarget = { kind: "folder" | "document"; id: string; name: string };
type DeleteTarget =
  | { kind: "folder"; folder: RepoFolder; recursive: boolean; message?: string }
  | { kind: "document"; document: RepoDocument };
type UploadRow = {
  id: string;
  name: string;
  status: "queued" | "uploading" | "done" | "error";
  percent: number;
  error?: string;
};

function isFileDrag(event: DragEvent) {
  return Array.from(event.dataTransfer.types).includes("Files");
}

function isDescendantOrSelf(folders: RepoFolder[], candidateId: string, ancestorId: string) {
  if (candidateId === ancestorId) return true;
  let current = folders.find((folder) => folder.id === candidateId);
  const seen = new Set<string>();
  while (current?.parentId) {
    if (current.parentId === ancestorId) return true;
    if (seen.has(current.parentId)) return false;
    seen.add(current.parentId);
    current = folders.find((folder) => folder.id === current!.parentId);
  }
  return false;
}

export function WorkspaceRepoBrowsePage() {
  const { folderId } = useParams();
  const [params] = useSearchParams();
  const documentId = params.get("doc") ?? params.get("documentId") ?? undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const fileRef = useRef<HTMLInputElement | null>(null);
  const internalDrag = useRef(false);
  const dragDepth = useRef(0);

  const [searchText, setSearchText] = useState("");
  const [fileOver, setFileOver] = useState(false);
  const [uploads, setUploads] = useState<UploadRow[]>([]);
  const [newFolderOpen, setNewFolderOpen] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [renameTarget, setRenameTarget] = useState<RenameTarget | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [moveTarget, setMoveTarget] = useState<MoveTarget | null>(null);
  const [moveDestination, setMoveDestination] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const browseQuery = useQuery({
    queryKey: ["ws-repo", folderId, documentId],
    queryFn: () => workspaceApi.browse(folderId, documentId),
  });
  const treeQuery = useQuery({ queryKey: ["ws-sidebar"], queryFn: workspaceApi.sidebar });
  const chatsQuery = useQuery({
    queryKey: ["ws-folder-chats", browseQuery.data?.folder.id],
    queryFn: () => workspaceApi.folderChats(browseQuery.data!.folder.id),
    enabled: !!browseQuery.data?.folder.id,
  });

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ["ws-repo"] });
    await queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
    await queryClient.invalidateQueries({ queryKey: ["ws-folder-chats"] });
  }

  const askMutation = useMutation({
    mutationFn: async (doc: RepoDocument) => {
      const chat = await workspaceApi.createChat({
        title: `Ask about ${doc.name}`,
        folderId: doc.folderId,
        documentId: doc.id,
      });
      return chat;
    },
    onSuccess: (chat) => {
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      navigate(`/workspace/chats/${chat.id}`);
    },
    onError: (err: Error) => setError(err.message),
  });

  const reindexMutation = useMutation({
    mutationFn: (doc: RepoDocument) => workspaceApi.reindexDocument(doc.id),
    onSuccess: () => void refresh(),
    onError: (err: Error) => setError(err.message),
  });

  const newChatMutation = useMutation({
    mutationFn: () =>
      workspaceApi.createChat({
        title: `Chat in ${browseQuery.data?.folder.name ?? "folder"}`,
        folderId: browseQuery.data?.folder.id,
      }),
    onSuccess: (chat) => {
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      navigate(`/workspace/chats/${chat.id}`);
    },
    onError: (err: Error) => setError(err.message),
  });

  const browse = browseQuery.data;
  const folders = treeQuery.data?.repository ?? [];
  const selected = browse?.selected;
  const isRoot = !browse?.folder.parentId;
  const folderName = browse?.folder.name ?? "folder";
  const isEmpty = (browse?.children ?? []).length === 0 && (browse?.documents ?? []).length === 0;

  const moveOptions = useMemo(() => {
    if (!moveTarget) return folders;
    if (moveTarget.kind === "document") return folders;
    return folders.filter((folder) => !isDescendantOrSelf(folders, folder.id, moveTarget.id));
  }, [folders, moveTarget]);

  function updateUpload(id: string, patch: Partial<UploadRow>) {
    setUploads((current) => current.map((row) => (row.id === id ? { ...row, ...patch } : row)));
  }

  async function startUploads(files: File[], destinationId?: string) {
    const folder = destinationId ?? browse?.folder.id;
    if (!folder || files.length === 0) return;
    const rows: UploadRow[] = files.map((file) => ({
      id: `${file.name}-${file.size}-${file.lastModified}-${Math.random().toString(36).slice(2)}`,
      name: file.name,
      status: "queued",
      percent: 0,
    }));
    setUploads((current) => [...current, ...rows]);
    setError(null);
    let last: RepoDocument | null = null;
    for (let i = 0; i < files.length; i += 1) {
      const file = files[i];
      const row = rows[i];
      updateUpload(row.id, { status: "uploading", percent: 0 });
      try {
        last = await workspaceApi.upload(folder, file, (percent) => updateUpload(row.id, { percent }));
        updateUpload(row.id, { status: "done", percent: 100 });
      } catch (err) {
        const message = err instanceof Error ? err.message : "Upload failed.";
        updateUpload(row.id, { status: "error", error: message });
        setError(message);
      }
    }
    await refresh();
    if (last) navigate(`/workspace/repo/${last.folderId}?doc=${last.id}`);
  }

  function beginRenameFolder(folder: RepoFolder) {
    setRenameTarget({ kind: "folder", id: folder.id, name: folder.name });
    setRenameValue(folder.name);
  }

  function beginRenameDocument(doc: RepoDocument) {
    setRenameTarget({ kind: "document", id: doc.id, name: doc.name });
    setRenameValue(doc.name);
  }

  function beginMoveFolder(folder: RepoFolder) {
    setMoveTarget({ kind: "folder", id: folder.id, name: folder.name });
    setMoveDestination("");
  }

  function beginMoveDocument(doc: RepoDocument) {
    setMoveTarget({ kind: "document", id: doc.id, name: doc.name });
    setMoveDestination("");
  }

  async function submitRename(event: FormEvent) {
    event.preventDefault();
    if (!renameTarget) return;
    const name = renameValue.trim();
    if (!name || name === renameTarget.name) {
      setRenameTarget(null);
      return;
    }
    setBusy(true);
    try {
      if (renameTarget.kind === "folder") await workspaceApi.renameFolder(renameTarget.id, name);
      else await workspaceApi.renameDocument(renameTarget.id, name);
      setRenameTarget(null);
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not rename.");
    } finally {
      setBusy(false);
    }
  }

  async function submitMove(event: FormEvent) {
    event.preventDefault();
    if (!moveTarget || !moveDestination) return;
    setBusy(true);
    try {
      if (moveTarget.kind === "folder") await workspaceApi.moveFolder(moveTarget.id, moveDestination);
      else await workspaceApi.moveDocument(moveTarget.id, moveDestination);
      setMoveTarget(null);
      setMoveDestination("");
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not move item.");
    } finally {
      setBusy(false);
    }
  }

  async function submitNewFolder(event: FormEvent) {
    event.preventDefault();
    const name = newFolderName.trim();
    if (!name || !browse) return;
    setBusy(true);
    try {
      const folder = await workspaceApi.createFolder(browse.folder.id, name);
      setNewFolderOpen(false);
      setNewFolderName("");
      await refresh();
      navigate(`/workspace/repo/${folder.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create folder.");
    } finally {
      setBusy(false);
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    setBusy(true);
    try {
      if (deleteTarget.kind === "document") {
        await workspaceApi.deleteDocument(deleteTarget.document.id);
        setDeleteTarget(null);
        await refresh();
        navigate(`/workspace/repo/${deleteTarget.document.folderId}`);
        return;
      }
      const { folder, recursive } = deleteTarget;
      try {
        await workspaceApi.deleteFolder(folder.id, recursive);
        setDeleteTarget(null);
        await refresh();
        navigate(`/workspace/repo/${folder.parentId ?? ""}`);
      } catch (err) {
        const message = err instanceof Error ? err.message : "Could not delete folder.";
        if (!recursive && (message.toLowerCase().includes("not empty") || message.toLowerCase().includes("recursive"))) {
          setDeleteTarget({ kind: "folder", folder, recursive: true, message });
        } else {
          setError(message);
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not delete.");
    } finally {
      setBusy(false);
    }
  }

  async function dropOnFolder(event: DragEvent, destinationId: string) {
    event.preventDefault();
    event.stopPropagation();
    const files = Array.from(event.dataTransfer.files);
    if (files.length) {
      await startUploads(files, destinationId);
      return;
    }
    const folderDrag = event.dataTransfer.getData(FOLDER_MIME);
    const docDrag = event.dataTransfer.getData(DOC_MIME);
    try {
      if (folderDrag && folderDrag !== destinationId) {
        await workspaceApi.moveFolder(folderDrag, destinationId);
        await refresh();
      } else if (docDrag) {
        await workspaceApi.moveDocument(docDrag, destinationId);
        await refresh();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not move item.");
    }
  }

  function onListDragEnter(event: DragEvent) {
    if (internalDrag.current || !isFileDrag(event)) return;
    dragDepth.current += 1;
    setFileOver(true);
  }

  function onListDragLeave(event: DragEvent) {
    if (internalDrag.current || !isFileDrag(event)) return;
    dragDepth.current = Math.max(0, dragDepth.current - 1);
    if (dragDepth.current === 0) setFileOver(false);
  }

  function onListDragOver(event: DragEvent) {
    event.preventDefault();
  }

  async function onListDrop(event: DragEvent) {
    event.preventDefault();
    dragDepth.current = 0;
    setFileOver(false);
    if (!browse) return;
    const files = Array.from(event.dataTransfer.files);
    if (files.length) {
      await startUploads(files, browse.folder.id);
      return;
    }
    await dropOnFolder(event, browse.folder.id);
  }

  function runFolderSearch(event: FormEvent) {
    event.preventDefault();
    const q = searchText.trim();
    if (!q) return;
    navigate(`/workspace/search?q=${encodeURIComponent(q)}&folderId=${browse?.folder.id ?? ""}`);
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center justify-between gap-3 px-6 py-3">
        <div className="flex min-w-0 flex-wrap items-center gap-1 text-sm">
          {(browse?.breadcrumbs ?? []).map((crumb, index) => (
            <span key={crumb.id} className="flex items-center gap-1">
              {index > 0 ? <span className="text-muted-foreground">/</span> : null}
              <Link
                to={`/workspace/repo/${crumb.id}`}
                className="rounded-sm hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                onDragOver={(e) => e.preventDefault()}
                onDrop={(e) => void dropOnFolder(e, crumb.id)}
              >
                {crumb.name}
              </Link>
            </span>
          ))}
          {browse && !isRoot ? (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  type="button"
                  size="icon-xs"
                  variant="ghost"
                  className="ml-0.5"
                  aria-label={`Folder actions for ${folderName}`}
                >
                  <MoreHorizontal />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start">
                <DropdownMenuItem onSelect={() => beginRenameFolder(browse.folder)}>Rename folder</DropdownMenuItem>
                <DropdownMenuItem onSelect={() => beginMoveFolder(browse.folder)}>Move folder…</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onSelect={() => setDeleteTarget({ kind: "folder", folder: browse.folder, recursive: false })}>
                  Delete folder
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          ) : null}
          {browse?.folder.isPublic ? (
            <span className="ml-2 rounded-full border px-2 py-0.5 text-[11px] text-muted-foreground">Public · Indexed</span>
          ) : null}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <input
            ref={fileRef}
            type="file"
            multiple
            className="hidden"
            onChange={(e) => {
              const files = Array.from(e.target.files ?? []);
              if (files.length) void startUploads(files);
              e.target.value = "";
            }}
          />
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button size="sm" className="rounded-full">
                <Plus /> New
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onSelect={() => fileRef.current?.click()}>
                <Upload /> Upload files
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => setNewFolderOpen(true)}>
                <Folder /> New folder
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
          <Button
            size="sm"
            variant="outline"
            className="rounded-full"
            onClick={() => newChatMutation.mutate()}
            disabled={newChatMutation.isPending || !browse}
          >
            <MessageSquare /> Chat in this folder
          </Button>
        </div>
      </header>
      {error ? (
        <p className="px-6 pb-2 text-xs text-destructive" role="alert">
          {error}
        </p>
      ) : null}

      <div className="grid min-h-0 flex-1 grid-cols-[minmax(0,1fr)_280px] divide-x">
        <div className="flex min-h-0 min-w-0 flex-col">
          <form
            className="border-b bg-muted/30 px-4 py-2"
            onSubmit={runFolderSearch}
            role="search"
            aria-label={`Search in ${folderName} and subfolders`}
          >
            <div className="relative">
              <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={searchText}
                onChange={(e) => setSearchText(e.target.value)}
                placeholder={`Search in ${folderName} and subfolders`}
                className="h-9 rounded-md bg-background pl-9"
                aria-label={`Search in ${folderName} and subfolders`}
              />
            </div>
          </form>
          <div
            className="relative min-h-0 flex-1 overflow-auto"
            onDragEnter={onListDragEnter}
            onDragLeave={onListDragLeave}
            onDragOver={onListDragOver}
            onDrop={(e) => void onListDrop(e)}
          >
            {fileOver ? (
              <div className="pointer-events-none absolute inset-0 z-10 flex items-center justify-center bg-background/80 ring-2 ring-inset ring-primary">
                <p className="rounded-full border bg-background px-4 py-2 text-sm font-medium shadow-sm">
                  Drop files to upload to {folderName}
                </p>
              </div>
            ) : null}
            {uploads.length > 0 ? (
              <ul className="space-y-1.5 border-b px-4 py-3" aria-live="polite">
                {uploads.map((row) => (
                  <li key={row.id} className="flex items-center gap-3 text-xs">
                    {row.status === "uploading" || row.status === "queued" ? (
                      <Loader2 className="size-3.5 shrink-0 animate-spin text-muted-foreground" />
                    ) : row.status === "done" ? (
                      <Check className="size-3.5 shrink-0 text-emerald-600" />
                    ) : (
                      <span className="size-3.5 shrink-0 text-destructive">!</span>
                    )}
                    <span className="min-w-0 flex-1 truncate">{row.name}</span>
                    {row.status === "uploading" ? (
                      <span className="flex items-center gap-2 text-muted-foreground">
                        <span className="h-1.5 w-24 overflow-hidden rounded-full bg-muted">
                          <span className="block h-full bg-foreground" style={{ width: `${row.percent}%` }} />
                        </span>
                        {row.percent}%
                      </span>
                    ) : row.status === "queued" ? (
                      <span className="text-muted-foreground">Queued</span>
                    ) : row.status === "done" ? (
                      <span className="text-muted-foreground">Uploaded</span>
                    ) : (
                      <span className="text-destructive">{row.error ?? "Failed"}</span>
                    )}
                  </li>
                ))}
              </ul>
            ) : null}
            {isEmpty && uploads.length === 0 ? (
              <div className="flex flex-col items-center justify-center gap-2 px-6 py-16 text-center">
                <p className="text-sm text-muted-foreground">
                  No files yet. Drop files here or use + New → Upload files
                </p>
                <button
                  type="button"
                  className="text-sm text-foreground underline-offset-4 hover:underline"
                  onClick={() => setNewFolderOpen(true)}
                >
                  New folder
                </button>
              </div>
            ) : (
              <table className="w-full text-left text-sm">
                <thead className="text-xs text-muted-foreground">
                  <tr>
                    <th className="px-4 py-2 font-medium">Name</th>
                    <th className="px-4 py-2 font-medium">Type</th>
                    <th className="px-4 py-2 font-medium">Modified</th>
                    <th className="px-4 py-2 font-medium">Owner</th>
                    <th className="px-4 py-2 font-medium">
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {(browse?.children ?? []).map((child) => (
                    <tr
                      key={child.id}
                      className="cursor-pointer hover:bg-muted/50"
                      draggable
                      onDragStart={(e) => {
                        internalDrag.current = true;
                        e.dataTransfer.setData(FOLDER_MIME, child.id);
                      }}
                      onDragEnd={() => {
                        internalDrag.current = false;
                      }}
                      onDragOver={(e) => e.preventDefault()}
                      onDrop={(e) => void dropOnFolder(e, child.id)}
                      onClick={() => navigate(`/workspace/repo/${child.id}`)}
                    >
                      <td className="px-4 py-2">
                        <span className="flex items-center gap-2">
                          <Folder className="size-4 text-muted-foreground" />
                          {child.name}
                          {child.chatCount ? (
                            <span className="text-[11px] text-muted-foreground">{child.chatCount} chats</span>
                          ) : null}
                        </span>
                      </td>
                      <td className="px-4 py-2 text-muted-foreground">Folder</td>
                      <td className="px-4 py-2 text-muted-foreground">
                        {child.updatedAt ? new Date(child.updatedAt).toLocaleDateString() : "—"}
                      </td>
                      <td className="px-4 py-2 text-muted-foreground">{child.createdByName ?? "—"}</td>
                      <td className="px-4 py-2 text-right" onClick={(e) => e.stopPropagation()}>
                        <RowMenu
                          label={`Actions for ${child.name}`}
                          items={[
                            { label: "Rename", onSelect: () => beginRenameFolder(child) },
                            { label: "Move", onSelect: () => beginMoveFolder(child) },
                            { label: "Delete", onSelect: () => setDeleteTarget({ kind: "folder", folder: child, recursive: false }), destructive: true },
                          ]}
                        />
                      </td>
                    </tr>
                  ))}
                  {(browse?.documents ?? []).map((doc) => (
                    <tr
                      key={doc.id}
                      className={cn("cursor-pointer hover:bg-muted/50", selected?.id === doc.id && "bg-muted")}
                      draggable
                      onDragStart={(e) => {
                        internalDrag.current = true;
                        e.dataTransfer.setData(DOC_MIME, doc.id);
                      }}
                      onDragEnd={() => {
                        internalDrag.current = false;
                      }}
                      onClick={() => navigate(`/workspace/repo/${doc.folderId}?doc=${doc.id}`)}
                    >
                      <td className="px-4 py-2">
                        <span className="flex items-center gap-2">
                          <FileIcon type={doc.fileType} />
                          {doc.name}
                          {doc.extractionStatus && doc.extractionStatus !== "ok" ? (
                            <span className="text-[11px] text-amber-700">No extractable text</span>
                          ) : null}
                        </span>
                      </td>
                      <td className="px-4 py-2 capitalize text-muted-foreground">{doc.fileType}</td>
                      <td className="px-4 py-2 text-muted-foreground">{new Date(doc.modifiedAt).toLocaleDateString()}</td>
                      <td className="px-4 py-2 text-muted-foreground">{doc.ownerName}</td>
                      <td className="px-4 py-2 text-right" onClick={(e) => e.stopPropagation()}>
                        <RowMenu
                          label={`Actions for ${doc.name}`}
                          items={[
                            { label: "Chat about this file", onSelect: () => askMutation.mutate(doc) },
                            { label: "Re-index", onSelect: () => reindexMutation.mutate(doc) },
                            { label: "Rename", onSelect: () => beginRenameDocument(doc) },
                            { label: "Move", onSelect: () => beginMoveDocument(doc) },
                            { label: "Delete", onSelect: () => setDeleteTarget({ kind: "document", document: doc }), destructive: true },
                          ]}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            {(chatsQuery.data ?? []).length > 0 ? (
              <div className="border-t px-4 py-3">
                <p className="mb-2 text-xs font-medium text-muted-foreground">Chats in this folder</p>
                <ul className="flex flex-col gap-1">
                  {(chatsQuery.data ?? []).map((chat) => (
                    <li key={chat.id}>
                      <Link to={`/workspace/chats/${chat.id}`} className="flex items-center gap-2 text-sm hover:underline">
                        <MessageSquare className="size-3.5 text-muted-foreground" />
                        {chat.title}
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        </div>
        <aside className="overflow-y-auto p-5">
          {selected ? (
            <div className="flex flex-col gap-3">
              <h2 className="text-base font-semibold">{selected.name}</h2>
              <p className="text-xs text-muted-foreground">Preview</p>
              {selected.extractionStatus && selected.extractionStatus !== "ok" ? (
                <p className="text-sm leading-relaxed text-amber-800">
                  No extractable text. {selected.extractionNote || "This file may be a scan or image-only PDF. Try Re-index, or upload a text/DOCX copy."}
                </p>
              ) : (
                <p className="text-sm leading-relaxed text-muted-foreground">{selected.previewText || "No extracted text yet."}</p>
              )}
              <p className="text-[11px] text-muted-foreground">
                {selected.path} · {Math.max(1, Math.round(selected.sizeBytes / 1024))} KB · {selected.isIndexed ? "Indexed" : "Not indexed"} · {selected.isPublic ? "Public" : "Private"}
              </p>
              <Button className="rounded-full" onClick={() => askMutation.mutate(selected)}>
                <Sparkles /> Chat about this file
              </Button>
              <Button
                variant="outline"
                className="rounded-full"
                disabled={reindexMutation.isPending}
                onClick={() => reindexMutation.mutate(selected)}
              >
                {reindexMutation.isPending ? "Re-indexing…" : "Re-index"}
              </Button>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">Select a file to preview it, or start a chat scoped to this folder.</p>
          )}
        </aside>
      </div>

      <Dialog
        open={newFolderOpen}
        onOpenChange={(open) => {
          setNewFolderOpen(open);
          if (!open) setNewFolderName("");
        }}
      >
        <DialogContent>
          <form className="contents" onSubmit={(e) => void submitNewFolder(e)}>
            <DialogHeader>
              <DialogTitle>New folder</DialogTitle>
              <DialogDescription>Created inside {folderName}.</DialogDescription>
            </DialogHeader>
            <div className="py-3">
              <Label htmlFor="new-folder-name">Name</Label>
              <Input
                id="new-folder-name"
                className="mt-1.5"
                autoFocus
                value={newFolderName}
                onChange={(e) => setNewFolderName(e.target.value)}
                placeholder="Folder name"
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setNewFolderOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy || !newFolderName.trim()}>
                Create
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog
        open={!!renameTarget}
        onOpenChange={(open) => {
          if (!open) setRenameTarget(null);
        }}
      >
        <DialogContent>
          <form className="contents" onSubmit={(e) => void submitRename(e)}>
            <DialogHeader>
              <DialogTitle>{renameTarget?.kind === "folder" ? "Rename folder" : "Rename file"}</DialogTitle>
            </DialogHeader>
            <div className="py-3">
              <Label htmlFor="rename-item">Name</Label>
              <Input
                id="rename-item"
                className="mt-1.5"
                autoFocus
                value={renameValue}
                onChange={(e) => setRenameValue(e.target.value)}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setRenameTarget(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy || !renameValue.trim()}>
                Save
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog
        open={!!moveTarget}
        onOpenChange={(open) => {
          if (!open) {
            setMoveTarget(null);
            setMoveDestination("");
          }
        }}
      >
        <DialogContent>
          <form className="contents" onSubmit={(e) => void submitMove(e)}>
            <DialogHeader>
              <DialogTitle>Move {moveTarget?.kind === "folder" ? "folder" : "file"}</DialogTitle>
              <DialogDescription>
                Choose a destination for {moveTarget?.name ?? "this item"}.
              </DialogDescription>
            </DialogHeader>
            <div className="py-3">
              <Label htmlFor="move-destination">Destination folder</Label>
              <Select value={moveDestination || undefined} onValueChange={setMoveDestination}>
                <SelectTrigger id="move-destination" className="mt-1.5 w-full">
                  <SelectValue placeholder="Choose folder…" />
                </SelectTrigger>
                <SelectContent>
                  {moveOptions.map((folder) => (
                    <SelectItem key={folder.id} value={folder.id}>
                      {folder.path}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setMoveTarget(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy || !moveDestination}>
                Move
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog
        open={!!deleteTarget}
        onOpenChange={(open) => {
          if (!open) setDeleteTarget(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {deleteTarget?.kind === "document"
                ? `Delete “${deleteTarget.document.name}”?`
                : deleteTarget?.recursive
                  ? `Delete “${deleteTarget.folder.name}” and everything inside it?`
                  : `Delete “${deleteTarget?.folder.name}”?`}
            </DialogTitle>
            <DialogDescription>
              {deleteTarget?.kind === "document"
                ? "This file will be removed from the repository."
                : deleteTarget?.recursive
                  ? `${deleteTarget.message ?? "This folder is not empty."} Files and subfolders will be removed. Chats in this folder will be moved to My chats.`
                  : "Files and subfolders in it will be removed. Chats in this folder will be moved to My chats."}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setDeleteTarget(null)}>
              Cancel
            </Button>
            <Button type="button" variant="destructive" disabled={busy} onClick={() => void confirmDelete()}>
              {deleteTarget?.kind === "folder" && deleteTarget.recursive ? "Delete all" : "Delete"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function RowMenu({
  label,
  items,
}: {
  label: string;
  items: { label: string; onSelect: () => void; destructive?: boolean }[];
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" size="icon-xs" variant="ghost" aria-label={label}>
          <MoreHorizontal />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {items.map((item) => (
          <DropdownMenuItem
            key={item.label}
            variant={item.destructive ? "destructive" : "default"}
            onSelect={item.onSelect}
          >
            {item.label}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
