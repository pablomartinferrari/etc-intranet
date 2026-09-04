const PREFIX = "etc-kb-chat-selection:";

export type ChatSelection = {
  selectedProjectId?: string;
  sessionsByProject: Record<string, string | undefined>;
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

export function lastSessionForProject(selection: ChatSelection, projectId: string): string | undefined {
  return selection.sessionsByProject[projectId];
}
