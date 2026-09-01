import { HomeIcon } from "lucide-react";
import { Link as RouterLink } from "react-router-dom";

import { BrandBar, SignOutButton } from "@/components/brand-bar";
import { Button } from "@/components/ui/button";

export function CleatusChrome({
  title,
  subtitle,
  icon,
  children,
}: {
  title: string;
  subtitle: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <div className="flex min-h-svh flex-col bg-muted/40">
      <BrandBar actions={<SignOutButton outlineOnBlack />} />
      <div className="flex flex-wrap items-center justify-between gap-4 border-b bg-background px-6 py-4">
        <div className="flex items-center gap-3">
          <span className="text-foreground">{icon}</span>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
            <p className="text-sm text-muted-foreground">{subtitle}</p>
          </div>
        </div>
        <Button variant="ghost" asChild>
          <RouterLink to="/">
            <HomeIcon />
            Applications
          </RouterLink>
        </Button>
      </div>
      {children}
    </div>
  );
}
