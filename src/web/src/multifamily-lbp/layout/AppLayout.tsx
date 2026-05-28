import { Link as RouterLink, Outlet } from "react-router-dom";
import { Button, makeStyles, tokens, Body1, Title1 } from "@fluentui/react-components";
import { DocumentDataRegular, HomeRegular } from "@fluentui/react-icons";

const useStyles = makeStyles({
  root: {
    display: "flex",
    flexDirection: "column",
    minHeight: "100%",
    backgroundColor: tokens.colorNeutralBackground2,
  },
  header: {
    padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalXL}`,
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    flexWrap: "wrap",
    gap: tokens.spacingVerticalM,
  },
  brand: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalM },
  main: {
    flex: 1,
    padding: tokens.spacingVerticalXL,
    maxWidth: "1200px",
    width: "100%",
    margin: "0 auto",
    boxSizing: "border-box",
  },
  footer: {
    padding: tokens.spacingVerticalM,
    textAlign: "center",
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
});

export function AppLayout(): React.JSX.Element {
  const styles = useStyles();

  return (
    <div className={styles.root}>
      <header className={styles.header}>
        <div className={styles.brand}>
          <DocumentDataRegular fontSize={28} />
          <div>
            <Title1>XRF Multifamily</Title1>
            <Body1>Lead paint inspection processing</Body1>
          </div>
        </div>
        <RouterLink to="/lead-inspection" style={{ textDecoration: "none" }}>
          <Button appearance="subtle" icon={<HomeRegular />}>
            Home
          </Button>
        </RouterLink>
      </header>
      <main className={styles.main}>
        <Outlet />
      </main>
      <footer className={styles.footer}>
        Ported from SPFx — React + Fluent UI v9 + C# API
      </footer>
    </div>
  );
}
