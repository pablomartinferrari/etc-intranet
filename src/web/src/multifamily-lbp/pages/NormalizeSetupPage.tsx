import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { Button, Dropdown, Field, Option, Spinner, Text, Title1, tokens } from "@fluentui/react-components";
import { useEntity } from "@mf/context/EntityContext";
import { runNormalization } from "@mf/api/entity";
import { DATA_TYPE_NORMALIZE_OPTIONS, dropdownDisplayValue } from "@mf/config/reportOptions";

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
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        AI normalization setup
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM }}>
        Step 4 of the workflow — normalize component and substrate values before reviewing grouped readings.
        AI generates suggestions for values that would change. Exact matches (original = suggestion) are applied
        automatically — only differences appear on the review screen. Run separately for component and substrate.
      </Text>
      <Field label="Field to normalize" style={{ marginBottom: tokens.spacingVerticalM }}>
        <Dropdown
          value={dropdownDisplayValue(NORMALIZE_FIELDS, field)}
          selectedOptions={[field]}
          onOptionSelect={(_, d) => setField((d.optionValue as NormalizeField) ?? "component")}
        >
          {NORMALIZE_FIELDS.map((opt) => (
            <Option key={opt.value} value={opt.value}>
              {opt.label}
            </Option>
          ))}
        </Dropdown>
      </Field>
      <Field label="Scope" style={{ marginBottom: tokens.spacingVerticalM }}>
        <Dropdown
          value={dropdownDisplayValue(NORMALIZE_SCOPES, scope)}
          selectedOptions={[scope]}
          onOptionSelect={(_, d) => setScope(d.optionValue ?? "entire")}
        >
          {NORMALIZE_SCOPES.map((opt) => (
            <Option key={opt.value} value={opt.value}>
              {opt.label}
            </Option>
          ))}
        </Dropdown>
      </Field>
      <Field label="Data type" style={{ marginBottom: tokens.spacingVerticalL }}>
        <Dropdown
          placeholder="Both"
          value={dropdownDisplayValue(DATA_TYPE_NORMALIZE_OPTIONS, dataType)}
          selectedOptions={dataType ? [dataType] : []}
          onOptionSelect={(_, d) => setDataType(d.optionValue ?? "")}
        >
          {DATA_TYPE_NORMALIZE_OPTIONS.map((opt) => (
            <Option key={opt.value || "both"} value={opt.value}>
              {opt.label}
            </Option>
          ))}
        </Dropdown>
      </Field>
      {mut.isError && (
        <Text block style={{ color: tokens.colorPaletteRedForeground1, marginBottom: tokens.spacingVerticalM }}>
          {mut.error instanceof Error ? mut.error.message : "Normalization failed."}
        </Text>
      )}
      <Button appearance="primary" disabled={mut.isPending} onClick={() => mut.mutate()}>
        {mut.isPending ? <Spinner size="tiny" /> : "Run normalization"}
      </Button>
    </div>
  );
}
