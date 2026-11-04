import { ArrowUp, Paperclip, Plus } from "lucide-react";
import { useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export function Composer({
  placeholder = "Ask anything...",
  disabled,
  onSend,
  disclaimer,
}: {
  placeholder?: string;
  disabled?: boolean;
  onSend: (text: string) => void;
  disclaimer?: string;
}) {
  const [text, setText] = useState("");

  function submit(event?: FormEvent) {
    event?.preventDefault();
    const next = text.trim();
    if (!next || disabled) return;
    onSend(next);
    setText("");
  }

  return (
    <div className="mx-auto w-full max-w-[760px] px-4 pb-4">
      <form
        onSubmit={submit}
        className="flex items-center gap-2 rounded-full border bg-background px-2 py-1.5 shadow-sm"
      >
        <Button type="button" size="icon-sm" variant="ghost" className="rounded-full" disabled>
          <Plus />
        </Button>
        <Button type="button" size="icon-sm" variant="ghost" className="rounded-full" disabled title="Attach is available from the repository">
          <Paperclip />
        </Button>
        <input
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder={placeholder}
          className="min-w-0 flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
        />
        <Button
          type="submit"
          size="icon-sm"
          className={cn("rounded-full", !text.trim() && "opacity-50")}
          disabled={disabled || !text.trim()}
        >
          <ArrowUp />
        </Button>
      </form>
      {disclaimer ? (
        <p className="pt-2 text-center text-[11px] text-muted-foreground">{disclaimer}</p>
      ) : null}
    </div>
  );
}
