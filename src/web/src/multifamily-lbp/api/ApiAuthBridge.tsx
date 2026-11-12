import { useMsal } from "@azure/msal-react";
import { useMemo } from "react";
import { apiRequest } from "../../authConfig";
import { setApiAuthHeadersProvider } from "@mf/api/client";

/** Wires MSAL tokens into multifamily API fetch helpers. */
export function ApiAuthBridge({ children }: { children: React.ReactNode }) {
  const { instance, accounts } = useMsal();

  // Set provider during render (before child useEffects) so deep links from
  // SharePoint do not call /api/* without Authorization on first paint.
  useMemo(() => {
    if (accounts.length === 0) {
      setApiAuthHeadersProvider(null);
      return null;
    }

    const account = accounts[0];
    setApiAuthHeadersProvider(async () => {
      const tokenResponse = await instance.acquireTokenSilent({
        ...apiRequest,
        account,
      });
      return { Authorization: `Bearer ${tokenResponse.accessToken}` };
    });
    return null;
  }, [accounts, instance]);

  return <>{children}</>;
}
