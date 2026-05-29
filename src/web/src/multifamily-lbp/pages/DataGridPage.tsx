import { useMemo, useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Badge,
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
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { useEntity } from "@mf/context/EntityContext";
import { fetchRows, patchRows, type InspectionRow } from "@mf/api/entity";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";
import { DATA_TYPE_FILTER_OPTIONS, RESULT_FILTER_OPTIONS, dropdownDisplayValue } from "@mf/config/reportOptions";
import {
  displayComponentWithEdits,
  displaySubstrate,
} from "@mf/utils/readingGroups";
import { ReadingResultBadge } from "@mf/utils/readingResult";

const useStyles = makeStyles({
  toolbar: {
    display: "flex",
    flexWrap: "wrap",
    alignItems: "flex-end",
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  toolbarField: { minWidth: "180px" },
  rowEdited: {
    backgroundColor: tokens.colorBrandBackground2,
  },
  rowWarning: {
    backgroundColor: tokens.colorPaletteYellowBackground1,
  },
  rowError: {
    backgroundColor: tokens.colorPaletteRedBackground1,
  },
  mono: {
    fontFamily: tokens.fontFamilyMonospace,
    fontSize: tokens.fontSizeBase200,
  },
  footer: {
    marginTop: tokens.spacingVerticalS,
    color: tokens.colorNeutralForeground3,
  },
  empty: {
    padding: tokens.spacingVerticalXXL,
    textAlign: "center",
    color: tokens.colorNeutralForeground3,
  },
});

export function DataGridPage(): React.JSX.Element {
  const styles = useStyles();
  const tableStyles = useDataTableStyles();
  const { jobId, entitySlug, refetchDashboard, dashboard } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;
  const qc = useQueryClient();
  const [dataType, setDataType] = useState<string>("");
  const [resultFilter, setResultFilter] = useState<string>("");
  const [search, setSearch] = useState("");
  const [edits, setEdits] = useState<Record<string, Partial<InspectionRow>>>({});
  const [saveMsg, setSaveMsg] = useState<string | null>(null);

  const params = useMemo(() => {
    const p: Record<string, string> = {};
    if (dataType) p.dataType = dataType;
    if (resultFilter) p.result = resultFilter;
    if (search) p.search = search;
    return p;
  }, [dataType, resultFilter, search]);

  const { data: rows = [], isLoading } = useQuery({
    queryKey: ["rows", jobId, entitySlug, params],
    queryFn: () => fetchRows(jobId, entitySlug, params),
  });

  useEffect(() => {
    void refetchDashboard();
  }, [jobId, entitySlug, refetchDashboard]);

  const totalRows =
    dashboard != null ? dashboard.unitsRowCount + dashboard.commonAreasRowCount : null;
  const hasFilters = Boolean(dataType || resultFilter || search);

  const saveMut = useMutation({
    mutationFn: () =>
      patchRows(
        jobId,
        entitySlug,
        Object.entries(edits).map(([id, e]) => ({
          id,
          location: e.location as string | undefined,
          normalizedComponent: e.normalizedComponent as string | undefined,
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

  const displayComponent = (row: InspectionRow): string =>
    displayComponentWithEdits(row, edits[row.id]);

  const rowClass = (row: InspectionRow): string | undefined => {
    if (edits[row.id]) return styles.rowEdited;
    if (row.validationStatus === "error") return styles.rowError;
    if (row.validationStatus === "warning") return styles.rowWarning;
    return tableStyles.zebra;
  };

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalS }}>
        Data grid
        {totalRows != null && (
          <Text
            block
            size={400}
            weight="regular"
            style={{ color: tokens.colorNeutralForeground2, marginTop: tokens.spacingVerticalXS }}
          >
            {hasFilters
              ? `Showing ${rows.length.toLocaleString()} of ${totalRows.toLocaleString()} rows`
              : `${totalRows.toLocaleString()} rows total`}
          </Text>
        )}
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalL, color: tokens.colorNeutralForeground2 }}>
        Edit locations and components inline. Component shows the normalized name when set, otherwise the imported
        value; edits are saved to the normalized field. Changes are highlighted until you save.{" "}
        <Link to={`${base}/normalize`}>Next: AI normalization</Link>
      </Text>

      <div className={styles.toolbar}>
        <Field label="Data type" className={styles.toolbarField}>
          <Dropdown
            placeholder="All types"
            value={dropdownDisplayValue(DATA_TYPE_FILTER_OPTIONS, dataType)}
            selectedOptions={dataType ? [dataType] : []}
            onOptionSelect={(_, d) => setDataType(d.optionValue ?? "")}
          >
            {DATA_TYPE_FILTER_OPTIONS.map((opt) => (
              <Option key={opt.value || "all"} value={opt.value}>
                {opt.label}
              </Option>
            ))}
          </Dropdown>
        </Field>
        <Field label="Result" className={styles.toolbarField}>
          <Dropdown
            placeholder="All results"
            value={dropdownDisplayValue(RESULT_FILTER_OPTIONS, resultFilter)}
            selectedOptions={resultFilter ? [resultFilter] : []}
            onOptionSelect={(_, d) => setResultFilter(d.optionValue ?? "")}
          >
            {RESULT_FILTER_OPTIONS.map((opt) => (
              <Option key={opt.value || "all-results"} value={opt.value}>
                {opt.label}
              </Option>
            ))}
          </Dropdown>
        </Field>
        <Field label="Search" className={styles.toolbarField}>
          <Input value={search} onChange={(_, d) => setSearch(d.value)} placeholder="Component, location…" />
        </Field>
        <Button
          appearance="primary"
          disabled={Object.keys(edits).length === 0 || saveMut.isPending}
          onClick={() => saveMut.mutate()}
        >
          {saveMut.isPending ? <Spinner size="tiny" /> : "Save changes"}
        </Button>
        {Object.keys(edits).length > 0 && (
          <Button appearance="secondary" onClick={() => setEdits({})}>
            Discard edits
          </Button>
        )}
      </div>

      {saveMsg && (
        <MessageBar intent="success" style={{ marginBottom: tokens.spacingVerticalM }}>
          <MessageBarBody>{saveMsg}</MessageBarBody>
        </MessageBar>
      )}

      {isLoading ? (
        <Spinner label="Loading rows…" />
      ) : rows.length === 0 ? (
        <div className={styles.empty}>
          <Text>No rows match your filters. Import from SharePoint or adjust filters.</Text>
        </div>
      ) : (
        <DataTablePanel>
          <Table className={tableStyles.table} size="small" aria-label="Inspection data grid">
            <TableHeader className={tableStyles.stickyHead}>
              <TableRow>
                {["Reading", "Type", "Location", "Component", "Substrate", "Pb (mg/cm²)", "Result"].map((h) => (
                  <TableHeaderCell key={h} className={tableStyles.headCell}>
                    {h}
                  </TableHeaderCell>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id} className={rowClass(row)}>
                  <TableCell className={`${tableStyles.bodyCell} ${styles.mono}`}>{row.readingId}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <Badge appearance="outline" color="informative">
                      {row.dataType === "commonAreas" ? "Common" : "Units"}
                    </Badge>
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <Input
                      size="small"
                      appearance="filled-darker"
                      value={display(row, "location")}
                      onChange={(_, d) => update(row.id, "location", d.value)}
                    />
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <Input
                      size="small"
                      appearance="filled-darker"
                      value={displayComponent(row)}
                      onChange={(_, d) => update(row.id, "normalizedComponent", d.value)}
                    />
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>{displaySubstrate(row)}</TableCell>
                  <TableCell className={`${tableStyles.bodyCell} ${styles.mono}`}>{row.leadContent.toFixed(2)}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <ReadingResultBadge row={row} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </DataTablePanel>
      )}

      <Text size={200} className={styles.footer}>
        {hasFilters && totalRows != null
          ? `${rows.length.toLocaleString()} of ${totalRows.toLocaleString()} rows shown`
          : `${rows.length.toLocaleString()} row${rows.length === 1 ? "" : "s"}`}
        {totalRows != null && !hasFilters && ` · ${totalRows.toLocaleString()} total`}
      </Text>
    </div>
  );
}
