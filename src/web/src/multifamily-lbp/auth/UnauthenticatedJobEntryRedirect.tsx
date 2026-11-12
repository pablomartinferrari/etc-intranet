import { InteractionStatus } from "@azure/msal-browser";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { Navigate, useLocation } from "react-router-dom";
import {
  isMultifamilyJobEntryPath,
  savePostLoginReturnPath,
} from "./jobEntryPaths";

/**
 * SharePoint opens /jobs/{id}/multifamily-lbp?import=1 directly.
 * Send unauthenticated users to the intranet home (ETC header + sign in),
 * then return them here after login.
 */
export function UnauthenticatedJobEntryRedirect(): React.JSX.Element | null {
  const { pathname, search } = useLocation();
  const isAuthenticated = useIsAuthenticated();
  const { inProgress } = useMsal();

  if (inProgress !== InteractionStatus.None) {
    return null;
  }

  if (isAuthenticated || !isMultifamilyJobEntryPath(pathname)) {
    return null;
  }

  savePostLoginReturnPath(pathname, search);
  return <Navigate to="/" replace />;
}
