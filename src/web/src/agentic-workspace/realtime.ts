import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { getApiAuthHeaders, hasApiAuth } from "@mf/api/client";
import type { ChatMessage, PresenceUser } from "./api";

const hubUrl = "/hubs/workspace";

export type WorkspaceRealtimeHandlers = {
  onPresence?: (users: PresenceUser[]) => void;
  onTyping?: (event: { oid: string; displayName: string; typing: boolean }) => void;
  onMessage?: (message: ChatMessage) => void;
  onAgent?: (event: { status: string }) => void;
};

export async function connectWorkspaceHub(
  chatId: string,
  handlers: WorkspaceRealtimeHandlers,
): Promise<HubConnection> {
  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: async () => {
        if (!hasApiAuth()) return "";
        try {
          const headers = await getApiAuthHeaders();
          const raw = (headers as Record<string, string>).Authorization ?? "";
          return raw.replace(/^Bearer\s+/i, "");
        } catch {
          return "";
        }
      },
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on("presence", (users: PresenceUser[]) => handlers.onPresence?.(users));
  connection.on("typing", (event: { oid: string; displayName: string; typing: boolean }) =>
    handlers.onTyping?.(event),
  );
  connection.on("message", (message: ChatMessage) => handlers.onMessage?.(message));
  connection.on("agent", (event: { status: string }) => handlers.onAgent?.(event));

  await connection.start();
  await connection.invoke("JoinChat", chatId);
  return connection;
}

export async function sendTyping(connection: HubConnection | null, chatId: string, typing: boolean) {
  if (!connection) return;
  try {
    await connection.invoke("Typing", chatId, typing);
  } catch {
    /* ignore dropped typing pings */
  }
}
