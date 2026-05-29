import { InteractionStatus } from "@azure/msal-browser";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { Navigate, useLocation } from "react-router-dom";
import { Spinner } from "@fluentui/react-components";
import {
  isMultifamilyJobEntryPath,
  savePostLoginReturnPath,
} from "./jobEntryPaths";

export interface RequireAuthProps {
  children: React.ReactNode;
}

/** Ensures multifamily job routes are only used when signed in. */
export function RequireAuth({ children }: RequireAuthProps): React.JSX.Element {
  const { pathname, search } = useLocation();
  const isAuthenticated = useIsAuthenticated();
  const { inProgress } = useMsal();

  if (inProgress !== InteractionStatus.None) {
    return <Spinner size="large" label="Signing you in…" />;
  }

  if (!isAuthenticated) {
    if (isMultifamilyJobEntryPath(pathname)) {
      savePostLoginReturnPath(pathname, search);
    }
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
