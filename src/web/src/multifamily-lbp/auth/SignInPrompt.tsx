import {
  Body1,
  Button,
  Card,
  MessageBar,
  MessageBarBody,
  Subtitle1,
  Text,
  Title2,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { ShieldLockRegular } from "@fluentui/react-icons";
import { useMsal } from "@azure/msal-react";
import { useParams } from "react-router-dom";
import { apiRequest, signInRequest } from "../../authConfig";

const useStyles = makeStyles({
  wrap: {
    display: "flex",
    justifyContent: "center",
    paddingTop: tokens.spacingVerticalXXL,
    paddingBottom: tokens.spacingVerticalXXL,
  },
  card: {
    width: "100%",
    maxWidth: "560px",
    padding: tokens.spacingHorizontalXL,
    display: "grid",
    rowGap: tokens.spacingVerticalL,
  },
  iconRow: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalM,
    color: tokens.colorBrandForeground1,
  },
  actions: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalM,
    alignItems: "flex-start",
  },
  jobBadge: {
    display: "inline-block",
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground3,
    fontWeight: tokens.fontWeightSemibold,
  },
});

export interface SignInPromptProps {
  /** Extra detail below the main heading. */
  message?: string;
  /** Override job number from the route (e.g. SharePoint deep link). */
  jobId?: string;
}

/**
 * Friendly gate before any intranet API calls — used for SharePoint deep links and job routes.
 */
export function SignInPrompt({ message, jobId: jobIdProp }: SignInPromptProps): React.JSX.Element {
  const styles = useStyles();
  const { instance } = useMsal();
  const { jobId: jobIdParam } = useParams<{ jobId?: string }>();
  const missingApiScope = apiRequest.scopes.length === 0;
  const jobId = jobIdProp ?? jobIdParam;

  const detail =
    message ??
    (jobId
      ? "Your SharePoint upload is already saved. Sign in to import readings, review the data grid, and generate reports."
      : "Sign in with your Microsoft work account to open the lead inspection workspace.");

  return (
    <div className={styles.wrap}>
      <Card className={styles.card}>
        <div className={styles.iconRow}>
          <ShieldLockRegular fontSize={32} />
          <Title2 block>Please sign in to continue</Title2>
        </div>

        {jobId && (
          <div>
            <Subtitle1 block style={{ marginBottom: tokens.spacingVerticalS }}>
              Continuing from SharePoint
            </Subtitle1>
            <span className={styles.jobBadge}>Job {jobId}</span>
          </div>
        )}

        <Body1>{detail}</Body1>

        {missingApiScope && (
          <MessageBar intent="warning">
            <MessageBarBody>
              This build is missing <code>VITE_API_SCOPE</code>. Redeploy the intranet web app with Entra
              settings configured.
            </MessageBarBody>
          </MessageBar>
        )}

        <div className={styles.actions}>
          <Button
            appearance="primary"
            size="large"
            disabled={missingApiScope}
            onClick={() => {
              const returnPath = `${window.location.pathname}${window.location.search}`;
              sessionStorage.setItem("mf-post-login-nav", returnPath);
              void instance.loginRedirect(signInRequest);
            }}
          >
            Sign in with Microsoft
          </Button>
          <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
            Use the same work account you use for SharePoint and Microsoft 365.
          </Text>
        </div>
      </Card>
    </div>
  );
}
