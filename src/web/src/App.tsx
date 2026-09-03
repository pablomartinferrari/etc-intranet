import { useEffect, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { BrowserRouter, Route, Routes, useNavigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BriefcaseIcon, ClipboardListIcon, SparklesIcon } from "lucide-react";

import { apiRequest, signInRequest } from "./authConfig";
import MultifamilyRoutes from "./multifamily-lbp/MultifamilyRoutes";
import KnowledgeRoutes from "./knowledge-base/KnowledgeRoutes";
import {
  CleatusOpportunitiesRoute,
  CleatusPipelineRoute,
} from "./cleatus/CleatusRoutes";
import { FeatureRequestsRoute } from "./sales/FeatureRequestsPage";
import { SalesHubRoute } from "./sales/SalesHubPage";
import { ApiAuthBridge } from "./multifamily-lbp/api/ApiAuthBridge";
import {
  parseJobIdFromReturnPath,
  readPostLoginReturnPath,
  POST_LOGIN_NAV_KEY,
} from "./multifamily-lbp/auth/jobEntryPaths";
import { BrandBar, SignOutButton } from "@/components/brand-bar";
import { IntranetAppGrid, type IntranetApp } from "@/components/intranet-app-card";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { TooltipProvider } from "@/components/ui/tooltip";

type MeResponse = {
  name: string | null;
  email: string | null;
  objectId: string | null;
  tenantId: string | null;
};

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (failureCount, error) =>
        failureCount < 1 && !(error instanceof Error && error.name === "AuthRequiredError"),
    },
  },
});

/** After Entra redirect, return user to a multifamily deep link saved before login. */
function PostLoginRedirect(): null {
  const navigate = useNavigate();
  const { accounts } = useMsal();

  useEffect(() => {
    if (accounts.length === 0) return;
    const target = readPostLoginReturnPath();
    if (target) {
      sessionStorage.removeItem(POST_LOGIN_NAV_KEY);
      navigate(target, { replace: true });
    }
  }, [accounts.length, navigate]);

  return null;
}

const INTRANET_APPS: IntranetApp[] = [
  {
    to: "/knowledge",
    title: "Chat",
    description: "ChatGPT for every ETC employee.",
    Icon: SparklesIcon,
    accent: "border-teal-500",
  },
  {
    to: "/lead-inspection",
    title: "Lead",
    description: "XRF readings, grids, and reports.",
    Icon: ClipboardListIcon,
    accent: "border-blue-500",
  },
  {
    to: "/sales",
    title: "Sales",
    description: "Bids and the pursuit pipeline.",
    Icon: BriefcaseIcon,
    accent: "border-violet-500",
  },
];

function IntranetHome() {
  const { instance, accounts } = useMsal();
  const [me, setMe] = useState<MeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const isSignedIn = accounts.length > 0;
  const account = accounts[0];
  const pendingReturnPath = readPostLoginReturnPath();
  const pendingJobId = parseJobIdFromReturnPath(pendingReturnPath);
  const displayName =
    me?.name && !me.name.includes("@")
      ? me.name
      : (account?.name ?? me?.name ?? "there");
  const firstName = displayName.split(/\s+/)[0] ?? displayName;
  const displayEmail = me?.email ?? account?.username ?? null;

  async function loadData() {
    if (!isSignedIn) {
      setMe(null);
      return;
    }

    setError(null);

    try {
      const signedInAccount = accounts[0];
      const tokenResponse = await instance.acquireTokenSilent({
        ...apiRequest,
        account: signedInAccount,
      });

      const authHeaders = {
        Authorization: `Bearer ${tokenResponse.accessToken}`,
      };

      const meRes = await fetch("/api/me", { headers: authHeaders });

      if (!meRes.ok) {
        throw new Error("API request failed");
      }

      setMe(await meRes.json());
    } catch {
      setError(
        "Could not authenticate with the API. Check Entra app registrations and API scope configuration.",
      );
    }
  }

  useEffect(() => {
    void loadData();
  }, [isSignedIn]);

  return (
    <main className="mx-auto flex min-h-svh max-w-[960px] flex-col gap-8 bg-muted/40 px-6 py-7 pb-16">
      <header className="flex flex-col gap-7">
        <BrandBar
          rounded
          actions={
            !isSignedIn ? (
              <Button
                onClick={() => {
                  if (pendingReturnPath) {
                    sessionStorage.setItem(POST_LOGIN_NAV_KEY, pendingReturnPath);
                  }
                  void instance.loginRedirect(signInRequest);
                }}
              >
                Sign in with Microsoft
              </Button>
            ) : (
              <SignOutButton outlineOnBlack />
            )
          }
        />

        {!isSignedIn ? (
          <div className="flex flex-col gap-2 px-1 pt-2">
            <h1 className="text-3xl font-semibold tracking-tight">ETC intranet</h1>
            <p className="max-w-xl text-base text-muted-foreground">
              {pendingJobId
                ? `Sign in to continue to job ${pendingJobId} in the lead inspection workspace.`
                : "Sign in with your Microsoft work account to open company applications."}
            </p>
          </div>
        ) : (
          <div className="flex flex-col gap-2 px-1 pt-2">
            <p className="text-xs font-semibold tracking-[0.06em] text-muted-foreground uppercase">
              Environmental Testing & Consulting
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">Welcome back, {firstName}</h1>
            {displayEmail && (
              <p className="text-xs text-muted-foreground">{displayEmail}</p>
            )}
            {error && <p className="mt-2 text-sm text-destructive">{error}</p>}
          </div>
        )}
      </header>

      {!isSignedIn && pendingJobId && (
        <Card className="rounded-xl shadow-sm">
          <CardHeader>
            <CardTitle>Lead inspection workspace</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-muted-foreground">
              After you sign in, you will return to job <strong>{pendingJobId}</strong> to import
              SharePoint files, review readings, and generate reports.
            </p>
          </CardContent>
        </Card>
      )}

      {isSignedIn && (
        <section className="flex flex-col gap-4">
          <h2 className="px-1 text-base font-semibold text-muted-foreground">Applications</h2>
          <IntranetAppGrid apps={INTRANET_APPS} />
        </section>
      )}
    </main>
  );
}

export default function App() {
  return (
    <TooltipProvider>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <ApiAuthBridge>
            <PostLoginRedirect />
            <Routes>
              <Route path="/" element={<IntranetHome />} />
              <Route path="/knowledge/*" element={<KnowledgeRoutes />} />
              <Route path="/sales" element={<SalesHubRoute />} />
              <Route path="/sales/requests" element={<FeatureRequestsRoute />} />
              <Route path="/opportunities" element={<CleatusOpportunitiesRoute />} />
              <Route path="/pipeline" element={<CleatusPipelineRoute />} />
              <Route path="/*" element={<MultifamilyRoutes />} />
            </Routes>
          </ApiAuthBridge>
        </BrowserRouter>
      </QueryClientProvider>
    </TooltipProvider>
  );
}
