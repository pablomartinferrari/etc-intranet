import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { HubConnection } from "@microsoft/signalr";
import { FileText } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ChatMarkdown } from "@/knowledge-base/ChatMarkdown";
import { cn } from "@/lib/utils";
import { workspaceApi, type ChatMessage, type PresenceUser } from "../api";
import { Composer } from "../Composer";
import { connectWorkspaceHub, sendTyping } from "../realtime";
import { ShareChatDialog } from "../ShareChatDialog";

function avatarColor(name: string) {
  const tones = ["bg-sky-600", "bg-violet-600", "bg-amber-600", "bg-emerald-600", "bg-rose-600"];
  let hash = 0;
  for (const ch of name) hash = (hash + ch.charCodeAt(0)) % tones.length;
  return tones[hash];
}

function MessageBubble({ message, selfOid }: { message: ChatMessage; selfOid?: string }) {
  const isSelf = message.role === "user" && !!selfOid && message.authorOid === selfOid;
  const isAssistant = message.role === "assistant";
  if (isAssistant) {
    return (
      <div className="mx-auto flex w-full max-w-[760px] gap-3 px-6">
        <span className="mt-1 flex size-7 shrink-0 items-center justify-center rounded-full border text-xs">✶</span>
        <div className="min-w-0 flex-1">
          {message.authorName !== "Assistant" ? (
            <p className="mb-1 text-xs text-muted-foreground">{message.authorName}</p>
          ) : null}
          <ChatMarkdown content={message.content} className="text-sm" />
          {message.citations?.length ? (
            <div className="mt-2 flex flex-wrap gap-2">
              {message.citations.map((citation, index) => (
                <Link
                  key={`${citation.documentId ?? citation.url ?? index}`}
                  to={citation.url || "/workspace/search"}
                  className="inline-flex items-center gap-1 rounded-full border px-2 py-1 text-[11px] text-muted-foreground hover:bg-muted"
                >
                  <FileText className="size-3" />
                  {citation.path ?? citation.title}
                </Link>
              ))}
            </div>
          ) : null}
        </div>
      </div>
    );
  }

  return (
    <div className={cn("mx-auto flex w-full max-w-[760px] px-6", isSelf ? "justify-end" : "justify-start")}>
      <div className="max-w-[min(100%,560px)] rounded-3xl bg-muted px-4 py-3 text-sm">
        <p className="mb-1 text-[11px] text-muted-foreground">{message.authorName}</p>
        <ChatMarkdown content={message.content} />
      </div>
    </div>
  );
}

export function WorkspaceChatPage() {
  const { chatId } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [shareOpen, setShareOpen] = useState(false);
  const [presence, setPresence] = useState<PresenceUser[]>([]);
  const [typingName, setTypingName] = useState<string | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);
  const scroller = useRef<HTMLDivElement | null>(null);

  const sidebarQuery = useQuery({ queryKey: ["ws-sidebar"], queryFn: workspaceApi.sidebar });
  const capabilitiesQuery = useQuery({ queryKey: ["ws-capabilities"], queryFn: workspaceApi.capabilities });
  const chatQuery = useQuery({
    queryKey: ["ws-chat", chatId],
    queryFn: () => workspaceApi.chat(chatId!),
    enabled: !!chatId,
  });
  const sharesQuery = useQuery({
    queryKey: ["ws-shares", chatId],
    queryFn: () => workspaceApi.shares(chatId!),
    enabled: !!chatId,
  });
  const messagesQuery = useQuery({
    queryKey: ["ws-messages", chatId],
    queryFn: () => workspaceApi.messages(chatId!),
    enabled: !!chatId,
  });

  const sendMutation = useMutation({
    mutationFn: (text: string) => workspaceApi.send(chatId!, text),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["ws-messages", chatId] });
      await queryClient.invalidateQueries({ queryKey: ["ws-chat", chatId] });
      await queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
    },
  });

  useEffect(() => {
    if (!chatId) return;
    let cancelled = false;
    void connectWorkspaceHub(chatId, {
      onPresence: (users) => {
        if (!cancelled) setPresence(users);
      },
      onTyping: (event) => {
        if (!cancelled) setTypingName(event.typing ? event.displayName : null);
      },
      onMessage: (message) => {
        queryClient.setQueryData<ChatMessage[]>(["ws-messages", chatId], (current) => {
          const next = current ?? [];
          if (next.some((m) => m.id === message.id)) return next;
          return [...next, message];
        });
      },
    })
      .then((connection) => {
        if (cancelled) {
          void connection.stop();
          return;
        }
        connectionRef.current = connection;
      })
      .catch(() => {
        /* SignalR is optional when the API is down */
      });
    return () => {
      cancelled = true;
      void connectionRef.current?.stop();
      connectionRef.current = null;
    };
  }, [chatId, queryClient]);

  useEffect(() => {
    scroller.current?.scrollTo({ top: scroller.current.scrollHeight });
  }, [messagesQuery.data, sendMutation.isPending]);

  const chat = chatQuery.data;
  const selfOid = sidebarQuery.data?.me.oid;
  const shareLine = useMemo(() => {
    const names = (sharesQuery.data ?? []).map((s) => s.principalDisplayName);
    if (!names.length) return null;
    return `With ${names.join(", ")}`;
  }, [sharesQuery.data]);
  const others = useMemo(
    () => presence.filter((p) => p.oid !== "dev-you" && p.oid !== "agent"),
    [presence],
  );

  if (!chatId) return null;

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center justify-between gap-3 px-6 py-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="truncate text-sm font-semibold">{chat?.title ?? "Chat"}</h1>
            {chat?.visibility === "shared" ? (
              <Badge variant="outline">{chat.realtimeCollaboration ? "Shared" : "Private"}</Badge>
            ) : (
              <Badge variant="outline">Private</Badge>
            )}
            {chat?.projectId ? (
              <Link to={`/workspace/projects/${chat.projectId}`} className="text-xs text-muted-foreground hover:underline">
                {chat.projectName}
              </Link>
            ) : null}
            {shareLine ? <span className="text-xs text-muted-foreground">{shareLine}</span> : null}
          </div>
          {others.length > 0 ? (
            <p className="mt-1 text-[11px] text-muted-foreground">
              {others.filter((p) => p.typing).length
                ? `${others.find((p) => p.typing)?.displayName} is typing`
                : `${others.length} online`}
            </p>
          ) : null}
        </div>
        <div className="flex items-center gap-2">
          {presence.length > 0 ? (
            <div className="flex items-center -space-x-1.5">
              {presence.slice(0, 4).map((user) => (
                <span
                  key={user.oid}
                  title={user.displayName}
                  className={cn(
                    "flex size-6 items-center justify-center rounded-full text-[10px] font-medium text-white",
                    avatarColor(user.displayName),
                  )}
                >
                  {user.initial}
                </span>
              ))}
            </div>
          ) : null}
          <Button variant="outline" size="sm" className="rounded-full" onClick={() => navigate("/workspace")}>
            Move
          </Button>
          <Button size="sm" className="rounded-full" onClick={() => setShareOpen(true)}>
            Share
          </Button>
        </div>
      </header>
      <div ref={scroller} className="min-h-0 flex-1 space-y-6 overflow-y-auto py-6">
        {(messagesQuery.data ?? []).length === 0 && !sendMutation.isPending ? (
          <div className="mx-auto max-w-[760px] px-6 text-sm text-muted-foreground">
            This chat is empty. Ask a question — the assistant uses project instructions, linked files, and repository
            search as context.
          </div>
        ) : null}
        {(messagesQuery.data ?? []).map((message) => (
          <MessageBubble key={message.id} message={message} selfOid={selfOid} />
        ))}
        {sendMutation.isPending ? (
          <div className="mx-auto max-w-[760px] px-6 text-sm text-muted-foreground">Assistant is answering the group…</div>
        ) : null}
        {typingName ? (
          <div className="mx-auto max-w-[760px] px-6 text-sm text-muted-foreground">{typingName} is typing…</div>
        ) : null}
      </div>
      <Composer
        disabled={chat?.role === "viewer" || sendMutation.isPending}
        disclaimer={
          capabilitiesQuery.data && !capabilitiesQuery.data.hostedLlmConfigured
            ? "No language model is configured — answers come from repository search and say so."
            : "Uses the same language model as Chat at /knowledge. Citations come from the workspace repository."
        }
        onSend={(text) => {
          void sendTyping(connectionRef.current, chatId, false);
          sendMutation.mutate(text);
        }}
      />
      <ShareChatDialog open={shareOpen} chat={chat} onClose={() => setShareOpen(false)} />
    </div>
  );
}
