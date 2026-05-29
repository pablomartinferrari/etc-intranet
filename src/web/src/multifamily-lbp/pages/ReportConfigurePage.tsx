import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import {
  Button,
  Checkbox,
  Dropdown,
  Field,
  Option,
  Spinner,
  Text,
  Title1,
  tokens,
} from "@fluentui/react-components";
import { useEntity } from "@mf/context/EntityContext";
import { generateReport } from "@mf/api/entity";
import {
  REPORT_DATA_TYPES,
  REPORT_SECTIONS,
  STATISTICAL_SAMPLE_SIZE,
  dropdownDisplayValue,
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
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        Configure report
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM, color: tokens.colorNeutralForeground2 }}>
        Summaries follow HUD/EPA rules per component: groups with {STATISTICAL_SAMPLE_SIZE} or more readings appear
        under Average (positive if more than 2.5% of shots are positive). Groups below {STATISTICAL_SAMPLE_SIZE}{" "}
        readings are Uniform when every shot is the same result, or Non-uniform when mixed (with individual shot
        detail).
      </Text>

      <Field label="Data type" style={{ marginBottom: tokens.spacingVerticalL }}>
        <Dropdown
          value={dropdownDisplayValue(REPORT_DATA_TYPES, dataType)}
          selectedOptions={[dataType]}
          onOptionSelect={(_, d) => setDataType(d.optionValue ?? "units")}
        >
          {REPORT_DATA_TYPES.map((dt) => (
            <Option key={dt.value} value={dt.value}>
              {dt.label}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Sections to include" style={{ marginBottom: tokens.spacingVerticalL }}>
        {REPORT_SECTIONS.map(({ key, label }) => (
          <Checkbox
            key={key}
            label={label}
            checked={sections[key] ?? false}
            onChange={(_, d) => setSections((s) => ({ ...s, [key]: !!d.checked }))}
          />
        ))}
      </Field>

      {mut.isError && (
        <Text block style={{ color: tokens.colorPaletteRedForeground1, marginBottom: tokens.spacingVerticalM }}>
          {mut.error instanceof Error ? mut.error.message : "Report generation failed."}
        </Text>
      )}

      <Button appearance="primary" disabled={mut.isPending} onClick={() => mut.mutate()}>
        {mut.isPending ? <Spinner size="tiny" /> : "Generate report"}
      </Button>
    </div>
  );
}
