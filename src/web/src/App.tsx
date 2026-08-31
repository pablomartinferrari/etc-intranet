import {
  FluentProvider,
  Tab,
  TabList,
  makeStyles,
  tokens,
  webLightTheme,
} from "@fluentui/react-components";
import { BuildingBank24Regular } from "@fluentui/react-icons";
import { Outlet, useLocation, useNavigate } from "react-router-dom";

export default function App() {
  const styles = useStyles();
  const location = useLocation();
  const navigate = useNavigate();
  const selected = location.pathname.startsWith("/opportunities")
    ? "opportunities"
    : location.pathname.startsWith("/pipeline")
      ? "pipeline"
      : "home";

  return (
    <FluentProvider theme={webLightTheme}>
      <div className={styles.shell}>
        <header className={styles.nav}>
          <div className={styles.brand}>
            <BuildingBank24Regular />
            <strong>ETC Intranet</strong>
          </div>
          <TabList
            selectedValue={selected}
            onTabSelect={(_, data) => {
              if (data.value === "opportunities") {
                navigate("/opportunities");
              } else if (data.value === "pipeline") {
                navigate("/pipeline");
              } else {
                navigate("/");
              }
            }}
          >
            <Tab value="home">Home</Tab>
            <Tab value="opportunities">Opportunities</Tab>
            <Tab value="pipeline">Pipeline</Tab>
          </TabList>
        </header>
        <Outlet />
      </div>
    </FluentProvider>
  );
}

const useStyles = makeStyles({
  shell: {
    minHeight: "100vh",
    backgroundColor: tokens.colorNeutralBackground3,
  },
  nav: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: "16px",
    flexWrap: "wrap",
    padding: "12px 20px",
    backgroundColor: tokens.colorNeutralBackground1,
    boxShadow: tokens.shadow2,
  },
  brand: {
    display: "flex",
    alignItems: "center",
    gap: "8px",
    color: tokens.colorBrandForeground1,
  },
});
