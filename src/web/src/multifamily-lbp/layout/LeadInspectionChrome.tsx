import { Link as RouterLink } from "react-router-dom";
import { Body1, Button, makeStyles, tokens, Title1 } from "@fluentui/react-components";
import { DocumentDataRegular, HomeRegular } from "@fluentui/react-icons";
import etcLogo from "../../images/etc-logo.png";

const useStyles = makeStyles({
  root: {
    display: "flex",
    flexDirection: "column",
    minHeight: "100vh",
    backgroundColor: tokens.colorNeutralBackground2,
  },
  brandBar: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalM,
    padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalXL}`,
    backgroundColor: "#000000",
    borderRadius: 0,
  },
  logo: {
    display: "block",
    height: "44px",
    width: "auto",
    maxWidth: "min(100%, 280px)",
    objectFit: "contain",
  },
  appHeader: {
    padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalXL}`,
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    flexWrap: "wrap",
    gap: tokens.spacingVerticalM,
  },
  brand: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalM,
  },
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

export function LeadInspectionChrome({
  children,
}: {
  children: React.ReactNode;
}): React.JSX.Element {
  const styles = useStyles();

  return (
    <div className={styles.root}>
      <header className={styles.brandBar}>
        <RouterLink to="/" style={{ lineHeight: 0 }}>
          <img alt="Environmental Testing & Consulting" className={styles.logo} src={etcLogo} />
        </RouterLink>
      </header>
      <div className={styles.appHeader}>
        <div className={styles.brand}>
          <DocumentDataRegular fontSize={28} />
          <div>
            <Title1>Lead Inspection Data Manager</Title1>
            <Body1>Multifamily LBP — SharePoint import, grid, normalization, and reports</Body1>
          </div>
        </div>
        <RouterLink to="/lead-inspection" style={{ textDecoration: "none" }}>
          <Button appearance="subtle" icon={<HomeRegular />}>
            Job lookup
          </Button>
        </RouterLink>
      </div>
      <main className={styles.main}>{children}</main>
      <footer className={styles.footer}>ETC intranet · Lead inspection workspace</footer>
    </div>
  );
}
