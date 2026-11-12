import { FileSpreadsheetIcon, HomeIcon } from "lucide-react";
import { Link as RouterLink } from "react-router-dom";

import { BrandBar } from "@/components/brand-bar";
import { Button } from "@/components/ui/button";

export function LeadInspectionChrome({
  children,
}: {
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <div className="flex min-h-svh flex-col bg-muted/40">
      <header>
        <BrandBar />
      </header>
      <div className="flex flex-wrap items-center justify-between gap-4 border-b bg-background px-4 py-3 md:px-6 md:py-4">
        <div className="flex min-w-0 items-center gap-3">
          <FileSpreadsheetIcon className="size-7 shrink-0" />
          <div className="min-w-0">
            <h1 className="text-xl font-semibold tracking-tight md:text-2xl">Lead Inspection Data Manager</h1>
            <p className="text-sm text-muted-foreground">
              Multifamily LBP — SharePoint import, grid, normalization, and reports
            </p>
          </div>
        </div>
        <Button variant="ghost" asChild>
          <RouterLink to="/lead-inspection">
            <HomeIcon />
            Job lookup
          </RouterLink>
        </Button>
      </div>
      <main className="mx-auto w-full max-w-[1200px] flex-1 p-4 pb-24 md:p-6">{children}</main>
      <footer className="p-4 text-center text-xs text-muted-foreground">
        ETC intranet · Lead inspection workspace
      </footer>
    </div>
  );
}
