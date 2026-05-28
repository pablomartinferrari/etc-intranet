import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Button,
  Dropdown,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Option,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Title1,
  tokens,
} from "@fluentui/react-components";
import { useEntity } from "@mf/context/EntityContext";
import { fetchRows, patchRows, type InspectionRow } from "@mf/api/entity";

export function DataGridPage(): React.JSX.Element {
  const { jobId, entitySlug, refetchDashboard } = useEntity();
  const qc = useQueryClient();
  const [dataType, setDataType] = useState<string>("");
  const [search, setSearch] = useState("");
  const [edits, setEdits] = useState<Record<string, Partial<InspectionRow>>>({});
  const [saveMsg, setSaveMsg] = useState<string | null>(null);

  const params = useMemo(() => {
    const p: Record<string, string> = {};
    if (dataType) p.dataType = dataType;
    if (search) p.search = search;
    return p;
  }, [dataType, search]);

  const { data: rows = [], isLoading } = useQuery({
    queryKey: ["rows", jobId, entitySlug, params],
    queryFn: () => fetchRows(jobId, entitySlug, params),
  });

  const saveMut = useMutation({
    mutationFn: () =>
      patchRows(
        jobId,
        entitySlug,
        Object.entries(edits).map(([id, e]) => ({
          id,
          location: e.location as string | undefined,
          component: e.component as string | undefined,
          normalizedComponent: e.normalizedComponent as string | undefined,
          substrate: e.substrate as string | undefined,
          normalizedSubstrate: e.normalizedSubstrate as string | undefined,
          notes: e.notes as string | undefined,
        }))
      ),
    onSuccess: (r) => {
      setEdits({});
      setSaveMsg(`Saved ${r.updated} rows`);
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
      refetchDashboard();
    },
  });

  const update = (id: string, field: keyof InspectionRow, value: string): void => {
    setEdits((prev) => ({ ...prev, [id]: { ...prev[id], [field]: value } }));
  };

  const display = (row: InspectionRow, field: keyof InspectionRow): string => {
    const e = edits[row.id];
    const v = e && field in e ? (e as Record<string, unknown>)[field] : row[field];
    return v == null ? "" : String(v);
  };

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        Data grid
      </Title1>
      <div style={{ display: "flex", flexWrap: "wrap", gap: tokens.spacingHorizontalM, marginBottom: tokens.spacingVerticalM }}>
        <Field label="Data type">
          <Dropdown value={dataType} placeholder="All" onOptionSelect={(_, d) => setDataType(d.optionValue ?? "")}>
            <Option value="">All</Option>
            <Option value="units">Units</Option>
            <Option value="commonAreas">Common Areas</Option>
          </Dropdown>
        </Field>
        <Field label="Search">
          <Input value={search} onChange={(_, d) => setSearch(d.value)} placeholder="Component, location…" />
        </Field>
        <Button appearance="primary" disabled={Object.keys(edits).length === 0 || saveMut.isPending} onClick={() => saveMut.mutate()}>
          {saveMut.isPending ? <Spinner size="tiny" /> : "Save changes"}
        </Button>
      </div>
      {saveMsg && (
        <MessageBar intent="success" style={{ marginBottom: tokens.spacingVerticalM }}>
          <MessageBarBody>{saveMsg}</MessageBarBody>
        </MessageBar>
      )}
      {isLoading ? (
        <Spinner label="Loading rows…" />
      ) : (
        <div style={{ overflow: "auto", maxHeight: "560px", border: `1px solid ${tokens.colorNeutralStroke2}` }}>
          <Table size="small">
            <TableHeader>
              <TableRow>
                {["Reading", "Type", "Location", "Component", "Normalized", "Substrate", "Pb", "Status"].map((h) => (
                  <TableHeaderCell key={h}>{h}</TableHeaderCell>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id} style={row.validationStatus !== "clean" ? { backgroundColor: tokens.colorPaletteYellowBackground2 } : undefined}>
                  <TableCell>{row.readingId}</TableCell>
                  <TableCell>{row.dataType}</TableCell>
                  <TableCell>
                    <Input size="small" value={display(row, "location")} onChange={(_, d) => update(row.id, "location", d.value)} />
                  </TableCell>
                  <TableCell>
                    <Input size="small" value={display(row, "component")} onChange={(_, d) => update(row.id, "component", d.value)} />
                  </TableCell>
                  <TableCell>
                    <Input size="small" value={display(row, "normalizedComponent")} onChange={(_, d) => update(row.id, "normalizedComponent", d.value)} />
                  </TableCell>
                  <TableCell>{row.substrate ?? ""}</TableCell>
                  <TableCell>{row.leadContent.toFixed(2)}</TableCell>
                  <TableCell>{row.validationStatus}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
      <Text size={200} style={{ marginTop: tokens.spacingVerticalS }}>
        {rows.length} rows · Original values preserved in component/substrate columns
      </Text>
    </div>
  );
}
