import { NavLink, Outlet, useNavigate, useParams } from "react-router-dom";
import {
  Button,
  makeStyles,
  tokens,
  Text,
  Title2,
  Body1,
  Spinner,
} from "@fluentui/react-components";
import {
  GridRegular,
  ArrowDownloadRegular,
  SparkleRegular,
  DocumentRegular,
  BoardRegular,
  GroupListRegular,
} from "@fluentui/react-icons";
import { RequireAuth } from "@mf/auth/RequireAuth";
import { EntityProvider, useEntity } from "@mf/context/EntityContext";
import { isValidEntitySlug } from "@mf/config/entities";

const useStyles = makeStyles({
  shell: { display: "flex", flexDirection: "column", minHeight: "calc(100vh - 120px)" },
  header: {
    display: "flex",
    flexWrap: "wrap",
    alignItems: "center",
    justifyContent: "space-between",
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground1,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    marginBottom: tokens.spacingVerticalM,
  },
  headerText: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXS },
  headerActions: { display: "flex", gap: tokens.spacingHorizontalS, flexWrap: "wrap" },
  body: { display: "flex", gap: tokens.spacingHorizontalL, flex: 1 },
  nav: {
    width: "200px",
    flexShrink: 0,
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXS,
  },
  navLink: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalS,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderRadius: tokens.borderRadiusMedium,
    textDecoration: "none",
    color: tokens.colorNeutralForeground1,
    ":hover": { backgroundColor: tokens.colorNeutralBackground3 },
  },
  navActive: { backgroundColor: tokens.colorBrandBackground2, fontWeight: 600 },
  main: { flex: 1, minWidth: 0 },
});

function ShellInner(): React.JSX.Element {
  const styles = useStyles();
  const nav = useNavigate();
  const { jobId, entitySlug } = useParams<{ jobId: string; entitySlug: string }>();
  const { entityDisplayName, job, dashboard, isLoading } = useEntity();
  const base = `/jobs/${jobId}/${entitySlug}`;

  if (!isValidEntitySlug(entitySlug ?? "")) {
    return <Text>Unknown entity.</Text>;
  }

  const navItems = [
    { to: `${base}/overview`, label: "Overview", icon: <BoardRegular /> },
    { to: `${base}/uploads`, label: "Source files", icon: <ArrowDownloadRegular /> },
    { to: `${base}/grid`, label: "Data grid", icon: <GridRegular /> },
    { to: `${base}/normalize`, label: "AI normalization", icon: <SparkleRegular /> },
    { to: `${base}/grid/groups`, label: "Grouped readings", icon: <GroupListRegular /> },
    { to: `${base}/reports/configure`, label: "Reports", icon: <DocumentRegular /> },
  ];

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <div className={styles.headerText}>
          <Title2>
            Job {jobId} · {entityDisplayName}
          </Title2>
          <Body1>
            {[job?.clientName, job?.facilityName].filter(Boolean).join(" · ") || " "}
            {dashboard && (
              <>
                {" "}
                · {dashboard.unitsRowCount + dashboard.commonAreasRowCount} rows
              </>
            )}
          </Body1>
        </div>
        <div className={styles.headerActions}>
          <Button appearance="secondary" icon={<SparkleRegular />} onClick={() => nav(`${base}/normalize`)}>
            Normalize
          </Button>
          <Button appearance="primary" icon={<DocumentRegular />} onClick={() => nav(`${base}/reports/configure`)}>
            Generate report
          </Button>
        </div>
      </header>
      {isLoading ? (
        <Spinner label="Loading…" />
      ) : (
        <div className={styles.body}>
          <nav className={styles.nav}>
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === base}
                className={({ isActive }) => `${styles.navLink} ${isActive ? styles.navActive : ""}`}
              >
                {item.icon}
                {item.label}
              </NavLink>
            ))}
          </nav>
          <main className={styles.main}>
            <Outlet />
          </main>
        </div>
      )}
    </div>
  );
}

export function EntityShellLayout(): React.JSX.Element {
  return (
    <RequireAuth>
      <EntityProvider>
        <ShellInner />
      </EntityProvider>
    </RequireAuth>
  );
}
