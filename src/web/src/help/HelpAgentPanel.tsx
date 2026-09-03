import { useEffect, useRef, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { CircleHelpIcon, FolderPlusIcon, SendIcon } from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";

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
import { askHelp, type HelpAskResponse } from "./api";
import { SUGGESTED_HELP_QUESTIONS } from "./intranetMap";
import { useAddSharePointFolder } from "../knowledge-base/AddSharePointFolderSheet";

type Exchange = {
  question: string;
  result: HelpAskResponse;
};

export function HelpAgentHost() {
  const { accounts } = useMsal();
  const [open, setOpen] = useState(false);

  if (accounts.length === 0) {
    return null;
  }

  return (
    <>
      <Button
        type="button"
        className="fixed right-5 bottom-5 z-40 shadow-lg"
        size="lg"
        onClick={() => setOpen(true)}
        aria-label="Open intranet help"
      >
        <CircleHelpIcon />
        Help
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
  const [exchange, setExchange] = useState<Exchange | null>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (open) {
      const timer = window.setTimeout(() => inputRef.current?.focus(), 80);
      return () => window.clearTimeout(timer);
    }
    return undefined;
  }, [open]);

  async function submit(raw: string) {
    const text = raw.trim();
    if (!text || asking) {
      return;
    }

    setAsking(true);
    setError(null);
    try {
      const result = await askHelp(text);
      setExchange({ question: text, result });
      setQuestion("");
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

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-md" side="right">
        <SheetHeader>
          <SheetTitle>Intranet help</SheetTitle>
          <SheetDescription>
            Ask where to go. Answers stay on the real Home apps — Chat, Lead, and Sales. Use Add
            SharePoint folder to give Chat new documents.
          </SheetDescription>
        </SheetHeader>
        <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-4 pb-2">
          <Button
            type="button"
            className="justify-start"
            onClick={() => {
              onOpenChange(false);
              openAddFolder();
            }}
          >
            <FolderPlusIcon />
            Add SharePoint folder
          </Button>
          {!exchange && (
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

          {exchange && (
            <div className="flex flex-col gap-3">
              <div className="rounded-lg bg-muted px-3 py-2 text-sm">{exchange.question}</div>
              <div className="flex flex-col gap-3 rounded-lg border bg-card px-3 py-3 text-sm shadow-sm">
                <p className="leading-6 whitespace-pre-wrap">{exchange.result.answer}</p>
                {exchange.result.links.length > 0 && (
                  <div className="flex flex-col gap-2">
                    {exchange.result.links.map((link) => (
                      <Button
                        key={link.path}
                        type="button"
                        variant={link.path === "/" ? "outline" : "default"}
                        className="justify-start"
                        onClick={() => goTo(link.path)}
                      >
                        Open {link.label}
                      </Button>
                    ))}
                  </div>
                )}
                <p className="text-[11px] leading-4 text-muted-foreground">
                  {exchange.result.source === "llm"
                    ? "Answered with the intranet map as context."
                    : "Answered from the intranet map."}
                </p>
              </div>
              <Button
                type="button"
                variant="ghost"
                className="self-start"
                onClick={() => {
                  setExchange(null);
                  setError(null);
                }}
              >
                Ask something else
              </Button>
            </div>
          )}

          {error && <p className="text-sm text-destructive">{error}</p>}
        </div>
        <SheetFooter>
          <form
            className="flex flex-col gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              void submit(question);
            }}
          >
            <Textarea
              ref={inputRef}
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              rows={3}
              placeholder="Where do I…?"
              disabled={asking}
              onKeyDown={(event) => {
                if (event.key === "Enter" && !event.shiftKey) {
                  event.preventDefault();
                  void submit(question);
                }
              }}
            />
            <Button type="submit" disabled={asking || !question.trim()}>
              {asking ? (
                <>
                  <Spinner size="sm" />
                  Looking…
                </>
              ) : (
                <>
                  <SendIcon />
                  Ask
                </>
              )}
            </Button>
          </form>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
