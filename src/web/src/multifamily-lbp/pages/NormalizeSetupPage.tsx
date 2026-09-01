import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";

import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { useEntity } from "@mf/context/EntityContext";
import { runNormalization } from "@mf/api/entity";
import { DATA_TYPE_NORMALIZE_OPTIONS } from "@mf/config/reportOptions";

type NormalizeField = "component" | "substrate";

const NORMALIZE_FIELDS = [
  { value: "component", label: "Component" },
  { value: "substrate", label: "Substrate" },
] as const;

const NORMALIZE_SCOPES = [
  { value: "entire", label: "Entire job" },
  { value: "missing", label: "Only missing normalized values" },
] as const;

export function NormalizeSetupPage(): React.JSX.Element {
  const { jobId, entitySlug } = useEntity();
  const nav = useNavigate();
  const [field, setField] = useState<NormalizeField>("component");
  const [scope, setScope] = useState("entire");
  const [dataType, setDataType] = useState("");

  const mut = useMutation({
    mutationFn: () =>
      runNormalization(jobId, entitySlug, {
        fields: [field],
        scope,
        dataType: dataType || undefined,
      }),
    onSuccess: (result) =>
      nav(
        `/jobs/${jobId}/${entitySlug}/normalize/review?fields=${encodeURIComponent(field)}&autoApplied=${result.autoAppliedCount}`
      ),
  });

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold tracking-tight">AI normalization setup</h1>
      <p className="mb-4">
        Step 4 of the workflow — normalize component and substrate values before reviewing grouped readings.
        AI generates suggestions for values that would change. Exact matches (original = suggestion) are applied
        automatically — only differences appear on the review screen. Run separately for component and substrate.
      </p>
      <div className="mb-4 grid gap-1.5">
        <Label>Field to normalize</Label>
        <Select value={field} onValueChange={(v) => setField(v as NormalizeField)}>
          <SelectTrigger className="w-full max-w-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {NORMALIZE_FIELDS.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="mb-4 grid gap-1.5">
        <Label>Scope</Label>
        <Select value={scope} onValueChange={setScope}>
          <SelectTrigger className="w-full max-w-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {NORMALIZE_SCOPES.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="mb-6 grid gap-1.5">
        <Label>Data type</Label>
        <Select value={dataType || "both"} onValueChange={(v) => setDataType(v === "both" ? "" : v)}>
          <SelectTrigger className="w-full max-w-xs">
            <SelectValue placeholder="Both" />
          </SelectTrigger>
          <SelectContent>
            {DATA_TYPE_NORMALIZE_OPTIONS.map((opt) => (
              <SelectItem key={opt.value || "both"} value={opt.value || "both"}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      {mut.isError && (
        <p className="mb-4 text-destructive">
          {mut.error instanceof Error ? mut.error.message : "Normalization failed."}
        </p>
      )}
      <Button disabled={mut.isPending} onClick={() => mut.mutate()}>
        {mut.isPending ? <Spinner size="sm" /> : "Run normalization"}
      </Button>
    </div>
  );
}
