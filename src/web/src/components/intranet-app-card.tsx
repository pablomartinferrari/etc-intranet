import type { ComponentType } from "react";
import { ChevronRightIcon } from "lucide-react";
import { Link as RouterLink } from "react-router-dom";

import { cn } from "@/lib/utils";

export type IntranetApp = {
  to: string;
  title: string;
  description: string;
  Icon: ComponentType<{ className?: string }>;
  accent: string;
};

export function IntranetAppCard({ to, title, description, Icon, accent }: IntranetApp) {
  return (
    <RouterLink
      to={to}
      className={cn(
        "flex items-center gap-4 rounded-xl border bg-card p-5 text-inherit no-underline shadow-sm",
        "transition duration-150 ease-out hover:-translate-y-0.5 hover:border-border hover:shadow-md",
        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
      )}
    >
      <div
        className={cn(
          "flex size-12 shrink-0 items-center justify-center rounded-lg border bg-muted",
          accent,
        )}
      >
        <Icon className="size-6" />
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-1.5">
        <p className="text-base leading-tight font-semibold">{title}</p>
        <p className="text-xs leading-snug text-muted-foreground">{description}</p>
      </div>
      <ChevronRightIcon className="size-5 shrink-0 text-muted-foreground" aria-hidden />
    </RouterLink>
  );
}

export function IntranetAppGrid({ apps }: { apps: IntranetApp[] }) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      {apps.map((app) => (
        <IntranetAppCard key={app.to} {...app} />
      ))}
    </div>
  );
}
