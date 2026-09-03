import { BriefcaseIcon, Building2Icon, HomeIcon, InboxIcon, TrendingUpIcon } from "lucide-react";
import { Link as RouterLink } from "react-router-dom";

import { BrandBar, SignOutButton } from "@/components/brand-bar";
import { IntranetAppGrid, type IntranetApp } from "@/components/intranet-app-card";
import { Button } from "@/components/ui/button";
import { RequireAuth } from "../multifamily-lbp/auth/RequireAuth";
import { PageExplainer } from "./PageExplainer";

const SALES_APPS: IntranetApp[] = [
  {
    to: "/opportunities",
    title: "Bids",
    description: "Recommended government bids.",
    Icon: Building2Icon,
    accent: "border-violet-500",
  },
  {
    to: "/pipeline",
    title: "Pipeline",
    description: "Pursued, won, lost, close-out.",
    Icon: TrendingUpIcon,
    accent: "border-green-500",
  },
];

function SalesHubPage() {
  return (
    <div className="flex min-h-svh flex-col bg-muted/40">
      <BrandBar actions={<SignOutButton outlineOnBlack />} />
      <div className="flex flex-wrap items-center justify-between gap-4 border-b bg-background px-6 py-4">
        <div className="flex items-center gap-3">
          <BriefcaseIcon className="size-7" />
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Sales</h1>
            <p className="text-sm text-muted-foreground">Bids and the pursuit pipeline.</p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="outline" asChild>
            <RouterLink to="/requests">
              <InboxIcon />
              Requests
            </RouterLink>
          </Button>
          <Button variant="ghost" asChild>
            <RouterLink to="/">
              <HomeIcon />
              Applications
            </RouterLink>
          </Button>
        </div>
      </div>
      <main className="mx-auto flex w-full max-w-[960px] flex-1 flex-col gap-4 p-6">
        <PageExplainer title="Sales">
          <p>
            This is the sales home. Pick Bids (recommended government opportunities
            from CLEATUS) or Pipeline (deals ETC is pursuing, plus close-out).
            Request a change from Home so it can cover Chat, Lead, Sales, or General.
          </p>
        </PageExplainer>
        <IntranetAppGrid apps={SALES_APPS} />
      </main>
    </div>
  );
}

export function SalesHubRoute(): React.JSX.Element {
  return (
    <RequireAuth>
      <SalesHubPage />
    </RequireAuth>
  );
}
