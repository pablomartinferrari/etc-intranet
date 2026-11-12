const PREFIX = "etc-kb-chat-selection:";

export type ChatSelection = {
  selectedProjectId?: string;
  /** Session id for the project, or `null` when New chat (draft) is active. */
  sessionsByProject: Record<string, string | null>;
};

function storageKey(userKey: string): string {
  return `${PREFIX}${userKey}`;
}

export function readChatSelection(userKey: string): ChatSelection {
  if (typeof window === "undefined") {
    return { sessionsByProject: {} };
  }
  try {
    const raw = window.localStorage.getItem(storageKey(userKey));
    if (!raw) return { sessionsByProject: {} };
    const parsed = JSON.parse(raw) as ChatSelection;
    return {
      selectedProjectId: parsed.selectedProjectId,
      sessionsByProject: parsed.sessionsByProject ?? {},
    };
  } catch {
    return { sessionsByProject: {} };
  }
}

export function writeChatSelection(userKey: string, selection: ChatSelection): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(storageKey(userKey), JSON.stringify(selection));
  } catch {
    // ignore quota / private mode
  }
}

/** Persist the current project + session. `null`/`undefined` session means a New chat draft. */
export function persistChatSelection(
  userKey: string,
  selectedProjectId: string | undefined,
  sessionId: string | undefined | null,
): void {
  if (!selectedProjectId) return;
  const current = readChatSelection(userKey);
  writeChatSelection(userKey, {
    selectedProjectId,
    sessionsByProject: {
      ...current.sessionsByProject,
      [selectedProjectId]: sessionId ?? null,
    },
  });
}

export function lastSessionForProject(selection: ChatSelection, projectId: string): string | undefined {
  const value = selection.sessionsByProject[projectId];
  if (value == null || value === "") return undefined;
  return value;
}
