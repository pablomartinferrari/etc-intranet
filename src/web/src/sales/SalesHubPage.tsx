import { BriefcaseIcon, Building2Icon, HomeIcon, TrendingUpIcon } from "lucide-react";
import { Link as RouterLink } from "react-router-dom";

import { BrandBar, SignOutButton } from "@/components/brand-bar";
import { IntranetAppGrid, type IntranetApp } from "@/components/intranet-app-card";
import { Button } from "@/components/ui/button";
import { RequireAuth } from "../multifamily-lbp/auth/RequireAuth";

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
        <Button variant="ghost" asChild>
          <RouterLink to="/">
            <HomeIcon />
            Applications
          </RouterLink>
        </Button>
      </div>
      <main className="mx-auto w-full max-w-[960px] flex-1 p-6">
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
