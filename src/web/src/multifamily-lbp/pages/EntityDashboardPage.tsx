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
  ArrowUploadRegular,
  GridRegular,
  SparkleRegular,
  DocumentRegular,
} from "@fluentui/react-icons";
import { useEntity } from "@mf/context/EntityContext";

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
  const { jobId, entitySlug, dashboard } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;

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
          icon={<ArrowUploadRegular />}
          onClick={() => nav(`${base}/uploads`)}
        >
          Upload data
        </Button>
        {dashboard.hasRows && (
          <Button icon={<GridRegular />} onClick={() => nav(`${base}/grid`)}>
            Open data grid
          </Button>
        )}
        {dashboard.pendingNormalizationCount > 0 && (
          <Button icon={<SparkleRegular />} onClick={() => nav(`${base}/normalize/review`)}>
            Review AI suggestions
          </Button>
        )}
        <Button icon={<SparkleRegular />} onClick={() => nav(`${base}/normalize`)}>
          Run normalization
        </Button>
        <Button icon={<DocumentRegular />} onClick={() => nav(`${base}/reports/configure`)}>
          Generate report
        </Button>
        {dashboard.hasRows && (
          <Button appearance="subtle" onClick={() => nav(`${base}/grid`)}>
            Continue where you left off →
          </Button>
        )}
      </div>
    </div>
  );
}
