import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FileSpreadsheet, FileText, Folder, Image as ImageIcon, Sparkles } from "lucide-react";
import { useMemo, useRef } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { workspaceApi, type RepoDocument } from "../api";

function FileIcon({ type }: { type: string }) {
  if (type === "spreadsheet") return <FileSpreadsheet className="size-4" />;
  if (type === "image") return <ImageIcon className="size-4" />;
  return <FileText className="size-4" />;
}

export function WorkspaceRepoBrowsePage() {
  const { folderId } = useParams();
  const [params] = useSearchParams();
  const documentId = params.get("doc") ?? params.get("documentId") ?? undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const fileRef = useRef<HTMLInputElement | null>(null);

  const browseQuery = useQuery({
    queryKey: ["ws-repo", folderId, documentId],
    queryFn: () => workspaceApi.browse(folderId, documentId),
  });
  const treeQuery = useQuery({ queryKey: ["ws-sidebar"], queryFn: workspaceApi.sidebar });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => workspaceApi.upload(browseQuery.data!.folder.id, file),
    onSuccess: (doc) => {
      void queryClient.invalidateQueries({ queryKey: ["ws-repo"] });
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      navigate(`/workspace/repo/${doc.folderId}?doc=${doc.id}`);
    },
  });

  const askMutation = useMutation({
    mutationFn: async (doc: RepoDocument) => {
      const chat = await workspaceApi.createChat({ title: `Ask about ${doc.name}` });
      await workspaceApi.send(chat.id, `What does ${doc.name} say? Cite the repository.`);
      return chat;
    },
    onSuccess: (chat) => navigate(`/workspace/chats/${chat.id}`),
  });

  const browse = browseQuery.data;
  const folders = treeQuery.data?.repository ?? [];
  const rootId = folders.find((f) => !f.parentId)?.id;
  const selected = browse?.selected;

  const folderKids = useMemo(
    () => (parent: string | null) => folders.filter((f) => (f.parentId ?? null) === parent),
    [folders],
  );

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center justify-between gap-3 px-6 py-3">
        <div className="flex min-w-0 flex-wrap items-center gap-1 text-sm">
          {(browse?.breadcrumbs ?? []).map((crumb, index) => (
            <span key={crumb.id} className="flex items-center gap-1">
              {index > 0 ? <span className="text-muted-foreground">/</span> : null}
              <Link to={`/workspace/repo/${crumb.id}`} className="hover:underline">
                {crumb.name}
              </Link>
            </span>
          ))}
          {browse?.folder.isPublic ? (
            <span className="ml-2 rounded-full border px-2 py-0.5 text-[11px] text-muted-foreground">Public · Indexed</span>
          ) : null}
        </div>
        <div className="flex items-center gap-2">
          <Input
            placeholder={`Search in ${browse?.folder.name ?? "folder"}`}
            className="h-9 w-56 rounded-full"
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                const q = (e.target as HTMLInputElement).value.trim();
                navigate(`/workspace/search?q=${encodeURIComponent(q)}&folderId=${browse?.folder.id ?? ""}`);
              }
            }}
          />
          <input
            ref={fileRef}
            type="file"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) uploadMutation.mutate(file);
              e.target.value = "";
            }}
          />
          <Button size="sm" className="rounded-full" onClick={() => fileRef.current?.click()}>
            Upload
          </Button>
        </div>
      </header>
      <div className="grid min-h-0 flex-1 grid-cols-[220px_1fr_280px] divide-x">
        <nav className="overflow-y-auto p-3 text-sm">
          <FolderNav folders={folders} kids={folderKids} currentId={browse?.folder.id} rootId={rootId ?? null} />
        </nav>
        <div className="overflow-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-xs text-muted-foreground">
              <tr>
                <th className="px-4 py-2 font-medium">Name</th>
                <th className="px-4 py-2 font-medium">Type</th>
                <th className="px-4 py-2 font-medium">Modified</th>
                <th className="px-4 py-2 font-medium">Owner</th>
              </tr>
            </thead>
            <tbody>
              {(browse?.children ?? []).map((child) => (
                <tr key={child.id} className="cursor-pointer hover:bg-muted/50" onClick={() => navigate(`/workspace/repo/${child.id}`)}>
                  <td className="px-4 py-2">
                    <span className="flex items-center gap-2">
                      <Folder className="size-4 text-muted-foreground" />
                      {child.name}
                    </span>
                  </td>
                  <td className="px-4 py-2 text-muted-foreground">Folder</td>
                  <td className="px-4 py-2 text-muted-foreground">—</td>
                  <td className="px-4 py-2 text-muted-foreground">—</td>
                </tr>
              ))}
              {(browse?.children ?? []).length === 0 && (browse?.documents ?? []).length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-sm text-muted-foreground">
                    This folder is empty. Upload a document, spreadsheet, or PDF so Workspace chat can cite it.
                  </td>
                </tr>
              ) : null}
              {(browse?.documents ?? []).map((doc) => (
                <tr
                  key={doc.id}
                  className={cn("cursor-pointer hover:bg-muted/50", selected?.id === doc.id && "bg-muted")}
                  onClick={() => navigate(`/workspace/repo/${doc.folderId}?doc=${doc.id}`)}
                >
                  <td className="px-4 py-2">
                    <span className="flex items-center gap-2">
                      <FileIcon type={doc.fileType} />
                      {doc.name}
                    </span>
                  </td>
                  <td className="px-4 py-2 capitalize text-muted-foreground">{doc.fileType}</td>
                  <td className="px-4 py-2 text-muted-foreground">{new Date(doc.modifiedAt).toLocaleDateString()}</td>
                  <td className="px-4 py-2 text-muted-foreground">{doc.ownerName}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <aside className="overflow-y-auto p-5">
          {selected ? (
            <div className="flex flex-col gap-3">
              <h2 className="text-base font-semibold">{selected.name}</h2>
              <p className="text-xs text-muted-foreground">Preview</p>
              <p className="text-sm leading-relaxed text-muted-foreground">{selected.previewText || "No extracted text yet."}</p>
              <p className="text-[11px] text-muted-foreground">
                {selected.path} · {Math.max(1, Math.round(selected.sizeBytes / 1024))} KB · {selected.isIndexed ? "Indexed" : "Not indexed"} · {selected.isPublic ? "Public" : "Private"}
              </p>
              <Button className="rounded-full" onClick={() => askMutation.mutate(selected)}>
                <Sparkles /> Ask agent about this
              </Button>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">Select a file to preview it.</p>
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
}: {
  folders: { id: string; parentId?: string | null; name: string }[];
  kids: (parent: string | null) => { id: string; parentId?: string | null; name: string }[];
  currentId?: string;
  rootId: string | null;
  parentId?: string | null;
}) {
  const nodes = kids(parentId ?? rootId);
  const startParent = parentId ?? rootId;
  const list = parentId === null && rootId ? kids(rootId) : nodes;
  void folders;
  void startParent;
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
          >
            <Folder className="size-3.5 text-muted-foreground" />
            {folder.name}
          </Link>
          <div className="pl-3">
            <FolderNav folders={folders} kids={kids} currentId={currentId} rootId={rootId} parentId={folder.id} />
          </div>
        </li>
      ))}
    </ul>
  );
}
