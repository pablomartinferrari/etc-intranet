import { useNavigate } from "react-router-dom";
import {
  Button,
  Card,
  CardHeader,
  makeStyles,
  tokens,
  Text,
  Title1,
} from "@fluentui/react-components";
import {
  ArrowDownloadRegular,
  DeleteRegular,
  DocumentRegular,
  GridRegular,
  GroupListRegular,
  SparkleRegular,
} from "@fluentui/react-icons";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useEntity } from "@mf/context/EntityContext";
import { clearWorkspace } from "@mf/api/entity";
import { ClearWorkspaceDialog } from "@mf/components/ClearWorkspaceDialog";

const useStyles = makeStyles({
  grid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))",
    gap: tokens.spacingHorizontalL,
    marginBottom: tokens.spacingVerticalXL,
  },
  stat: { fontSize: tokens.fontSizeHero800, fontWeight: 700 },
  actions: { display: "flex", flexWrap: "wrap", gap: tokens.spacingHorizontalM },
});

export function EntityDashboardPage(): React.JSX.Element {
  const styles = useStyles();
  const nav = useNavigate();
  const { jobId, entitySlug, dashboard, refetchDashboard } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;
  const qc = useQueryClient();
  const [clearOpen, setClearOpen] = useState(false);

  const clearMut = useMutation({
    mutationFn: () => clearWorkspace(jobId, entitySlug),
    onSuccess: async () => {
      setClearOpen(false);
      await refetchDashboard();
      void qc.invalidateQueries({ queryKey: ["rows", jobId, entitySlug] });
      void qc.invalidateQueries({ queryKey: ["normalizations", jobId, entitySlug] });
    },
  });

  if (!dashboard) return <Text>Loading dashboard…</Text>;

  return (
    <div>
      <Title1 block style={{ marginBottom: tokens.spacingVerticalL }}>
        Dashboard
      </Title1>
      <div className={styles.grid}>
        <Card>
          <CardHeader header={<Text weight="semibold">Uploaded files</Text>} />
          <Text className={styles.stat}>{dashboard.uploadedFilesCount}</Text>
        </Card>
        <Card>
          <CardHeader header={<Text weight="semibold">Units rows</Text>} />
          <Text className={styles.stat}>{dashboard.unitsRowCount}</Text>
        </Card>
        <Card>
          <CardHeader header={<Text weight="semibold">Common areas rows</Text>} />
          <Text className={styles.stat}>{dashboard.commonAreasRowCount}</Text>
        </Card>
        <Card>
          <CardHeader header={<Text weight="semibold">Validation warnings</Text>} />
          <Text className={styles.stat}>{dashboard.validationWarningCount}</Text>
        </Card>
        <Card>
          <CardHeader header={<Text weight="semibold">Pending AI review</Text>} />
          <Text className={styles.stat}>{dashboard.pendingNormalizationCount}</Text>
        </Card>
      </div>
      <div className={styles.actions}>
        <Button
          appearance="primary"
          icon={<ArrowDownloadRegular />}
          onClick={() => nav(`${base}/uploads`)}
        >
          Import from SharePoint
        </Button>
        {dashboard.hasRows && (
          <Button icon={<GridRegular />} onClick={() => nav(`${base}/grid`)}>
            Open data grid
          </Button>
        )}
        <Button icon={<SparkleRegular />} onClick={() => nav(`${base}/normalize`)}>
          Run normalization
        </Button>
        {dashboard.pendingNormalizationCount > 0 && (
          <Button icon={<SparkleRegular />} onClick={() => nav(`${base}/normalize/review`)}>
            Review AI suggestions
          </Button>
        )}
        {dashboard.hasRows && (
          <Button icon={<GroupListRegular />} onClick={() => nav(`${base}/grid/groups`)}>
            Grouped readings
          </Button>
        )}
        <Button icon={<DocumentRegular />} onClick={() => nav(`${base}/reports/configure`)}>
          Generate report
        </Button>
        {dashboard.hasRows && (
          <Button
            appearance="outline"
            icon={<DeleteRegular />}
            onClick={() => setClearOpen(true)}
          >
            Clear workspace data
          </Button>
        )}
      </div>

      <ClearWorkspaceDialog
        open={clearOpen}
        pending={clearMut.isPending}
        onConfirm={() => clearMut.mutate()}
        onCancel={() => setClearOpen(false)}
      />
    </div>
  );
}
