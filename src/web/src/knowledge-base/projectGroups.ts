import { projectRole, type Project } from "./api/knowledge";

const PROJECT_ICON_TONES = [
  "bg-orange-100 text-orange-800",
  "bg-emerald-100 text-emerald-800",
  "bg-sky-100 text-sky-800",
  "bg-violet-100 text-violet-800",
  "bg-rose-100 text-rose-800",
  "bg-amber-100 text-amber-800",
  "bg-teal-100 text-teal-800",
  "bg-indigo-100 text-indigo-800",
] as const;

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
  if (projectRole(project) !== "owner") return "Shared with me";
  if (project.isShared) return "Shared";
  return null;
}

export function projectIconTone(project: Project): string {
  let hash = 0;
  for (let i = 0; i < project.id.length; i += 1) {
    hash = (hash + project.id.charCodeAt(i) * (i + 1)) % PROJECT_ICON_TONES.length;
  }
  return PROJECT_ICON_TONES[hash];
}

export function groupProjectsByArea(
  projects: Project[],
): { key: string; label: string; projects: Project[] }[] {
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
      projects: items
        .slice()
        .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" })),
    }));

  if (none.length > 0) {
    named.push({
      key: "__none__",
      label: "No area",
      projects: none
        .slice()
        .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" })),
    });
  }

  return named;
}
