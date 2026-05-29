import { makeStyles, tokens } from "@fluentui/react-components";

const useStyles = makeStyles({
  panel: {
    overflow: "auto",
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
    boxShadow: tokens.shadow2,
  },
  table: {
    minWidth: "100%",
    width: "max-content",
  },
  stickyHead: {
    position: "sticky",
    top: 0,
    zIndex: 1,
    backgroundColor: tokens.colorNeutralBackground2,
    boxShadow: `0 1px 0 ${tokens.colorNeutralStroke2}`,
  },
  headCell: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
    whiteSpace: "nowrap",
    paddingTop: tokens.spacingVerticalS,
    paddingBottom: tokens.spacingVerticalS,
  },
  bodyCell: {
    verticalAlign: "middle",
    fontSize: tokens.fontSizeBase300,
    paddingTop: tokens.spacingVerticalXS,
    paddingBottom: tokens.spacingVerticalXS,
  },
  zebra: {
    "&:nth-child(even)": {
      backgroundColor: tokens.colorNeutralBackground2,
    },
  },
});

export function useDataTableStyles() {
  return useStyles();
}

export function DataTablePanel({
  children,
  maxHeight = "min(70vh, 640px)",
}: {
  children: React.ReactNode;
  maxHeight?: string;
}): React.JSX.Element {
  const styles = useStyles();
  return (
    <div className={styles.panel} style={{ maxHeight }}>
      {children}
    </div>
  );
}
