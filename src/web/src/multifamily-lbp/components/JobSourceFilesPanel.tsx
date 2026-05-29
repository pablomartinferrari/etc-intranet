import { useMemo } from "react";
import {
  Badge,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { CheckmarkRegular, CircleRegular } from "@fluentui/react-icons";
import type { SharePointSourceFile } from "@mf/api/entity";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";

const useStyles = makeStyles({
  panel: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  statusGrid: {
    display: "flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
  statusTile: {
    flex: "1 1 180px",
    minWidth: "160px",
    padding: tokens.spacingVerticalM,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  statusLabel: { color: tokens.colorNeutralForeground3, marginBottom: tokens.spacingVerticalXS },
  fileName: {
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
    maxWidth: "280px",
  },
});

function normalizeAreaType(areaType: string): "Units" | "Common Areas" | null {
  const t = areaType.trim();
  const compact = t.replace(/\s/g, "");
  if (/^units$/i.test(t) || /^units$/i.test(compact)) return "Units";
  if (/^common\s*areas?$/i.test(t) || /^commonareas?$/i.test(compact)) return "Common Areas";
  return null;
}

function groupFiles(files: SharePointSourceFile[]): {
  units: SharePointSourceFile[];
  commonAreas: SharePointSourceFile[];
} {
  const units: SharePointSourceFile[] = [];
  const commonAreas: SharePointSourceFile[] = [];
  for (const f of files) {
    const area = normalizeAreaType(f.areaType);
    if (area === "Units") units.push(f);
    else if (area === "Common Areas") commonAreas.push(f);
  }
  return { units, commonAreas };
}

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  return new Date(iso).toLocaleString();
}

function AreaStatus({
  label,
  count,
}: {
  label: string;
  count: number;
}): React.JSX.Element {
  const styles = useStyles();
  const missing = count === 0;
  return (
    <div className={styles.statusTile}>
      <Text size={200} className={styles.statusLabel}>
        {label}
      </Text>
      <Text weight="semibold" style={{ color: missing ? tokens.colorNeutralForeground3 : tokens.colorPaletteGreenForeground1 }}>
        {missing ? (
          <>
            <CircleRegular style={{ marginRight: 6, verticalAlign: "middle" }} />
            Not uploaded yet
          </>
        ) : (
          <>
            <CheckmarkRegular style={{ marginRight: 6, verticalAlign: "middle" }} />
            {count} file{count === 1 ? "" : "s"}
          </>
        )}
      </Text>
    </div>
  );
}

export function JobSourceFilesPanel({
  jobId,
  files,
  loading,
  error,
}: {
  jobId: string;
  files: SharePointSourceFile[];
  loading?: boolean;
  error?: string | null;
}): React.JSX.Element {
  const styles = useStyles();
  const tableStyles = useDataTableStyles();
  const grouped = useMemo(() => groupFiles(files), [files]);

  return (
    <div className={styles.panel}>
      <Text weight="semibold" block style={{ marginBottom: tokens.spacingVerticalXS }}>
        Files on SharePoint for job {jobId}
      </Text>
      <Text block size={200} style={{ color: tokens.colorNeutralForeground2, marginBottom: tokens.spacingVerticalM }}>
        Upload separate files for Units and Common Areas in SharePoint. This list matches the upload web part.
      </Text>

      {loading ? (
        <Spinner label="Loading SharePoint files…" />
      ) : error ? (
        <Text style={{ color: tokens.colorPaletteRedForeground1 }}>{error}</Text>
      ) : (
        <>
          <div className={styles.statusGrid}>
            <AreaStatus label="Units" count={grouped.units.length} />
            <AreaStatus label="Common Areas" count={grouped.commonAreas.length} />
          </div>

          {files.length === 0 ? (
            <Text block size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              No files uploaded yet for this job on SharePoint.
            </Text>
          ) : (
            <DataTablePanel maxHeight="min(50vh, 400px)">
              <Table className={tableStyles.table} size="small" aria-label="SharePoint source files">
                <TableHeader className={tableStyles.stickyHead}>
                  <TableRow>
                    {["File", "Type", "Uploaded", "Status"].map((h) => (
                      <TableHeaderCell key={h} className={tableStyles.headCell}>
                        {h}
                      </TableHeaderCell>
                    ))}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {files.map((file) => (
                    <TableRow key={file.id} className={tableStyles.zebra}>
                      <TableCell className={tableStyles.bodyCell}>
                        <span className={styles.fileName} title={file.fileName}>
                          {file.fileName}
                        </span>
                      </TableCell>
                      <TableCell className={tableStyles.bodyCell}>{file.areaType}</TableCell>
                      <TableCell className={tableStyles.bodyCell}>{formatDate(file.createdAt)}</TableCell>
                      <TableCell className={tableStyles.bodyCell}>
                        <Badge appearance="outline">{file.processedStatus || "Pending"}</Badge>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </DataTablePanel>
          )}
        </>
      )}
    </div>
  );
}
