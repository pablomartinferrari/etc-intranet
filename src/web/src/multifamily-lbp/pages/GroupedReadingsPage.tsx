import { Fragment, useMemo, useState } from "react";
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
import { ChevronDownRegular, ChevronRightRegular } from "@fluentui/react-icons";
import { useEntity } from "@mf/context/EntityContext";
import { fetchRows, patchRows, type InspectionRow } from "@mf/api/entity";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";
import { DATA_TYPE_FILTER_OPTIONS, dropdownDisplayValue } from "@mf/config/reportOptions";
import {
  displayComponentWithEdits,
  displaySubstrateWithEdits,
  groupReadingsByComponentSubstrate,
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
  summaryLink: { marginBottom: tokens.spacingVerticalM },
  groupRow: {
    backgroundColor: tokens.colorNeutralBackground3,
    cursor: "pointer",
    ":hover": {
      backgroundColor: tokens.colorNeutralBackground2,
    },
  },
  detailRow: {
    backgroundColor: tokens.colorNeutralBackground1,
  },
  detailIndent: {
    paddingLeft: tokens.spacingHorizontalXXL,
  },
  rowEdited: {
    backgroundColor: tokens.colorBrandBackground2,
  },
  expandBtn: {
    minWidth: "28px",
  },
  groupLabel: { fontWeight: tokens.fontWeightSemibold },
  mono: {
    fontFamily: tokens.fontFamilyMonospace,
    fontSize: tokens.fontSizeBase200,
  },
  footer: {
    marginTop: tokens.spacingVerticalS,
    color: tokens.colorNeutralForeground3,
  },
});

type EditFields = Pick<InspectionRow, "normalizedComponent" | "normalizedSubstrate">;

export function GroupedReadingsPage(): React.JSX.Element {
  const styles = useStyles();
  const tableStyles = useDataTableStyles();
  const { jobId, entitySlug, refetchDashboard } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;
  const qc = useQueryClient();
  const [dataType, setDataType] = useState("");
  const [search, setSearch] = useState("");
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set());
  const [edits, setEdits] = useState<Record<string, Partial<EditFields>>>({});
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

  const groups = useMemo(() => groupReadingsByComponentSubstrate(rows), [rows]);

  const saveMut = useMutation({
    mutationFn: () =>
      patchRows(
        jobId,
        entitySlug,
        Object.entries(edits).map(([id, e]) => ({
          id,
          normalizedComponent: e.normalizedComponent,
          normalizedSubstrate: e.normalizedSubstrate,
        }))
      ),
    onSuccess: (r) => {
      setEdits({});
      setSaveMsg(`Saved ${r.updated} rows`);
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
      refetchDashboard();
    },
  });

  const toggleGroup = (key: string): void => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const expandAll = (): void => setExpanded(new Set(groups.map((g) => g.key)));
  const collapseAll = (): void => setExpanded(new Set());

  const update = (id: string, field: keyof EditFields, value: string): void => {
    setEdits((prev) => ({ ...prev, [id]: { ...prev[id], [field]: value } }));
  };

  const displayComponent = (row: InspectionRow): string =>
    displayComponentWithEdits(row, edits[row.id]);

  const displaySubstrate = (row: InspectionRow): string =>
    displaySubstrateWithEdits(row, edits[row.id]);

  const rowClass = (row: InspectionRow, detail: boolean): string | undefined => {
    if (edits[row.id]) return styles.rowEdited;
    return detail ? styles.detailRow : tableStyles.zebra;
  };

  const columns = ["", "Component", "Substrate", "Reading", "Location", "Pb (mg/cm²)", "Result"];

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalS }}>
        Grouped readings
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM, color: tokens.colorNeutralForeground2 }}>
        Step 5 — readings grouped by component and substrate (normalized values when set). Expand groups to review
        or edit individual shots inline.
      </Text>

      <Text block className={styles.summaryLink}>
        <Link to={`${base}/normalize`}>Back to AI normalization</Link>
        {" · "}
        <Link to={`${base}/grid`}>View flat data grid</Link>
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
        <Field label="Search" className={styles.toolbarField}>
          <Input value={search} onChange={(_, d) => setSearch(d.value)} placeholder="Component, location…" />
        </Field>
        <Button appearance="subtle" onClick={expandAll} disabled={groups.length === 0}>
          Expand all
        </Button>
        <Button appearance="subtle" onClick={collapseAll} disabled={groups.length === 0}>
          Collapse all
        </Button>
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
        <Spinner label="Loading readings…" />
      ) : groups.length === 0 ? (
        <Text block style={{ color: tokens.colorNeutralForeground3 }}>
          No readings to group. Import data or adjust filters.
        </Text>
      ) : (
        <DataTablePanel>
          <Table className={tableStyles.table} size="small" aria-label="Grouped readings">
            <TableHeader className={tableStyles.stickyHead}>
              <TableRow>
                {columns.map((h) => (
                  <TableHeaderCell key={h || "expand"} className={tableStyles.headCell}>
                    {h}
                  </TableHeaderCell>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {groups.map((g) => {
                const isOpen = expanded.has(g.key);
                return (
                  <Fragment key={g.key}>
                    <TableRow
                      className={styles.groupRow}
                      onClick={() => toggleGroup(g.key)}
                      aria-expanded={isOpen}
                    >
                      <TableCell className={tableStyles.bodyCell}>
                        <Button
                          className={styles.expandBtn}
                          appearance="subtle"
                          size="small"
                          icon={isOpen ? <ChevronDownRegular /> : <ChevronRightRegular />}
                          aria-label={isOpen ? "Collapse group" : "Expand group"}
                          onClick={(e) => {
                            e.stopPropagation();
                            toggleGroup(g.key);
                          }}
                        />
                      </TableCell>
                      <TableCell className={`${tableStyles.bodyCell} ${styles.groupLabel}`}>{g.component}</TableCell>
                      <TableCell className={tableStyles.bodyCell}>{g.substrate}</TableCell>
                      <TableCell className={tableStyles.bodyCell}>
                        {g.readingCount} reading{g.readingCount === 1 ? "" : "s"}
                      </TableCell>
                      <TableCell className={tableStyles.bodyCell}>—</TableCell>
                      <TableCell className={`${tableStyles.bodyCell} ${styles.mono}`}>
                        {g.avgLeadContent.toFixed(2)} avg
                      </TableCell>
                      <TableCell className={tableStyles.bodyCell}>
                        {g.positiveCount > 0 ? (
                          <Badge appearance="filled" color="danger">
                            {g.positiveCount} positive
                          </Badge>
                        ) : (
                          <Badge appearance="outline" color="success">
                            All negative
                          </Badge>
                        )}
                      </TableCell>
                    </TableRow>

                    {isOpen &&
                      g.rows.map((row) => (
                        <TableRow
                          key={row.id}
                          className={rowClass(row, true)}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <TableCell className={`${tableStyles.bodyCell} ${styles.detailIndent}`} />
                          <TableCell className={tableStyles.bodyCell}>
                            <Input
                              size="small"
                              appearance="filled-darker"
                              value={displayComponent(row)}
                              onChange={(_, d) => update(row.id, "normalizedComponent", d.value)}
                            />
                          </TableCell>
                          <TableCell className={tableStyles.bodyCell}>
                            <Input
                              size="small"
                              appearance="filled-darker"
                              value={displaySubstrate(row)}
                              onChange={(_, d) => update(row.id, "normalizedSubstrate", d.value)}
                            />
                          </TableCell>
                          <TableCell className={`${tableStyles.bodyCell} ${styles.mono}`}>{row.readingId}</TableCell>
                          <TableCell className={tableStyles.bodyCell}>{row.location || "—"}</TableCell>
                          <TableCell className={`${tableStyles.bodyCell} ${styles.mono}`}>
                            {row.leadContent.toFixed(2)}
                          </TableCell>
                          <TableCell className={tableStyles.bodyCell}>
                            <ReadingResultBadge row={row} />
                          </TableCell>
                        </TableRow>
                      ))}
                  </Fragment>
                );
              })}
            </TableBody>
          </Table>
        </DataTablePanel>
      )}

      <Text size={200} className={styles.footer}>
        {groups.length} group{groups.length === 1 ? "" : "s"} · {rows.length} reading
        {rows.length === 1 ? "" : "s"}
        {Object.keys(edits).length > 0 && ` · ${Object.keys(edits).length} unsaved edit${Object.keys(edits).length === 1 ? "" : "s"}`}
      </Text>
    </div>
  );
}
