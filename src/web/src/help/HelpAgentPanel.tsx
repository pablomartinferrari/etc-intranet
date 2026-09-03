import { useEffect, useRef, useState } from "react";
import { useMsal } from "@azure/msal-react";
import {
  CircleHelpIcon,
  FolderPlusIcon,
  Loader2Icon,
  SendIcon,
  Trash2Icon,
} from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";
import { askHelp, type HelpAskResponse } from "./api";
import {
  SUGGESTED_HELP_QUESTIONS,
  helpAnswerSourceKind,
  helpAnswerSourceLabel,
  helpMapOnlyNote,
} from "./intranetMap";
import { useAddSharePointFolder } from "../knowledge-base/AddSharePointFolderSheet";

type UserMessage = { id: string; role: "user"; text: string };
type AssistantMessage = { id: string; role: "assistant"; result: HelpAskResponse };
type HelpMessage = UserMessage | AssistantMessage;

let helpMessageSeq = 0;
function nextHelpMessageId(role: string): string {
  helpMessageSeq += 1;
  return `help-${role}-${helpMessageSeq}`;
}

export function HelpAgentHost() {
  const { accounts } = useMsal();
  const location = useLocation();
  const [open, setOpen] = useState(false);

  if (accounts.length === 0) {
    return null;
  }

  const onChat = location.pathname.startsWith("/knowledge");

  return (
    <>
      <Button
        type="button"
        className={cn(
          "fixed z-40 shadow-lg",
          "size-11 px-0 sm:h-9 sm:w-auto sm:px-2.5",
          onChat
            ? "right-3 bottom-[max(6rem,calc(env(safe-area-inset-bottom)+5.25rem))] md:right-5 md:bottom-5"
            : "right-4 bottom-[max(1.25rem,env(safe-area-inset-bottom))] md:right-5 md:bottom-5",
        )}
        size="lg"
        onClick={() => setOpen(true)}
        aria-label="Open intranet help"
      >
        <CircleHelpIcon />
        <span className="hidden sm:inline">Help</span>
      </Button>
      <HelpAgentPanel open={open} onOpenChange={setOpen} />
    </>
  );
}

export function HelpAgentPanel({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const navigate = useNavigate();
  const location = useLocation();
  const { openAddFolder } = useAddSharePointFolder();
  const [question, setQuestion] = useState("");
  const [asking, setAsking] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [messages, setMessages] = useState<HelpMessage[]>([]);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const threadEndRef = useRef<HTMLDivElement>(null);

  const hasThread = messages.length > 0;
  const mapOnlyNote = messages
    .filter((message): message is AssistantMessage => message.role === "assistant")
    .map((message) => helpMapOnlyNote(message.result))
    .find((note) => note);

  useEffect(() => {
    if (open) {
      const timer = window.setTimeout(() => inputRef.current?.focus(), 80);
      return () => window.clearTimeout(timer);
    }
    return undefined;
  }, [open]);

  useEffect(() => {
    if (!open) {
      return;
    }
    threadEndRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [open, messages, asking]);

  async function submit(raw: string) {
    const text = raw.trim();
    if (!text || asking) {
      return;
    }

    const userMessage: UserMessage = { id: nextHelpMessageId("user"), role: "user", text };
    setMessages((current) => [...current, userMessage]);
    setQuestion("");
    setAsking(true);
    setError(null);
    try {
      const result = await askHelp(text);
      setMessages((current) => [
        ...current,
        { id: nextHelpMessageId("assistant"), role: "assistant", result },
      ]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not answer that.");
    } finally {
      setAsking(false);
    }
  }

  function goTo(path: string) {
    if (location.pathname !== path) {
      navigate(path);
    }
    onOpenChange(false);
  }

  function clearConversation() {
    setMessages([]);
    setError(null);
    setQuestion("");
    inputRef.current?.focus();
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="h-full w-full gap-0 sm:max-w-md" side="right">
        <SheetHeader className="border-b">
          <SheetTitle>Intranet help</SheetTitle>
          <SheetDescription>
            Ask where to go. Newest replies stay at the bottom — scroll up for
            earlier turns. Answers use the intranet map, with AI when a model is
            available.
          </SheetDescription>
          <div className="mt-3 flex flex-wrap gap-2">
            <Button
              type="button"
              size="sm"
              className="justify-start"
              onClick={() => {
                onOpenChange(false);
                openAddFolder();
              }}
            >
              <FolderPlusIcon />
              Add SharePoint folder
            </Button>
            {hasThread && (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={clearConversation}
              >
                <Trash2Icon />
                Clear conversation
              </Button>
            )}
          </div>
        </SheetHeader>
        <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-4 py-4">
          {mapOnlyNote && (
            <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-5 text-amber-950 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100">
              {mapOnlyNote}
            </p>
          )}

          {!hasThread && (
            <div className="flex flex-col gap-2">
              <p className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
                Try asking
              </p>
              <div className="flex flex-col gap-2">
                {SUGGESTED_HELP_QUESTIONS.map((item) => (
                  <Button
                    key={item}
                    type="button"
                    variant="outline"
                    className="h-auto justify-start whitespace-normal px-3 py-2 text-left"
                    disabled={asking}
                    onClick={() => void submit(item)}
                  >
                    {item}
                  </Button>
                ))}
              </div>
            </div>
          )}

          {messages.map((message) =>
            message.role === "user" ? (
              <div key={message.id} className="flex justify-end">
                <div className="max-w-[85%] rounded-[18px] bg-primary px-3.5 py-2.5 text-sm leading-6 text-primary-foreground">
                  {message.text}
                </div>
              </div>
            ) : (
              <AssistantTurn
                key={message.id}
                result={message.result}
                onOpen={goTo}
              />
            ),
          )}

          {asking && (
            <div className="flex justify-start">
              <div className="flex items-center gap-2 rounded-[18px] border bg-muted px-3.5 py-2.5 text-sm text-muted-foreground">
                <Spinner size="sm" />
                Looking…
              </div>
            </div>
          )}

          {error && <p className="text-sm text-destructive">{error}</p>}
          <div ref={threadEndRef} />
        </div>
        <SheetFooter className="border-t">
          {hasThread && (
            <div className="flex flex-wrap gap-1.5">
              {SUGGESTED_HELP_QUESTIONS.map((item) => (
                <Button
                  key={item}
                  type="button"
                  variant="ghost"
                  size="xs"
                  className="h-auto max-w-full justify-start whitespace-normal px-2 py-1 text-left text-muted-foreground"
                  disabled={asking}
                  onClick={() => void submit(item)}
                >
                  {item}
                </Button>
              ))}
            </div>
          )}
          <form
            className="flex items-end gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              void submit(question);
            }}
          >
            <Textarea
              ref={inputRef}
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              rows={2}
              placeholder="Where do I…?"
              disabled={asking}
              className="min-h-[2.75rem] flex-1 resize-none"
              onKeyDown={(event) => {
                if (event.key === "Enter" && !event.shiftKey) {
                  event.preventDefault();
                  void submit(question);
                }
              }}
            />
            <Button
              type="submit"
              size="icon-lg"
              disabled={asking || !question.trim()}
              aria-label={asking ? "Looking" : "Ask"}
            >
              {asking ? <Loader2Icon className="animate-spin" /> : <SendIcon />}
            </Button>
          </form>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

function AssistantTurn({
  result,
  onOpen,
}: {
  result: HelpAskResponse;
  onOpen: (path: string) => void;
}) {
  const kind = helpAnswerSourceKind(result);
  const badgeLabel = kind === "local" ? "Local" : kind === "hosted" ? "Hosted" : "Map";

  return (
    <div className="flex justify-start">
      <div className="flex max-w-[92%] flex-col gap-3 rounded-[18px] border bg-muted px-3.5 py-3 text-sm shadow-sm">
        <p className="leading-6 whitespace-pre-wrap">{result.answer}</p>
        {result.links.length > 0 && (
          <div className="flex flex-col gap-2">
            {result.links.map((link) => (
              <Button
                key={link.path}
                type="button"
                variant={link.path === "/" ? "outline" : "default"}
                className="justify-start"
                onClick={() => onOpen(link.path)}
              >
                Open {link.label}
              </Button>
            ))}
          </div>
        )}
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={kind === "map" ? "outline" : "secondary"}>{badgeLabel}</Badge>
          <p className="text-[11px] leading-4 text-muted-foreground">
            {helpAnswerSourceLabel(result)}
          </p>
        </div>
      </div>
    </div>
  );
}
