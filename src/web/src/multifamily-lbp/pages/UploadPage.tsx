import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Button,
  Dropdown,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Option,
  Spinner,
  Text,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { ArrowUploadRegular } from "@fluentui/react-icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEntity } from "@mf/context/EntityContext";
import { fetchUploadBatches, uploadFile, importLegacy } from "@mf/api/entity";

const useStyles = makeStyles({
  drop: {
    border: `2px dashed ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalXXL,
    textAlign: "center",
    marginBottom: tokens.spacingVerticalL,
    cursor: "pointer",
  },
  row: { display: "flex", flexWrap: "wrap", gap: tokens.spacingHorizontalM, marginBottom: tokens.spacingVerticalM },
});

export function UploadPage(): React.JSX.Element {
  const styles = useStyles();
  const nav = useNavigate();
  const qc = useQueryClient();
  const { jobId, entitySlug, refetchDashboard } = useEntity();
  const [files, setFiles] = useState<File[]>([]);
  const [dataType, setDataType] = useState("units");
  const [batchName, setBatchName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const batchesQuery = useQuery({
    queryKey: ["uploads", jobId, entitySlug],
    queryFn: () => fetchUploadBatches(jobId, entitySlug),
  });

  const uploadMut = useMutation({
    mutationFn: async () => {
      let lastBatchId = "";
      for (const f of files) {
        const r = await uploadFile(jobId, entitySlug, f, dataType, { batchName: batchName || undefined });
        lastBatchId = r.batchId;
      }
      return lastBatchId;
    },
    onSuccess: (batchId) => {
      void qc.invalidateQueries({ queryKey: ["uploads", jobId, entitySlug] });
      void qc.invalidateQueries({ queryKey: ["dashboard", jobId, entitySlug] });
      refetchDashboard();
      nav(`/jobs/${jobId}/${entitySlug}/uploads/results?batchId=${batchId}`);
    },
    onError: (e: Error) => setError(e.message),
  });

  const importMut = useMutation({
    mutationFn: () => importLegacy(jobId, entitySlug, false),
    onSuccess: (r) => {
      refetchDashboard();
      setError(r.imported === 0 ? "No legacy data found or data already imported." : null);
    },
    onError: (e: Error) => setError(e.message),
  });

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        Upload inspection data
      </Title1>
      <Text block style={{ marginBottom: tokens.spacingVerticalM }}>
        Each file must be either Units or Common Areas — not both.
      </Text>
      <div className={styles.row}>
        <Field label="Data type" required>
          <Dropdown value={dataType} onOptionSelect={(_, d) => setDataType(d.optionValue ?? "units")}>
            <Option value="units">Units</Option>
            <Option value="commonAreas">Common Areas</Option>
          </Dropdown>
        </Field>
        <Field label="Batch name (optional)">
          <Input value={batchName} onChange={(_, d) => setBatchName(d.value)} />
        </Field>
      </div>
      <div
        className={styles.drop}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          setFiles(Array.from(e.dataTransfer.files));
        }}
        onClick={() => document.getElementById("file-input")?.click()}
      >
        <input
          id="file-input"
          type="file"
          accept=".xlsx,.csv"
          multiple
          hidden
          onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
        />
        <ArrowUploadRegular fontSize={32} />
        <Text block>Drop .xlsx or .csv files here, or click to browse</Text>
        {files.length > 0 && <Text size={200}>{files.map((f) => f.name).join(", ")}</Text>}
      </div>
      {error && (
        <MessageBar intent="error" style={{ marginBottom: tokens.spacingVerticalM }}>
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}
      <div className={styles.row}>
        <Button
          appearance="primary"
          disabled={files.length === 0 || uploadMut.isPending}
          onClick={() => uploadMut.mutate()}
        >
          {uploadMut.isPending ? <Spinner size="tiny" /> : "Upload and import"}
        </Button>
        <Button appearance="secondary" disabled={importMut.isPending} onClick={() => importMut.mutate()}>
          Import from SharePoint (legacy)
        </Button>
      </div>
      {batchesQuery.data && batchesQuery.data.length > 0 && (
        <div style={{ marginTop: tokens.spacingVerticalXL }}>
          <Text weight="semibold" block style={{ marginBottom: tokens.spacingVerticalS }}>
            Recent uploads
          </Text>
          {batchesQuery.data.map((b) => (
            <Text key={b.id} block size={200}>
              {b.sourceFileName} · {b.dataType} · {b.status} · {b.importedRowCount} rows
            </Text>
          ))}
        </div>
      )}
    </div>
  );
}
