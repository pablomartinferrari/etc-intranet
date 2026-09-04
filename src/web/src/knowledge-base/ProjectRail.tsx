import { ArrowLeft, ChevronDown, ChevronRight, Plus, Search } from "lucide-react";
import { useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { AgentSourcesChatLink } from "./AgentSourcesPage";
import type { Project } from "./api/knowledge";

export function projectSubtitle(project: Project): string {
  const description = project.description?.trim();
  if (description) return description;
  const instructions = project.instructions?.trim();
  if (instructions) {
    return instructions.length > 72 ? `${instructions.slice(0, 72)}…` : instructions;
  }
  return "No description yet";
}

export function shareBadgeLabel(project: Project): string | null {
  if ((project.role ?? "owner") !== "owner") return "Shared with me";
  if (project.isShared) return "Shared";
  return null;
}

export function groupProjectsByArea(projects: Project[]): { key: string; label: string; projects: Project[] }[] {
  const groups = new Map<string, Project[]>();
  const none: Project[] = [];
  for (const project of projects) {
    const area = project.area?.trim();
    if (!area) {
      none.push(project);
      continue;
    }
    const list = groups.get(area) ?? [];
    list.push(project);
    groups.set(area, list);
  }

  const named = [...groups.entries()]
    .sort(([a], [b]) => a.localeCompare(b, undefined, { sensitivity: "base" }))
    .map(([area, items]) => ({
      key: area,
      label: area,
      projects: items.slice().sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" })),
    }));

  if (none.length > 0) {
    named.push({
      key: "__none__",
      label: "No area",
      projects: none.slice().sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" })),
    });
  }

  return named;
}

export function ProjectRail({
  projects,
  selectedProjectId,
  onSelectProject,
  onNewProject,
  showHomeLink,
  className,
  loading,
}: {
  projects: Project[];
  selectedProjectId?: string;
  onSelectProject: (id: string) => void;
  onNewProject: () => void;
  showHomeLink: boolean;
  className?: string;
  loading?: boolean;
}) {
  const [filter, setFilter] = useState("");
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  const groups = useMemo(() => {
    const q = filter.trim().toLowerCase();
    const filtered = q
      ? projects.filter(
          (p) =>
            p.name.toLowerCase().includes(q) ||
            (p.area ?? "").toLowerCase().includes(q),
        )
      : projects;
    return groupProjectsByArea(filtered);
  }, [filter, projects]);

  return (
    <nav
      className={cn(
        "flex w-full shrink-0 flex-col items-stretch gap-3 bg-muted px-2.5 py-3 md:w-[240px] md:border-r",
        className,
      )}
    >
      {showHomeLink && (
        <RouterLink
          className="mx-auto flex size-11 items-center justify-center rounded-md text-muted-foreground no-underline hover:bg-card"
          to="/"
          title="Back to home"
        >
          <ArrowLeft />
        </RouterLink>
      )}
      <AgentSourcesChatLink />
      <div className="relative">
        <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
        <Input
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          placeholder="Filter projects"
          className="h-9 bg-background pl-8"
          aria-label="Filter projects"
        />
      </div>
      <div className="flex w-full flex-1 flex-col gap-3 overflow-y-auto">
        {loading && projects.length === 0 && (
          <p className="px-1 py-4 text-center text-xs text-muted-foreground">Loading projects…</p>
        )}
        {!loading && projects.length === 0 && (
          <p className="px-1 py-4 text-center text-xs text-muted-foreground">
            Create a project to keep chats and files together.
          </p>
        )}
        {!loading && projects.length > 0 && groups.length === 0 && (
          <p className="px-1 py-4 text-center text-xs text-muted-foreground">No projects match that filter.</p>
        )}
        {groups.map((group) => {
          const isCollapsed = collapsed[group.key] === true;
          return (
            <section key={group.key} className="flex flex-col gap-1.5">
              <button
                type="button"
                className="flex w-full items-center gap-1 px-1 text-left text-[11px] font-semibold tracking-wide text-muted-foreground uppercase"
                onClick={() =>
                  setCollapsed((prev) => ({ ...prev, [group.key]: !isCollapsed }))
                }
              >
                {isCollapsed ? (
                  <ChevronRight className="size-3.5" />
                ) : (
                  <ChevronDown className="size-3.5" />
                )}
                <span className="truncate">{group.label}</span>
                <span className="ml-auto tabular-nums">{group.projects.length}</span>
              </button>
              {!isCollapsed &&
                group.projects.map((p) => {
                  const badge = shareBadgeLabel(p);
                  return (
                    <button
                      key={p.id}
                      type="button"
                      className={`flex min-h-16 w-full cursor-pointer items-start gap-2.5 rounded-lg border bg-card px-3 py-2.5 text-left hover:bg-muted ${
                        selectedProjectId === p.id ? "border-primary bg-primary/10" : ""
                      }`}
                      onClick={() => onSelectProject(p.id)}
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
                        {badge && (
                          <Badge variant="outline" className="w-fit text-[10px]">
                            {badge}
                          </Badge>
                        )}
                      </div>
                    </button>
                  );
                })}
            </section>
          );
        })}
      </div>
      <button
        type="button"
        className="flex h-11 w-full cursor-pointer items-center justify-center gap-1.5 rounded-lg border border-dashed bg-transparent text-sm text-muted-foreground hover:bg-card"
        onClick={onNewProject}
        title="New project"
      >
        <Plus className="size-4" />
        New project
      </button>
    </nav>
  );
}
