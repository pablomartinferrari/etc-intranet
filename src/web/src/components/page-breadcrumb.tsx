import { Link as RouterLink } from "react-router-dom";

import { cn } from "@/lib/utils";

export type BreadcrumbItem = {
  label: string;
  to?: string;
};

export function PageBreadcrumb({ items }: { items: BreadcrumbItem[] }) {
  return (
    <nav aria-label="Breadcrumb">
      <ol className="flex flex-wrap items-center gap-1.5 text-sm">
        {items.map((item, index) => {
          const isLast = index === items.length - 1;
          return (
            <li key={`${item.label}-${index}`} className="flex items-center gap-1.5">
              {index > 0 && (
                <span className="text-muted-foreground" aria-hidden="true">
                  /
                </span>
              )}
              {item.to && !isLast ? (
                <RouterLink
                  to={item.to}
                  className={cn(
                    "rounded-sm text-muted-foreground underline-offset-4 transition-colors",
                    "hover:text-foreground hover:underline",
                    "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
                  )}
                >
                  {item.label}
                </RouterLink>
              ) : (
                <span
                  className="font-medium text-foreground"
                  aria-current={isLast ? "page" : undefined}
                >
                  {item.label}
                </span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
