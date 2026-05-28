import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { Button, Checkbox, Dropdown, Field, Input, Option, Spinner, Text, Title1, tokens } from "@fluentui/react-components";
import { useEntity } from "@mf/context/EntityContext";
import { generateReport } from "@mf/api/entity";

export function ReportConfigurePage(): React.JSX.Element {
  const { jobId, entitySlug } = useEntity();
  const nav = useNavigate();
  const [dataType, setDataType] = useState("units");
  const [threshold, setThreshold] = useState("40");
  const [useNormalized, setUseNormalized] = useState(true);
  const [sections, setSections] = useState({
    allShots: true,
    uniform: true,
    nonUniform: true,
    byComponent: true,
    bySubstrate: true,
    exceptions: true,
  });

  const mut = useMutation({
    mutationFn: () =>
      generateReport(jobId, entitySlug, {
        dataType,
        sections: Object.entries(sections).filter(([, v]) => v).map(([k]) => k),
        uniformThreshold: Number(threshold) || 40,
        groupBy: "component",
        useNormalizedValues: useNormalized,
      }),
    onSuccess: (r) => nav(`/jobs/${jobId}/${entitySlug}/reports/viewer?reportId=${r.id}`),
  });

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        Configure report
      </Title1>
      <Field label="Data type" style={{ marginBottom: tokens.spacingVerticalM }}>
        <Dropdown value={dataType} onOptionSelect={(_, d) => setDataType(d.optionValue ?? "units")}>
          <Option value="units">Units</Option>
          <Option value="commonAreas">Common Areas</Option>
        </Dropdown>
      </Field>
      <Field label="Uniform threshold" style={{ marginBottom: tokens.spacingVerticalM }}>
        <Input value={threshold} onChange={(_, d) => setThreshold(d.value)} type="number" />
      </Field>
      <Checkbox
        label="Use normalized values"
        checked={useNormalized}
        onChange={(_, d) => setUseNormalized(!!d.checked)}
        style={{ marginBottom: tokens.spacingVerticalM }}
      />
      <Text block style={{ marginBottom: tokens.spacingVerticalS }}>
        Uniform shots: grouped components above threshold. Non-uniform: at or below threshold.
      </Text>
      <Field label="Sections" style={{ marginBottom: tokens.spacingVerticalL }}>
        {Object.entries(sections).map(([key, checked]) => (
          <Checkbox
            key={key}
            label={key}
            checked={checked}
            onChange={(_, d) => setSections((s) => ({ ...s, [key]: !!d.checked }))}
          />
        ))}
      </Field>
      <Button appearance="primary" disabled={mut.isPending} onClick={() => mut.mutate()}>
        {mut.isPending ? <Spinner size="tiny" /> : "Generate report"}
      </Button>
    </div>
  );
}
