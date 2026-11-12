import { useMemo } from "react";
import { CheckIcon, CircleIcon } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Spinner } from "@/components/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";
import type { SharePointSourceFile } from "@mf/api/entity";
import { DataTablePanel, useDataTableStyles } from "@mf/components/DataTablePanel";

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
  const missing = count === 0;
  return (
    <div className="min-w-[160px] flex-1 basis-[180px] rounded-md border bg-card p-4">
      <p className="mb-1 text-xs text-muted-foreground">{label}</p>
      <p
        className={cn(
          "flex items-center gap-1.5 font-semibold",
          missing ? "text-muted-foreground" : "text-green-600",
        )}
      >
        {missing ? <CircleIcon className="size-4" /> : <CheckIcon className="size-4" />}
        {missing ? "Not uploaded yet" : `${count} file${count === 1 ? "" : "s"}`}
      </p>
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
  const tableStyles = useDataTableStyles();
  const grouped = useMemo(() => groupFiles(files), [files]);

  return (
    <div className="rounded-md border bg-muted p-4">
      <p className="mb-1 font-semibold">Files on SharePoint for job {jobId}</p>
      <p className="mb-4 text-sm text-muted-foreground">
        Upload separate files for Units and Common Areas in SharePoint. This list matches the upload web part.
      </p>

      {loading ? (
        <Spinner label="Loading SharePoint files…" />
      ) : error ? (
        <p className="text-destructive">{error}</p>
      ) : (
        <>
          <div className="mb-4 flex flex-wrap gap-4">
            <AreaStatus label="Units" count={grouped.units.length} />
            <AreaStatus label="Common Areas" count={grouped.commonAreas.length} />
          </div>

          {files.length === 0 ? (
            <p className="text-sm text-muted-foreground">No files uploaded yet for this job on SharePoint.</p>
          ) : (
            <DataTablePanel maxHeight="min(50vh, 400px)">
              <Table className={tableStyles.table} aria-label="SharePoint source files">
                <TableHeader className={tableStyles.stickyHead}>
                  <TableRow>
                    {["File", "Type", "Uploaded", "Status"].map((h) => (
                      <TableHead key={h} className={tableStyles.headCell}>
                        {h}
                      </TableHead>
                    ))}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {files.map((file) => (
                    <TableRow key={file.id} className={tableStyles.zebra}>
                      <TableCell className={tableStyles.bodyCell}>
                        <span className="block max-w-[280px] truncate" title={file.fileName}>
                          {file.fileName}
                        </span>
                      </TableCell>
                      <TableCell className={tableStyles.bodyCell}>{file.areaType}</TableCell>
                      <TableCell className={tableStyles.bodyCell}>{formatDate(file.createdAt)}</TableCell>
                      <TableCell className={tableStyles.bodyCell}>
                        <Badge variant="outline">{file.processedStatus || "Pending"}</Badge>
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
