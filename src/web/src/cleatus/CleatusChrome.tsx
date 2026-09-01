import { Link as RouterLink } from "react-router-dom";
import { Body1, Button, Title1, makeStyles, tokens } from "@fluentui/react-components";
import { HomeRegular } from "@fluentui/react-icons";
import { useMsal } from "@azure/msal-react";
import etcLogo from "../images/etc-logo.png";

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
  },
  logo: {
    display: "block",
    height: "44px",
    width: "auto",
    maxWidth: "min(100%, 280px)",
    objectFit: "contain",
  },
  signOutButton: {
    color: "#ffffff",
    borderTopColor: "rgba(255, 255, 255, 0.85)",
    borderRightColor: "rgba(255, 255, 255, 0.85)",
    borderBottomColor: "rgba(255, 255, 255, 0.85)",
    borderLeftColor: "rgba(255, 255, 255, 0.85)",
    ":hover": {
      color: "#000000",
      backgroundColor: "#ffffff",
      borderTopColor: "#ffffff",
      borderRightColor: "#ffffff",
      borderBottomColor: "#ffffff",
      borderLeftColor: "#ffffff",
    },
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
});

export function CleatusChrome({
  title,
  subtitle,
  icon,
  children,
}: {
  title: string;
  subtitle: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}): React.JSX.Element {
  const styles = useStyles();
  const { instance } = useMsal();

  return (
    <div className={styles.root}>
      <header className={styles.brandBar}>
        <RouterLink to="/" style={{ lineHeight: 0 }}>
          <img alt="Environmental Testing & Consulting" className={styles.logo} src={etcLogo} />
        </RouterLink>
        <Button
          appearance="outline"
          className={styles.signOutButton}
          onClick={() => void instance.logoutRedirect()}
        >
          Sign out
        </Button>
      </header>
      <div className={styles.appHeader}>
        <div className={styles.brand}>
          {icon}
          <div>
            <Title1>{title}</Title1>
            <Body1>{subtitle}</Body1>
          </div>
        </div>
        <RouterLink to="/" style={{ textDecoration: "none" }}>
          <Button appearance="subtle" icon={<HomeRegular />}>
            Applications
          </Button>
        </RouterLink>
      </div>
      {children}
    </div>
  );
}
