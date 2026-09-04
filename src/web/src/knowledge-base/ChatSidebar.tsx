import {
  ArrowLeft,
  FileText,
  FolderOpen,
  Lightbulb,
  MessageSquare,
  MoreHorizontal,
  PanelLeft,
  Pencil,
  Plus,
  Search,
  Share2,
  Trash2,
} from "lucide-react";
import { useMemo, useState, type ReactNode } from "react";
import { Link as RouterLink } from "react-router-dom";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";
import { canManageProject, type ChatSession, type Project } from "./api/knowledge";
import { groupProjectsByArea, projectIconTone, shareBadgeLabel } from "./projectGroups";

export function ChatSidebar({
  projects,
  selectedProjectId,
  onSelectProject,
  onNewProject,
  onNewChat,
  onEditProject,
  onShareProject,
  onDeleteProject,
  onOpenFiles,
  onOpenPrompts,
  sessions,
  sessionsLoading,
  sessionId,
  onSelectSession,
  onRenameSession,
  collapsed = false,
  onCollapsedChange,
  showHomeLink = true,
  showCollapseToggle = true,
  loading,
  className,
}: {
  projects: Project[];
  selectedProjectId?: string;
  onSelectProject: (id: string) => void;
  onNewProject: () => void;
  onNewChat: () => void;
  onEditProject: (project: Project) => void;
  onShareProject: (project: Project) => void;
  onDeleteProject: (project: Project) => void;
  onOpenFiles: (project: Project) => void;
  onOpenPrompts: (project: Project) => void;
  sessions: ChatSession[];
  sessionsLoading?: boolean;
  sessionId?: string;
  onSelectSession: (id: string) => void;
  onRenameSession: (session: ChatSession) => void;
  collapsed?: boolean;
  onCollapsedChange?: (collapsed: boolean) => void;
  showHomeLink?: boolean;
  showCollapseToggle?: boolean;
  loading?: boolean;
  className?: string;
}) {
  const [filter, setFilter] = useState("");
  const [filterOpen, setFilterOpen] = useState(false);

  const groups = useMemo(() => {
    const q = filter.trim().toLowerCase();
    const filtered = q
      ? projects.filter(
          (p) =>
            p.name.toLowerCase().includes(q) || (p.area ?? "").toLowerCase().includes(q),
        )
      : projects;
    return groupProjectsByArea(filtered);
  }, [filter, projects]);

  const showAreaHeaders =
    groups.length > 1 || (groups.length === 1 && groups[0].key !== "__none__");

  return (
    <nav
      className={cn(
        "flex h-full min-h-0 shrink-0 flex-col bg-sidebar text-sidebar-foreground",
        collapsed ? "w-[52px] px-1.5 py-2" : "w-full px-2 py-2 md:w-[260px]",
        className,
      )}
    >
      <div className={cn("flex items-center gap-1", collapsed ? "flex-col" : "justify-between")}>
        {showHomeLink && (
          <SidebarIconLink to="/" label="Back to home" collapsed={collapsed}>
            <ArrowLeft className="size-4" />
            {!collapsed && <span className="truncate">Home</span>}
          </SidebarIconLink>
        )}
        <div className={cn("flex items-center gap-0.5", collapsed && "flex-col")}>
          {!collapsed && (
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label={filterOpen ? "Hide project search" : "Search projects"}
              aria-pressed={filterOpen}
              onClick={() => setFilterOpen((open) => !open)}
            >
              <Search />
            </Button>
          )}
          {showCollapseToggle && onCollapsedChange && (
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
              onClick={() => onCollapsedChange(!collapsed)}
            >
              <PanelLeft />
            </Button>
          )}
        </div>
      </div>

      <div className="mt-2 flex flex-col gap-0.5">
        <SidebarRow
          collapsed={collapsed}
          label="New chat"
          onClick={onNewChat}
          icon={<Plus className="size-4" />}
          prominent
        />
        <SidebarRow
          collapsed={collapsed}
          label="Sources"
          to="/knowledge/sources"
          icon={<FolderOpen className="size-4" />}
        />
      </div>

      {!collapsed && filterOpen && (
        <div className="relative mt-2">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            placeholder="Search projects"
            className="h-8 bg-background pl-8"
            aria-label="Search projects"
            autoFocus
          />
        </div>
      )}

      <div className="mt-3 flex min-h-0 flex-1 flex-col">
        {!collapsed && (
          <div className="mb-1 flex items-center gap-1 px-2">
            <p className="flex-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              Projects
            </p>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title="New project"
              aria-label="New project"
              onClick={onNewProject}
            >
              <Plus />
            </Button>
          </div>
        )}

        <div className="flex min-h-0 flex-1 flex-col gap-1 overflow-y-auto">
          {!collapsed && loading && projects.length === 0 && (
            <p className="px-2 py-4 text-center text-xs text-muted-foreground">Loading projects…</p>
          )}
          {!collapsed && !loading && projects.length === 0 && (
            <p className="px-2 py-4 text-center text-xs text-muted-foreground">
              Create a project to keep chats and files together.
            </p>
          )}
          {!collapsed && !loading && projects.length > 0 && groups.length === 0 && (
            <p className="px-2 py-4 text-center text-xs text-muted-foreground">
              No projects match that filter.
            </p>
          )}
          {groups.map((group) => (
            <section key={group.key} className="flex flex-col gap-0.5">
              {showAreaHeaders && !collapsed && (
                <p className="px-2 pt-2 pb-0.5 text-[11px] font-medium tracking-wide text-muted-foreground">
                  {group.label}
                </p>
              )}
              {group.projects.map((project) => {
                const expanded = !collapsed && selectedProjectId === project.id;
                return (
                  <div key={project.id} className="flex flex-col">
                    <ProjectRow
                      project={project}
                      selected={selectedProjectId === project.id}
                      collapsed={collapsed}
                      onSelect={() => {
                        if (collapsed) onCollapsedChange?.(false);
                        onSelectProject(project.id);
                      }}
                      onEdit={() => onEditProject(project)}
                      onShare={() => onShareProject(project)}
                      onDelete={() => onDeleteProject(project)}
                      onOpenFiles={() => onOpenFiles(project)}
                      onOpenPrompts={() => onOpenPrompts(project)}
                    />
                    {expanded && (
                      <div className="mb-1 ml-3 flex flex-col border-l border-sidebar-border pl-2">
                        {sessionsLoading && (
                          <div className="flex flex-col gap-1 px-1 py-1">
                            <Skeleton className="h-7 w-full" />
                            <Skeleton className="h-7 w-5/6" />
                          </div>
                        )}
                        {!sessionsLoading && sessions.length === 0 && (
                          <p className="px-2 py-1.5 text-[11px] text-muted-foreground">
                            No chats yet
                          </p>
                        )}
                        {!sessionsLoading &&
                          sessions.map((session) => (
                            <div
                              key={session.id}
                              className={cn(
                                "group/chat flex min-h-8 items-center rounded-md hover:bg-sidebar-accent",
                                sessionId === session.id && "bg-sidebar-accent font-medium",
                              )}
                            >
                              <button
                                type="button"
                                className="flex min-w-0 flex-1 items-center gap-2 px-2 py-1 text-left text-[13px]"
                                onClick={() => onSelectSession(session.id)}
                              >
                                <MessageSquare className="size-3.5 shrink-0 text-muted-foreground" />
                                <span className="min-w-0 flex-1 truncate">
                                  {session.title ?? "Untitled chat"}
                                </span>
                              </button>
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon-sm"
                                className="mr-0.5 opacity-0 group-hover/chat:opacity-100 group-focus-within/chat:opacity-100 max-md:opacity-100"
                                title="Rename chat"
                                aria-label={`Rename ${session.title ?? "chat"}`}
                                onClick={() => onRenameSession(session)}
                              >
                                <Pencil />
                              </Button>
                            </div>
                          ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </section>
          ))}
        </div>

        {collapsed && (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="mt-auto mb-1"
            title="New project"
            aria-label="New project"
            onClick={() => {
              onCollapsedChange?.(false);
              onNewProject();
            }}
          >
            <Plus />
          </Button>
        )}
      </div>
    </nav>
  );
}

function SidebarIconLink({
  to,
  label,
  collapsed,
  children,
}: {
  to: string;
  label: string;
  collapsed: boolean;
  children: ReactNode;
}) {
  const link = (
    <RouterLink
      to={to}
      aria-label={label}
      title={label}
      className={cn(
        "inline-flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm text-sidebar-foreground no-underline hover:bg-sidebar-accent",
        collapsed && "size-9 justify-center px-0",
      )}
    >
      {children}
    </RouterLink>
  );
  if (!collapsed) return link;
  return (
    <Tooltip>
      <TooltipTrigger asChild>{link}</TooltipTrigger>
      <TooltipContent side="right">{label}</TooltipContent>
    </Tooltip>
  );
}

function SidebarRow({
  collapsed,
  label,
  icon,
  onClick,
  to,
  prominent,
}: {
  collapsed: boolean;
  label: string;
  icon: ReactNode;
  onClick?: () => void;
  to?: string;
  prominent?: boolean;
}) {
  const className = cn(
    "flex w-full items-center gap-2 rounded-lg px-2 py-2 text-sm no-underline hover:bg-sidebar-accent",
    prominent ? "font-medium" : "text-sidebar-foreground",
    collapsed && "size-9 justify-center px-0",
  );
  const content = (
    <>
      {icon}
      {!collapsed && <span className="truncate">{label}</span>}
    </>
  );
  const node = to ? (
    <RouterLink to={to} className={className} title={label} aria-label={label}>
      {content}
    </RouterLink>
  ) : (
    <button type="button" className={className} onClick={onClick} title={label} aria-label={label}>
      {content}
    </button>
  );
  if (!collapsed) return node;
  return (
    <Tooltip>
      <TooltipTrigger asChild>{node}</TooltipTrigger>
      <TooltipContent side="right">{label}</TooltipContent>
    </Tooltip>
  );
}

function ProjectRow({
  project,
  selected,
  collapsed,
  onSelect,
  onEdit,
  onShare,
  onDelete,
  onOpenFiles,
  onOpenPrompts,
}: {
  project: Project;
  selected: boolean;
  collapsed: boolean;
  onSelect: () => void;
  onEdit: () => void;
  onShare: () => void;
  onDelete: () => void;
  onOpenFiles: () => void;
  onOpenPrompts: () => void;
}) {
  const canManage = canManageProject(project);
  const badge = shareBadgeLabel(project);
  const icon = (
    <span
      className={cn(
        "flex size-6 shrink-0 items-center justify-center rounded-md text-[11px] font-semibold",
        projectIconTone(project),
      )}
    >
      {project.name.charAt(0).toUpperCase()}
    </span>
  );

  if (collapsed) {
    return (
      <Tooltip>
        <TooltipTrigger asChild>
          <button
            type="button"
            className={cn(
              "mx-auto flex size-9 items-center justify-center rounded-lg hover:bg-sidebar-accent",
              selected && "bg-sidebar-accent ring-1 ring-sidebar-border",
            )}
            onClick={onSelect}
            aria-label={project.name}
          >
            {icon}
          </button>
        </TooltipTrigger>
        <TooltipContent side="right">{project.name}</TooltipContent>
      </Tooltip>
    );
  }

  return (
    <div
      className={cn(
        "group/project flex items-center rounded-lg hover:bg-sidebar-accent",
        selected && "bg-sidebar-accent",
      )}
    >
      <button
        type="button"
        className="flex min-w-0 flex-1 items-center gap-2 px-2 py-1.5 text-left"
        onClick={onSelect}
        aria-current={selected ? "true" : undefined}
        aria-expanded={selected}
      >
        {icon}
        <span className="min-w-0 flex-1 truncate text-sm">{project.name}</span>
        {badge && (
          <span className="hidden max-w-[4.5rem] truncate text-[10px] text-muted-foreground sm:inline">
            {badge}
          </span>
        )}
      </button>
      <div className="flex shrink-0 items-center pr-0.5 opacity-0 group-hover/project:opacity-100 group-focus-within/project:opacity-100 max-md:opacity-100">
        {canManage && (
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            title="Edit project"
            aria-label={`Edit ${project.name}`}
            onClick={(e) => {
              e.stopPropagation();
              onEdit();
            }}
          >
            <Pencil />
          </Button>
        )}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title="Project menu"
              aria-label={`${project.name} menu`}
              onClick={(e) => e.stopPropagation()}
            >
              <MoreHorizontal />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={onOpenFiles}>
              <FileText />
              Project files
            </DropdownMenuItem>
            <DropdownMenuItem onClick={onOpenPrompts}>
              <Lightbulb />
              Prompts
            </DropdownMenuItem>
            {canManage && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={onShare}>
                  <Share2 />
                  Share
                </DropdownMenuItem>
                <DropdownMenuItem onClick={onEdit}>
                  <Pencil />
                  Edit
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onClick={onDelete}>
                  <Trash2 />
                  Delete
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  );
}
