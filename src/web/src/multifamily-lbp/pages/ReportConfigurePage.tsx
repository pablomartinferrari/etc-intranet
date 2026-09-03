import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";

import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
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
import { generateReport } from "@mf/api/entity";
import {
  REPORT_DATA_TYPES,
  REPORT_SECTIONS,
  STATISTICAL_SAMPLE_SIZE,
} from "@mf/config/reportOptions";

export function ReportConfigurePage(): React.JSX.Element {
  const { jobId, entitySlug } = useEntity();
  const nav = useNavigate();
  const [dataType, setDataType] = useState<string>(REPORT_DATA_TYPES[0].value);
  const [sections, setSections] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(REPORT_SECTIONS.map((s) => [s.key, true]))
  );

  const mut = useMutation({
    mutationFn: () =>
      generateReport(jobId, entitySlug, {
        dataType,
        sections: REPORT_SECTIONS.filter((s) => sections[s.key]).map((s) => s.key),
        uniformThreshold: STATISTICAL_SAMPLE_SIZE,
        groupBy: "component",
        useNormalizedValues: true,
      }),
    onSuccess: (r) => nav(`/jobs/${jobId}/${entitySlug}/reports/viewer?reportId=${r.id}`),
  });

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold tracking-tight">Configure report</h1>
      <p className="mb-4 text-muted-foreground">
        Summaries follow HUD/EPA rules per component: groups with {STATISTICAL_SAMPLE_SIZE} or more readings appear
        under Average (positive if more than 2.5% of shots are positive). Groups below {STATISTICAL_SAMPLE_SIZE}{" "}
        readings are Uniform when every shot is the same result, or Non-uniform when mixed (with individual shot
        detail).
      </p>

      <div className="mb-6 grid gap-1.5">
        <Label>Data type</Label>
        <Select value={dataType} onValueChange={setDataType}>
          <SelectTrigger className="w-full max-w-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {REPORT_DATA_TYPES.map((dt) => (
              <SelectItem key={dt.value} value={dt.value}>
                {dt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="mb-6 grid gap-2">
        <Label>Sections to include</Label>
        {REPORT_SECTIONS.map(({ key, label }) => (
          <label key={key} className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={sections[key] ?? false}
              onCheckedChange={(checked) => setSections((s) => ({ ...s, [key]: !!checked }))}
            />
            {label}
          </label>
        ))}
      </div>

      {mut.isError && (
        <p className="mb-4 text-destructive">
          {mut.error instanceof Error ? mut.error.message : "Report generation failed."}
        </p>
      )}

      <Button disabled={mut.isPending} onClick={() => mut.mutate()}>
        {mut.isPending ? <Spinner size="sm" /> : "Generate report"}
      </Button>
    </div>
  );
}
