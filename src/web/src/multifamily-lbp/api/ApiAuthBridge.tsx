import { useMsal } from "@azure/msal-react";
import { useEffect } from "react";
import { apiRequest } from "../../authConfig";
import { setApiAuthHeadersProvider } from "@mf/api/client";

/** Wires MSAL tokens into multifamily API fetch helpers. */
export function ApiAuthBridge({ children }: { children: React.ReactNode }) {
  const { instance, accounts } = useMsal();

  useEffect(() => {
    if (accounts.length === 0) {
      setApiAuthHeadersProvider(null);
      return;
    }

    const account = accounts[0];
    setApiAuthHeadersProvider(async () => {
      const tokenResponse = await instance.acquireTokenSilent({
        ...apiRequest,
        account,
      });
      return { Authorization: `Bearer ${tokenResponse.accessToken}` };
    });

    return () => setApiAuthHeadersProvider(null);
  }, [accounts, instance]);

  return <>{children}</>;
}
