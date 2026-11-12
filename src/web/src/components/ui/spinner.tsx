import { Loader2Icon } from "lucide-react";

import { cn } from "@/lib/utils";

export function Spinner({
  className,
  label,
  size = "default",
}: {
  className?: string;
  label?: string;
  size?: "default" | "sm" | "lg";
}) {
  const iconSize = size === "lg" ? "size-8" : size === "sm" ? "size-3.5" : "size-4";

  return (
    <div
      className={cn("flex items-center gap-2 text-sm text-muted-foreground", className)}
      role="status"
    >
      <Loader2Icon className={cn(iconSize, "animate-spin")} />
      {label ? <span>{label}</span> : <span className="sr-only">Loading</span>}
    </div>
  );
}
