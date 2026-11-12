import type { PipelineItem } from "./api/cleat";

export type PipelinePhaseFilter =
  | "all"
  | "needs"
  | "triage"
  | "preparing"
  | "submitted"
  | "won"
  | "lost"
  | "archived";

const PHASE_BUCKETS = new Set(["triage", "preparing", "submitted", "won", "lost"]);

/**
 * Mirrors `PipelineCloseoutRules.Normalize`: trim, lower, spaces → underscores.
 * Empty / whitespace-only values become null so callers can fall back to columnTitle.
 */
export function normalizePipelineStage(value: string | null | undefined): string | null {
  if (value == null) {
    return null;
  }

  const normalized = value.trim().toLowerCase().replaceAll(" ", "_");
  return normalized.length === 0 ? null : normalized;
}

/** Same stage resolution as `PipelineService.Count` / `PipelineCloseoutRules.Evaluate`. */
export function pipelineStage(item: PipelineItem): string | null {
  return (
    normalizePipelineStage(item.pursuit.phase) ??
    normalizePipelineStage(item.pursuit.columnTitle)
  );
}

/**
 * Count-card bucket for a row. Archived is exclusive (matches the API switch:
 * archived items are not also counted as won/lost/triage/…).
 */
export function pipelineBucket(item: PipelineItem): string {
  if (item.pursuit.archived) {
    return "archived";
  }

  const stage = pipelineStage(item);
  return stage && PHASE_BUCKETS.has(stage) ? stage : "other";
}

export function matchesPipelineFilter(
  item: PipelineItem,
  filter: string,
): boolean {
  if (filter === "all") {
    return true;
  }
  if (filter === "needs") {
    return item.needsCloseOut;
  }
  return pipelineBucket(item) === filter;
}

export function filterPipelineItems(
  items: readonly PipelineItem[],
  filter: string,
): PipelineItem[] {
  if (filter === "all") {
    return [...items];
  }
  return items.filter((item) => matchesPipelineFilter(item, filter));
}

export function pipelineFilterHeading(filter: string, count: number): string {
  switch (filter) {
    case "needs":
      return `Needs close-out (${count})`;
    case "triage":
      return `Triage (${count})`;
    case "preparing":
      return `Preparing (${count})`;
    case "submitted":
      return `Submitted (${count})`;
    case "won":
      return `Won (${count})`;
    case "lost":
      return `Lost (${count})`;
    case "archived":
      return `Archived (${count})`;
    default:
      return "All pursuits";
  }
}
