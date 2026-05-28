import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { Button, Checkbox, Dropdown, Field, Option, Spinner, Text, Title1, tokens } from "@fluentui/react-components";
import { useEntity } from "@mf/context/EntityContext";
import { runNormalization } from "@mf/api/entity";

export function NormalizeSetupPage(): React.JSX.Element {
  const { jobId, entitySlug } = useEntity();
  const nav = useNavigate();
  const [fields, setFields] = useState({ component: true, substrate: true });
  const [scope, setScope] = useState("entire");
  const [dataType, setDataType] = useState("");

  const mut = useMutation({
    mutationFn: () =>
      runNormalization(jobId, entitySlug, {
        fields: [
          ...(fields.component ? ["component"] : []),
          ...(fields.substrate ? ["substrate"] : []),
        ],
        scope,
        dataType: dataType || undefined,
      }),
    onSuccess: () => nav(`/jobs/${jobId}/${entitySlug}/normalize/review`),
  });

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        AI normalization setup
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM }}>
        AI generates suggestions only — you approve before values are applied. Original values are preserved.
      </Text>
      <Field label="Fields to normalize" style={{ marginBottom: tokens.spacingVerticalM }}>
        <Checkbox label="Component" checked={fields.component} onChange={(_, d) => setFields((f) => ({ ...f, component: !!d.checked }))} />
        <Checkbox label="Substrate" checked={fields.substrate} onChange={(_, d) => setFields((f) => ({ ...f, substrate: !!d.checked }))} />
      </Field>
      <Field label="Scope" style={{ marginBottom: tokens.spacingVerticalM }}>
        <Dropdown value={scope} onOptionSelect={(_, d) => setScope(d.optionValue ?? "entire")}>
          <Option value="entire">Entire job</Option>
          <Option value="missing">Only missing normalized values</Option>
        </Dropdown>
      </Field>
      <Field label="Data type" style={{ marginBottom: tokens.spacingVerticalL }}>
        <Dropdown value={dataType} placeholder="Both" onOptionSelect={(_, d) => setDataType(d.optionValue ?? "")}>
          <Option value="">Both</Option>
          <Option value="units">Units</Option>
          <Option value="commonAreas">Common Areas</Option>
        </Dropdown>
      </Field>
      <Button appearance="primary" disabled={mut.isPending} onClick={() => mut.mutate()}>
        {mut.isPending ? <Spinner size="tiny" /> : "Run normalization"}
      </Button>
    </div>
  );
}
