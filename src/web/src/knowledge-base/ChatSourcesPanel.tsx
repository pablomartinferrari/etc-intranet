import { BookOpen, ExternalLink, X } from "lucide-react";
import { useCallback, useEffect, useState, type MouseEvent, type RefObject } from "react";

import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { cn } from "@/lib/utils";
import type { Citation } from "./api/knowledge";
import { FileTypeIcon, fileKindFromName } from "./fileTypeIcon";

export const CHAT_SOURCES_PANEL_ID = "chat-sources-panel";

const MD_UP = "(min-width: 768px)";

function useMdUp(): boolean {
  const [mdUp, setMdUp] = useState(() => window.matchMedia(MD_UP).matches);

  useEffect(() => {
    const media = window.matchMedia(MD_UP);
    const onChange = () => setMdUp(media.matches);
    onChange();
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, []);

  return mdUp;
}

export function citationHref(citation: Citation): string | null {
  const raw = citation.url?.trim() || citation.sourceUri?.trim() || "";
  if (!raw) return null;
  try {
    const parsed = new URL(raw);
    if (parsed.protocol === "http:" || parsed.protocol === "https:") return parsed.href;
  } catch {
    return null;
  }
  return null;
}

export function ChatSourcesTrigger({
  count,
  expanded,
  controlsId,
  onClick,
}: {
  count: number;
  expanded: boolean;
  controlsId: string;
  onClick: (event: MouseEvent<HTMLButtonElement>) => void;
}) {
  const label = count === 1 ? "1 source" : `${count} sources`;

  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      className="mt-2 h-7 rounded-full px-2.5 text-xs"
      aria-expanded={expanded}
      aria-controls={controlsId}
      onClick={onClick}
    >
      <BookOpen className="size-3.5" />
      {label}
    </Button>
  );
}

export function CitationCard({ citation }: { citation: Citation }) {
  const isWeb = citation.type === "web" || !!citation.url;
  const iconKind = isWeb ? "web" : fileKindFromName(citation.title);
  const href = citationHref(citation);

  return (
    <article className="rounded-lg border bg-card px-3 py-2.5">
      <div className="flex min-w-0 items-start gap-2">
        <FileTypeIcon kind={iconKind} className="mt-0.5 size-5 shrink-0 text-muted-foreground" />
        {href ? (
          <a
            href={href}
            target="_blank"
            rel="noopener noreferrer"
            className="min-w-0 flex-1 break-words text-xs font-semibold text-primary hover:underline"
          >
            {citation.title}
            <ExternalLink className="ml-1 inline size-3 align-text-top" aria-hidden />
            <span className="sr-only"> (opens in a new tab)</span>
          </a>
        ) : (
          <p className="min-w-0 flex-1 break-words text-xs font-semibold">{citation.title}</p>
        )}
      </div>
      {citation.snippet ? (
        <p className="mt-1.5 ml-7 break-words text-xs leading-relaxed text-muted-foreground">
          {citation.snippet}
        </p>
      ) : null}
    </article>
  );
}

function SourcesList({ citations }: { citations: Citation[] }) {
  if (citations.length === 0) {
    return <p className="text-sm text-muted-foreground">No sources for this answer.</p>;
  }

  return (
    <ul className="flex flex-col gap-2">
      {citations.map((citation, index) => (
        <li key={citation.documentId ?? citation.url ?? `${citation.title}-${index}`}>
          <CitationCard citation={citation} />
        </li>
      ))}
    </ul>
  );
}

export function ChatSourcesPanel({
  open,
  onOpenChange,
  citations,
  returnFocusRef,
  panelId = CHAT_SOURCES_PANEL_ID,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  citations: Citation[];
  returnFocusRef?: RefObject<HTMLButtonElement | null>;
  panelId?: string;
}) {
  const isDesktop = useMdUp();

  const close = useCallback(() => {
    onOpenChange(false);
    queueMicrotask(() => returnFocusRef?.current?.focus());
  }, [onOpenChange, returnFocusRef]);

  useEffect(() => {
    if (!open || !isDesktop) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape" || event.defaultPrevented) return;
      event.preventDefault();
      close();
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, isDesktop, close]);

  const countLabel =
    citations.length === 1 ? "1 source from this answer" : `${citations.length} sources from this answer`;

  return (
    <>
      {open && isDesktop ? (
        <aside
          id={panelId}
          role="complementary"
          aria-labelledby={`${panelId}-title`}
          className="flex h-full min-h-0 w-[360px] shrink-0 flex-col border-l border-sidebar-border bg-sidebar text-sidebar-foreground"
        >
          <div className="flex items-start justify-between gap-2 border-b px-4 py-3">
            <div className="min-w-0">
              <h2 id={`${panelId}-title`} className="text-sm font-semibold">
                Sources
              </h2>
              <p className="text-xs text-muted-foreground">{countLabel}</p>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label="Close sources"
              onClick={close}
            >
              <X />
            </Button>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
            <SourcesList citations={citations} />
          </div>
        </aside>
      ) : null}

      <Sheet
        open={open && !isDesktop}
        onOpenChange={(next) => {
          onOpenChange(next);
          if (!next) {
            queueMicrotask(() => returnFocusRef?.current?.focus());
          }
        }}
      >
        <SheetContent
          id={isDesktop ? undefined : panelId}
          side="right"
          className={cn("flex w-full flex-col gap-0 p-0 sm:max-w-[360px]")}
          showCloseButton
        >
          <SheetHeader className="border-b">
            <SheetTitle>Sources</SheetTitle>
            <SheetDescription>{countLabel}</SheetDescription>
          </SheetHeader>
          <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
            <SourcesList citations={citations} />
          </div>
        </SheetContent>
      </Sheet>
    </>
  );
}
