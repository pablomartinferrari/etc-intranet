import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FileSpreadsheet, FileText, Folder, Image as ImageIcon, MessageSquare, Plus, Sparkles } from "lucide-react";
import { useMemo, useRef, useState, type DragEvent } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { workspaceApi, type RepoDocument, type RepoFolder } from "../api";

function FileIcon({ type }: { type: string }) {
  if (type === "spreadsheet") return <FileSpreadsheet className="size-4" />;
  if (type === "image") return <ImageIcon className="size-4" />;
  return <FileText className="size-4" />;
}

const FOLDER_MIME = "application/x-workspace-folder";
const DOC_MIME = "application/x-workspace-document";

export function WorkspaceRepoBrowsePage() {
  const { folderId } = useParams();
  const [params] = useSearchParams();
  const documentId = params.get("doc") ?? params.get("documentId") ?? undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const fileRef = useRef<HTMLInputElement | null>(null);
  const [newFolderName, setNewFolderName] = useState("");
  const [creatingFolder, setCreatingFolder] = useState(false);
  const [moveTarget, setMoveTarget] = useState<{ kind: "folder" | "document"; id: string } | null>(null);
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

  const uploadMutation = useMutation({
    mutationFn: async (files: File[]) => {
      const folder = browseQuery.data!.folder.id;
      let last: RepoDocument | null = null;
      for (const file of files) {
        last = await workspaceApi.upload(folder, file);
      }
      return last;
    },
    onSuccess: (doc) => {
      void refresh();
      if (doc) navigate(`/workspace/repo/${doc.folderId}?doc=${doc.id}`);
    },
    onError: (err: Error) => setError(err.message),
  });

  const createFolderMutation = useMutation({
    mutationFn: (name: string) => workspaceApi.createFolder(browseQuery.data!.folder.id, name),
    onSuccess: (folder) => {
      setNewFolderName("");
      setCreatingFolder(false);
      void refresh();
      navigate(`/workspace/repo/${folder.id}`);
    },
    onError: (err: Error) => setError(err.message),
  });

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
  });

  const browse = browseQuery.data;
  const folders = treeQuery.data?.repository ?? [];
  const rootId = folders.find((f) => !f.parentId)?.id;
  const selected = browse?.selected;
  const isRoot = !browse?.folder.parentId;

  const folderKids = useMemo(
    () => (parent: string | null) => folders.filter((f) => (f.parentId ?? null) === parent),
    [folders],
  );

  async function renameFolder(folder: RepoFolder) {
    const name = window.prompt("Rename folder", folder.name)?.trim();
    if (!name || name === folder.name) return;
    try {
      await workspaceApi.renameFolder(folder.id, name);
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not rename folder.");
    }
  }

  async function renameDocument(doc: RepoDocument) {
    const name = window.prompt("Rename file", doc.name)?.trim();
    if (!name || name === doc.name) return;
    try {
      await workspaceApi.renameDocument(doc.id, name);
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not rename file.");
    }
  }

  async function deleteFolder(folder: RepoFolder) {
    const ok = window.confirm(
      `Delete “${folder.name}”? Files and subfolders in it will be removed. Chats in this folder will be moved to My chats.`,
    );
    if (!ok) return;
    try {
      await workspaceApi.deleteFolder(folder.id, false);
      await refresh();
      navigate(`/workspace/repo/${folder.parentId ?? ""}`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Could not delete folder.";
      if (message.toLowerCase().includes("not empty") || message.toLowerCase().includes("recursive")) {
        const recurse = window.confirm(
          `${message}\n\nDelete this folder and everything inside it? Chats in this folder will be moved to My chats.`,
        );
        if (!recurse) return;
        try {
          await workspaceApi.deleteFolder(folder.id, true);
          await refresh();
          navigate(`/workspace/repo/${folder.parentId ?? ""}`);
        } catch (inner) {
          setError(inner instanceof Error ? inner.message : message);
        }
        return;
      }
      setError(message);
    }
  }

  async function deleteDocument(doc: RepoDocument) {
    if (!window.confirm(`Delete “${doc.name}”?`)) return;
    try {
      await workspaceApi.deleteDocument(doc.id);
      await refresh();
      navigate(`/workspace/repo/${doc.folderId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not delete file.");
    }
  }

  async function moveItem(destinationId: string) {
    if (!moveTarget) return;
    try {
      if (moveTarget.kind === "folder") {
        await workspaceApi.moveFolder(moveTarget.id, destinationId);
      } else {
        await workspaceApi.moveDocument(moveTarget.id, destinationId);
      }
      setMoveTarget(null);
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not move item.");
    }
  }

  async function dropOnFolder(event: DragEvent, destinationId: string) {
    event.preventDefault();
    const folderDrag = event.dataTransfer.getData(FOLDER_MIME);
    const docDrag = event.dataTransfer.getData(DOC_MIME);
    try {
      if (folderDrag && folderDrag !== destinationId) {
        await workspaceApi.moveFolder(folderDrag, destinationId);
        await refresh();
      } else if (docDrag) {
        await workspaceApi.moveDocument(docDrag, destinationId);
        await refresh();
      } else if (event.dataTransfer.files.length && destinationId === browse?.folder.id) {
        uploadMutation.mutate(Array.from(event.dataTransfer.files));
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not move item.");
    }
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
                className="hover:underline"
                onDragOver={(e) => e.preventDefault()}
                onDrop={(e) => void dropOnFolder(e, crumb.id)}
              >
                {crumb.name}
              </Link>
            </span>
          ))}
          {browse?.folder.isPublic ? (
            <span className="ml-2 rounded-full border px-2 py-0.5 text-[11px] text-muted-foreground">Public · Indexed</span>
          ) : null}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Input
            placeholder={`Search in ${browse?.folder.path ?? browse?.folder.name ?? "folder"}`}
            className="h-9 w-64 rounded-full"
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                const q = (e.target as HTMLInputElement).value.trim();
                if (!q) return;
                navigate(`/workspace/search?q=${encodeURIComponent(q)}&folderId=${browse?.folder.id ?? ""}`);
              }
            }}
          />
          <input
            ref={fileRef}
            type="file"
            multiple
            className="hidden"
            onChange={(e) => {
              const files = Array.from(e.target.files ?? []);
              if (files.length) uploadMutation.mutate(files);
              e.target.value = "";
            }}
          />
          {creatingFolder ? (
            <form
              className="flex items-center gap-1"
              onSubmit={(event) => {
                event.preventDefault();
                const name = newFolderName.trim();
                if (name) createFolderMutation.mutate(name);
              }}
            >
              <Input
                autoFocus
                value={newFolderName}
                onChange={(e) => setNewFolderName(e.target.value)}
                placeholder="Folder name"
                className="h-9 w-40"
              />
              <Button type="submit" size="sm" className="rounded-full">
                Create
              </Button>
              <Button type="button" size="sm" variant="ghost" onClick={() => setCreatingFolder(false)}>
                Cancel
              </Button>
            </form>
          ) : (
            <Button size="sm" variant="outline" className="rounded-full" onClick={() => setCreatingFolder(true)}>
              <Plus /> New folder
            </Button>
          )}
          <Button size="sm" variant="outline" className="rounded-full" onClick={() => newChatMutation.mutate()}>
            <MessageSquare /> New chat
          </Button>
          <Button size="sm" className="rounded-full" onClick={() => fileRef.current?.click()}>
            Upload
          </Button>
          {browse && !isRoot ? (
            <>
              <Button size="sm" variant="ghost" onClick={() => void renameFolder(browse.folder)}>
                Rename
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setMoveTarget({ kind: "folder", id: browse.folder.id })}>
                Move to…
              </Button>
              <Button size="sm" variant="ghost" onClick={() => void deleteFolder(browse.folder)}>
                Delete
              </Button>
            </>
          ) : null}
        </div>
      </header>
      {error ? <p className="px-6 pb-2 text-xs text-destructive">{error}</p> : null}
      {moveTarget ? (
        <div className="flex items-center gap-2 px-6 pb-2 text-sm">
          <span>Move to</span>
          <select
            className="h-8 rounded-md border bg-background px-2 text-sm"
            defaultValue=""
            onChange={(e) => {
              if (e.target.value) void moveItem(e.target.value);
            }}
          >
            <option value="">Choose folder…</option>
            {folders.map((folder) => (
              <option key={folder.id} value={folder.id}>
                {folder.path}
              </option>
            ))}
          </select>
          <Button size="sm" variant="ghost" onClick={() => setMoveTarget(null)}>
            Cancel
          </Button>
        </div>
      ) : null}
      <div className="grid min-h-0 flex-1 grid-cols-[220px_1fr_280px] divide-x">
        <nav className="overflow-y-auto p-3 text-sm">
          <FolderNav
            folders={folders}
            kids={folderKids}
            currentId={browse?.folder.id}
            rootId={rootId ?? null}
            onMoveFolder={async (id, dest) => {
              try {
                await workspaceApi.moveFolder(id, dest);
                await refresh();
              } catch (err) {
                setError(err instanceof Error ? err.message : "Could not move folder.");
              }
            }}
          />
        </nav>
        <div
          className="overflow-auto"
          onDragOver={(e) => e.preventDefault()}
          onDrop={(e) => browse && void dropOnFolder(e, browse.folder.id)}
        >
          <table className="w-full text-left text-sm">
            <thead className="text-xs text-muted-foreground">
              <tr>
                <th className="px-4 py-2 font-medium">Name</th>
                <th className="px-4 py-2 font-medium">Type</th>
                <th className="px-4 py-2 font-medium">Modified</th>
                <th className="px-4 py-2 font-medium">Owner</th>
                <th className="px-4 py-2 font-medium"> </th>
              </tr>
            </thead>
            <tbody>
              {(browse?.children ?? []).map((child) => (
                <tr
                  key={child.id}
                  className="cursor-pointer hover:bg-muted/50"
                  draggable
                  onDragStart={(e) => e.dataTransfer.setData(FOLDER_MIME, child.id)}
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
                    <button type="button" className="mr-2 text-xs hover:underline" onClick={() => void renameFolder(child)}>
                      Rename
                    </button>
                    <button
                      type="button"
                      className="mr-2 text-xs hover:underline"
                      onClick={() => setMoveTarget({ kind: "folder", id: child.id })}
                    >
                      Move
                    </button>
                    <button type="button" className="text-xs hover:underline" onClick={() => void deleteFolder(child)}>
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
              {(browse?.children ?? []).length === 0 && (browse?.documents ?? []).length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-sm text-muted-foreground">
                    This folder is empty. Upload files or create a nested folder. New chats started here stay in this folder,
                    not in My chats.
                  </td>
                </tr>
              ) : null}
              {(browse?.documents ?? []).map((doc) => (
                <tr
                  key={doc.id}
                  className={cn("cursor-pointer hover:bg-muted/50", selected?.id === doc.id && "bg-muted")}
                  draggable
                  onDragStart={(e) => e.dataTransfer.setData(DOC_MIME, doc.id)}
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
                    <button type="button" className="mr-2 text-xs hover:underline" onClick={() => askMutation.mutate(doc)}>
                      Chat about this
                    </button>
                    <button type="button" className="mr-2 text-xs hover:underline" onClick={() => reindexMutation.mutate(doc)}>
                      Re-index
                    </button>
                    <button type="button" className="mr-2 text-xs hover:underline" onClick={() => void renameDocument(doc)}>
                      Rename
                    </button>
                    <button
                      type="button"
                      className="mr-2 text-xs hover:underline"
                      onClick={() => setMoveTarget({ kind: "document", id: doc.id })}
                    >
                      Move
                    </button>
                    <button type="button" className="text-xs hover:underline" onClick={() => void deleteDocument(doc)}>
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
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
    </div>
  );
}

function FolderNav({
  folders,
  kids,
  currentId,
  rootId,
  parentId = null,
  onMoveFolder,
}: {
  folders: { id: string; parentId?: string | null; name: string }[];
  kids: (parent: string | null) => { id: string; parentId?: string | null; name: string }[];
  currentId?: string;
  rootId: string | null;
  parentId?: string | null;
  onMoveFolder: (id: string, dest: string) => void;
}) {
  const nodes = kids(parentId ?? rootId);
  const list = parentId === null && rootId ? kids(rootId) : nodes;
  return (
    <ul className="flex flex-col">
      {list.map((folder) => (
        <li key={folder.id}>
          <Link
            to={`/workspace/repo/${folder.id}`}
            className={cn(
              "flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-muted",
              currentId === folder.id && "bg-muted font-medium",
            )}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault();
              const dragged = e.dataTransfer.getData(FOLDER_MIME);
              if (dragged) onMoveFolder(dragged, folder.id);
            }}
          >
            <Folder className="size-3.5 text-muted-foreground" />
            {folder.name}
          </Link>
          <div className="pl-3">
            <FolderNav
              folders={folders}
              kids={kids}
              currentId={currentId}
              rootId={rootId}
              parentId={folder.id}
              onMoveFolder={onMoveFolder}
            />
          </div>
        </li>
      ))}
    </ul>
  );
}
