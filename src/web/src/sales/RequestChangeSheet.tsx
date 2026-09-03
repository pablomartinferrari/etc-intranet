import { useState } from "react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
  featureRequestAreaLabel,
  type FeatureRequest,
  type FeatureRequestPage,
} from "./api/featureRequests";

export function RequestChangeSheet({
  open,
  onOpenChange,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: (request: FeatureRequest) => void;
}) {
  const [page, setPage] = useState<FeatureRequestPage | "">("");
  const [areaLabel, setAreaLabel] = useState("");
  const [rawText, setRawText] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<FeatureRequest | null>(null);

  function reset() {
    setPage("");
    setAreaLabel("");
    setRawText("");
    setSaving(false);
    setError(null);
    setSaved(null);
  }

  function handleOpenChange(next: boolean) {
    onOpenChange(next);
    if (!next) {
      reset();
    }
  }

  function onAreaChange(value: string) {
    const next = value as FeatureRequestPage;
    setPage(next);
    if (next !== "other") {
      setAreaLabel("");
    }
  }

  async function submit() {
    if (!page) {
      setError("Pick which area this request is about.");
      return;
    }

    const otherLabel = areaLabel.trim();
    if (page === "other" && !otherLabel) {
      setError("Name the area or topic this request is about.");
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
      const created = await createFeatureRequest(
        page,
        note,
        page === "other" ? otherLabel : undefined,
      );
      setSaved(created);
      onSaved?.(created);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save the request.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Sheet open={open} onOpenChange={handleOpenChange}>
      <SheetContent className="w-full sm:max-w-lg" side="right">
        <SheetHeader>
          <SheetTitle>{saved ? "Request captured" : "New request"}</SheetTitle>
          <SheetDescription>
            {saved
              ? "Saved in the intranet database. It is on the Feature Requests queue."
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
                  onValueChange={onAreaChange}
                  className="grid grid-cols-2 gap-2"
                >
                  {FEATURE_REQUEST_AREAS.map((area) => (
                    <label
                      key={area.value}
                      className="flex min-h-11 items-center gap-2 rounded-lg border bg-card px-3 py-2 text-sm"
                    >
                      <RadioGroupItem value={area.value} />
                      {area.label}
                    </label>
                  ))}
                </RadioGroup>
              </div>
              {page === "other" && (
                <div className="flex flex-col gap-2">
                  <Label htmlFor="feature-request-area-other">Area or topic</Label>
                  <Input
                    id="feature-request-area-other"
                    value={areaLabel}
                    onChange={(event) => setAreaLabel(event.target.value)}
                    maxLength={120}
                    placeholder="Example: HR onboarding, IT VPN"
                    autoComplete="off"
                  />
                </div>
              )}
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
            <Button type="button" onClick={() => handleOpenChange(false)}>
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
      <TicketField label="Area" value={featureRequestAreaLabel(request)} />
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
