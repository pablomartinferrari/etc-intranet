import { useState } from "react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import {
  createFeatureRequest,
  FEATURE_REQUEST_AREAS,
  featureRequestPageLabel,
  type FeatureRequest,
  type FeatureRequestPage,
} from "./api/featureRequests";

export function RequestChangeControl() {
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState<FeatureRequestPage | "">("");
  const [rawText, setRawText] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<FeatureRequest | null>(null);

  function reset() {
    setPage("");
    setRawText("");
    setSaving(false);
    setError(null);
    setSaved(null);
  }

  function onOpenChange(next: boolean) {
    setOpen(next);
    if (!next) {
      reset();
    }
  }

  async function submit() {
    if (!page) {
      setError("Pick which area this request is about.");
      return;
    }

    const note = rawText.trim();
    if (!note) {
      setError("Write a short note about the change you want.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const created = await createFeatureRequest(page, note);
      setSaved(created);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save the request.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <Button type="button" variant="outline" onClick={() => setOpen(true)}>
        Request a change
      </Button>
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent className="sm:max-w-lg" side="right">
          <SheetHeader>
            <SheetTitle>{saved ? "Request captured" : "Request a change"}</SheetTitle>
            <SheetDescription>
              {saved
                ? "Saved in the intranet database. Pablo can review it under Requests from Home."
                : "Pick an area, then describe the change in plain language. We will turn it into a short ticket."}
            </SheetDescription>
          </SheetHeader>
          <div className="flex flex-1 flex-col gap-4 overflow-y-auto px-4 pb-2">
            {error && (
              <Alert variant="destructive">
                <AlertTitle>Could not save</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
            {saved ? (
              <CapturedTicket request={saved} />
            ) : (
              <>
                <div className="flex flex-col gap-2">
                  <Label id="feature-request-area-label">Which area is this about?</Label>
                  <RadioGroup
                    aria-labelledby="feature-request-area-label"
                    value={page || undefined}
                    onValueChange={(value) => setPage(value as FeatureRequestPage)}
                    className="grid grid-cols-2 gap-2"
                  >
                    {FEATURE_REQUEST_AREAS.map((area) => (
                      <label
                        key={area.value}
                        className="flex items-center gap-2 rounded-lg border bg-card px-3 py-2 text-sm"
                      >
                        <RadioGroupItem value={area.value} />
                        {area.label}
                      </label>
                    ))}
                  </RadioGroup>
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="feature-request-note">What do you want?</Label>
                  <Textarea
                    id="feature-request-note"
                    value={rawText}
                    onChange={(event) => setRawText(event.target.value)}
                    rows={8}
                    placeholder="Example: Add a NAICS filter on Bids so I can hide work we never pursue."
                  />
                </div>
              </>
            )}
          </div>
          <SheetFooter>
            {saved ? (
              <Button type="button" onClick={() => onOpenChange(false)}>
                Close
              </Button>
            ) : (
              <Button type="button" onClick={() => void submit()} disabled={saving}>
                {saving ? "Saving…" : "Save request"}
              </Button>
            )}
          </SheetFooter>
        </SheetContent>
      </Sheet>
    </>
  );
}

export function CapturedTicket({ request }: { request: FeatureRequest }) {
  return (
    <div className="flex flex-col gap-3 text-sm">
      <p className="text-muted-foreground">
        {request.structuredBy === "llm"
          ? "Structured from your note."
          : "Saved as written. The assistant was unavailable, so we filled the ticket from your text."}
      </p>
      <TicketField label="Area" value={featureRequestPageLabel(request.page)} />
      <TicketField label="Title" value={request.title} />
      <TicketField label="Problem" value={request.problem} />
      <TicketField label="Desired behavior" value={request.desiredBehavior} />
      <TicketField label="Data involved" value={request.dataInvolved} />
      <TicketField label="Acceptance criteria" value={request.acceptanceCriteria} />
      <TicketField label="Your note" value={request.rawText} />
    </div>
  );
}

function TicketField({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-1">
      <p className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">{label}</p>
      <p className="whitespace-pre-wrap text-foreground">{value.trim() || "—"}</p>
    </div>
  );
}
