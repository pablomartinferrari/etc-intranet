import {
  ChevronRight,
  FileText,
  Folder,
  Home,
  MessageSquare,
  Plus,
  Search,
  Share2,
  Users,
} from "lucide-react";
import { useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { NavLink, useNavigate } from "react-router-dom";

import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { workspaceApi, type RepoFolder, type SidebarPayload, type WorkspaceChat, type WorkspaceProject } from "./api";

function locationLabel(chat: WorkspaceChat) {
  return chat.folderPath || chat.projectName || undefined;
}

function initial(name: string) {
  return (name.trim()[0] ?? "?").toUpperCase();
}

function SectionLabel({ children }: { children: string }) {
  return (
    <p className="px-2 pt-4 pb-1 text-[11px] font-medium tracking-wide text-sidebar-foreground/50">
      {children}
    </p>
  );
}

function Row({
  to,
  icon,
  label,
  meta,
  active,
}: {
  to: string;
  icon: ReactNode;
  label: string;
  meta?: string;
  active?: boolean;
}) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        cn(
          "flex items-center gap-2 rounded-md px-2 py-1.5 text-[13px] text-sidebar-foreground/85 hover:bg-sidebar-accent",
          (isActive || active) && "bg-sidebar-accent text-sidebar-accent-foreground",
        )
      }
    >
      <span className="flex size-5 shrink-0 items-center justify-center text-sidebar-foreground/60">{icon}</span>
      <span className="min-w-0 flex-1 truncate">{label}</span>
      {meta ? <span className="truncate text-[11px] text-sidebar-foreground/45">{meta}</span> : null}
    </NavLink>
  );
}

function folderVisible(folder: RepoFolder, folders: RepoFolder[], chats: WorkspaceChat[], query: string): boolean {
  if (!query) return true;
  if (folder.name.toLowerCase().includes(query) || folder.path.toLowerCase().includes(query)) return true;
  if (chats.some((c) => c.folderId === folder.id && c.title.toLowerCase().includes(query))) return true;
  return folders.some((child) => child.parentId === folder.id && folderVisible(child, folders, chats, query));
}

function FolderTree({
  folders,
  chats,
  parentId,
  query,
}: {
  folders: RepoFolder[];
  chats: WorkspaceChat[];
  parentId: string | null;
  query: string;
}) {
  const children = folders
    .filter((f) => (f.parentId ?? null) === parentId)
    .filter((f) => folderVisible(f, folders, chats, query))
    .sort((a, b) => a.name.localeCompare(b.name));
  return (
    <ul className="flex flex-col">
      {children.map((folder) => (
        <FolderNode key={folder.id} folder={folder} folders={folders} chats={chats} query={query} />
      ))}
    </ul>
  );
}

function FolderNode({
  folder,
  folders,
  chats,
  query,
}: {
  folder: RepoFolder;
  folders: RepoFolder[];
  chats: WorkspaceChat[];
  query: string;
}) {
  const folderChats = chats.filter((c) => c.folderId === folder.id);
  const kids = folders.filter((f) => f.parentId === folder.id);
  const [open, setOpen] = useState(true);
  const visibleChats = folderChats.filter((c) => !query || c.title.toLowerCase().includes(query) || folder.name.toLowerCase().includes(query));
  return (
    <li className="pl-1">
      <div className="flex items-center">
        {kids.length > 0 || folderChats.length > 0 ? (
          <button
            type="button"
            className="flex size-6 items-center justify-center text-sidebar-foreground/50"
            onClick={() => setOpen((v) => !v)}
            aria-label={open ? "Collapse folder" : "Expand folder"}
          >
            <ChevronRight className={cn("size-3.5 transition", open && "rotate-90")} />
          </button>
        ) : (
          <span className="size-6" />
        )}
        <div className="min-w-0 flex-1">
          <Row
            to={`/workspace/repo/${folder.id}`}
            icon={<Folder className="size-3.5" />}
            label={folder.name}
            meta={folder.chatCount ? `${folder.chatCount}` : undefined}
          />
        </div>
      </div>
      {open ? (
        <>
          {visibleChats.map((chat) => (
            <div key={chat.id} className="pl-6">
              <Row
                to={`/workspace/chats/${chat.id}`}
                icon={<MessageSquare className="size-3.5" />}
                label={chat.title}
              />
            </div>
          ))}
          {kids.length > 0 ? (
            <FolderTree folders={folders} chats={chats} parentId={folder.id} query={query} />
          ) : null}
        </>
      ) : null}
    </li>
  );
}

export function AgenticSidebar({
  data,
  onNewChat,
}: {
  data?: SidebarPayload;
  onNewChat: () => void;
}) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState("");
  const [projectName, setProjectName] = useState("");
  const [creatingProject, setCreatingProject] = useState(false);
  const q = filter.trim().toLowerCase();

  const projects = data?.projects ?? [];
  const scoped = data?.scopedChats ?? [];
  const chatsByProject = useMemo(() => {
    const map = new Map<string, WorkspaceChat[]>();
    for (const chat of scoped) {
      if (!chat.projectId) continue;
      const list = map.get(chat.projectId) ?? [];
      list.push(chat);
      map.set(chat.projectId, list);
    }
    return map;
  }, [scoped]);

  const unfiled = data?.myChats ?? [];
  const folderChats = scoped.filter((c) => c.folderId);
  const rootFolder = (data?.repository ?? []).find((f) => !f.parentId);
  const rootScopedChats = scoped.filter((c) => c.folderId && c.folderId === rootFolder?.id);
  const visibleProjects = projects.filter(
    (p) =>
      !q ||
      p.name.toLowerCase().includes(q) ||
      (chatsByProject.get(p.id) ?? []).some((c) => c.title.toLowerCase().includes(q)),
  );
  const searchHits = q
    ? uniqueChats([
        ...unfiled,
        ...scoped,
        ...(data?.sharedWithMe ?? []),
        ...(data?.sharedByMe ?? []),
      ]).filter(
        (c) =>
          c.title.toLowerCase().includes(q) ||
          (c.folderPath ?? "").toLowerCase().includes(q) ||
          (c.projectName ?? "").toLowerCase().includes(q),
      )
    : [];

  return (
    <aside className="dark flex h-full min-h-0 w-[260px] shrink-0 flex-col bg-sidebar text-sidebar-foreground">
      <div className="flex items-center gap-2 px-3 py-3">
        <button
          type="button"
          className="flex size-7 items-center justify-center rounded-md text-sidebar-foreground/70 hover:bg-sidebar-accent"
          onClick={() => navigate("/")}
          title="Intranet home"
        >
          <span className="size-4 rounded-full border border-sidebar-foreground/40" />
        </button>
        <span className="text-sm font-medium">Workspace</span>
      </div>
      <div className="flex flex-col gap-1 px-2">
        <button
          type="button"
          onClick={onNewChat}
          className="flex items-center gap-2 rounded-md px-2 py-1.5 text-[13px] text-sidebar-foreground/85 hover:bg-sidebar-accent"
        >
          <Plus className="size-4" />
          New chat
        </button>
        {creatingProject ? (
          <form
            className="flex flex-col gap-1 px-1"
            onSubmit={async (event) => {
              event.preventDefault();
              const name = projectName.trim();
              if (!name) return;
              const project = await workspaceApi.createProject({ name });
              setProjectName("");
              setCreatingProject(false);
              await queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
              navigate(`/workspace/projects/${project.id}`);
            }}
          >
            <Input
              autoFocus
              value={projectName}
              onChange={(e) => setProjectName(e.target.value)}
              placeholder="Project name"
              className="h-8 border-sidebar-border bg-sidebar-accent/40 text-xs text-sidebar-foreground"
            />
            <div className="flex gap-1">
              <button type="submit" className="rounded-md px-2 py-1 text-[11px] hover:bg-sidebar-accent">
                Create
              </button>
              <button
                type="button"
                className="rounded-md px-2 py-1 text-[11px] hover:bg-sidebar-accent"
                onClick={() => {
                  setCreatingProject(false);
                  setProjectName("");
                }}
              >
                Cancel
              </button>
            </div>
          </form>
        ) : (
          <button
            type="button"
            onClick={() => setCreatingProject(true)}
            className="flex items-center gap-2 rounded-md px-2 py-1.5 text-[13px] text-sidebar-foreground/85 hover:bg-sidebar-accent"
          >
            <Folder className="size-4" />
            New project
          </button>
        )}
        <div className="relative px-1 py-1">
          <Search className="pointer-events-none absolute top-1/2 left-3 size-3.5 -translate-y-1/2 text-sidebar-foreground/40" />
          <Input
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            placeholder="Search chats & repository"
            className="h-8 border-sidebar-border bg-sidebar-accent/40 pl-8 text-xs text-sidebar-foreground placeholder:text-sidebar-foreground/40"
          />
        </div>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-3">
        {searchHits.length > 0 ? (
          <>
            <SectionLabel>Matching chats</SectionLabel>
            {searchHits.map((chat) => (
              <Row
                key={`hit-${chat.id}`}
                to={`/workspace/chats/${chat.id}`}
                icon={<MessageSquare className="size-3.5" />}
                label={chat.title}
                meta={locationLabel(chat)}
              />
            ))}
          </>
        ) : null}
        <SectionLabel>My chats</SectionLabel>
        <Row to="/workspace" icon={<Home className="size-3.5" />} label="Home · My day" />
        {unfiled
          .filter((c) => !q || c.title.toLowerCase().includes(q))
          .map((chat) => (
            <Row
              key={chat.id}
              to={`/workspace/chats/${chat.id}`}
              icon={<MessageSquare className="size-3.5" />}
              label={chat.title}
            />
          ))}
        {unfiled.length === 0 ? (
          <p className="px-2 py-1 text-[11px] text-sidebar-foreground/45">Unscoped chats live here.</p>
        ) : null}

        <SectionLabel>Projects</SectionLabel>
        {visibleProjects.length === 0 ? (
          <p className="px-2 py-1 text-[11px] text-sidebar-foreground/45">No projects yet.</p>
        ) : null}
        {visibleProjects.map((project) => (
          <ProjectBlock
            key={project.id}
            project={project}
            chats={(chatsByProject.get(project.id) ?? []).filter((c) => !q || c.title.toLowerCase().includes(q))}
          />
        ))}

        <SectionLabel>Shared with me</SectionLabel>
        {(data?.sharedWithMe ?? []).length === 0 ? (
          <p className="px-2 py-1 text-[11px] text-sidebar-foreground/45">Nothing shared with you yet.</p>
        ) : null}
        {(data?.sharedWithMe ?? [])
          .filter((c) => !q || c.title.toLowerCase().includes(q))
          .map((chat) => (
            <Row
              key={chat.id}
              to={`/workspace/chats/${chat.id}`}
              icon={<Users className="size-3.5" />}
              label={chat.title}
              meta={locationLabel(chat)}
            />
          ))}

        <SectionLabel>Shared by me</SectionLabel>
        {(data?.sharedByMe ?? [])
          .filter((c) => !q || c.title.toLowerCase().includes(q))
          .map((chat) => (
            <Row
              key={chat.id}
              to={`/workspace/chats/${chat.id}`}
              icon={<Share2 className="size-3.5" />}
              label={chat.title}
              meta={locationLabel(chat) ?? (chat.shareCount ? `${chat.shareCount} people` : undefined)}
            />
          ))}

        <SectionLabel>Repository</SectionLabel>
        <Row to="/workspace/repo" icon={<Folder className="size-3.5" />} label="Browse & upload" />
        {rootScopedChats
          .filter((c) => !q || c.title.toLowerCase().includes(q))
          .map((chat) => (
            <div key={chat.id} className="pl-4">
              <Row
                to={`/workspace/chats/${chat.id}`}
                icon={<MessageSquare className="size-3.5" />}
                label={chat.title}
              />
            </div>
          ))}
        <FolderTree
          folders={(data?.repository ?? []).filter((f) => f.name !== "Root" || !!f.parentId)}
          chats={folderChats}
          parentId={rootFolder?.id ?? null}
          query={q}
        />
      </div>
      <div className="flex items-center gap-2 border-t border-sidebar-border px-3 py-3">
        <span className="flex size-7 items-center justify-center rounded-full bg-sidebar-accent text-xs font-medium">
          {initial(data?.me.displayName ?? "You")}
        </span>
        <span className="truncate text-xs">{data?.me.displayName ?? "You"}</span>
      </div>
    </aside>
  );
}

function uniqueChats(chats: WorkspaceChat[]) {
  const seen = new Set<string>();
  return chats.filter((chat) => {
    if (seen.has(chat.id)) return false;
    seen.add(chat.id);
    return true;
  });
}

function ProjectBlock({ project, chats }: { project: WorkspaceProject; chats: WorkspaceChat[] }) {
  const [open, setOpen] = useState(true);
  return (
    <div>
      <div className="flex items-center">
        <button
          type="button"
          className="flex size-6 items-center justify-center text-sidebar-foreground/50"
          onClick={() => setOpen((v) => !v)}
          aria-label={open ? "Collapse project" : "Expand project"}
        >
          <ChevronRight className={cn("size-3.5 transition", open && "rotate-90")} />
        </button>
        <NavLink
          to={`/workspace/projects/${project.id}`}
          className={({ isActive }) =>
            cn(
              "flex min-w-0 flex-1 items-center gap-2 rounded-md px-1 py-1.5 text-[13px] hover:bg-sidebar-accent",
              isActive && "bg-sidebar-accent",
            )
          }
        >
          <Folder className="size-3.5 shrink-0 text-sidebar-foreground/60" />
          <span className="min-w-0 flex-1 truncate">{project.name}</span>
          {project.chatCount ? (
            <span className="text-[11px] text-sidebar-foreground/45">{project.chatCount}</span>
          ) : null}
        </NavLink>
      </div>
      {open
        ? chats.map((chat) => (
            <div key={chat.id} className="pl-6">
              <Row
                to={`/workspace/chats/${chat.id}`}
                icon={<FileText className="size-3.5" />}
                label={chat.title}
              />
            </div>
          ))
        : null}
    </div>
  );
}
