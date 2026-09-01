import { useMsal } from "@azure/msal-react";
import { Link as RouterLink } from "react-router-dom";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import etcLogo from "@/images/etc-logo.png";

export function BrandBar({
  rounded = false,
  actions,
}: {
  rounded?: boolean;
  actions?: React.ReactNode;
}) {
  return (
    <div
      className={cn(
        "flex flex-wrap items-center justify-between gap-4 bg-black px-6 py-4",
        rounded && "rounded-xl shadow-lg",
      )}
    >
      <RouterLink to="/" className="leading-none">
        <img
          alt="Environmental Testing & Consulting"
          className="block h-12 w-auto max-w-[min(100%,300px)] object-contain"
          src={etcLogo}
        />
      </RouterLink>
      {actions ? <div className="flex shrink-0 items-center">{actions}</div> : null}
    </div>
  );
}

export function SignOutButton({ outlineOnBlack = false }: { outlineOnBlack?: boolean }) {
  const { instance } = useMsal();

  return (
    <Button
      type="button"
      variant="outline"
      className={
        outlineOnBlack
          ? "border-white bg-black text-white hover:bg-white hover:text-black"
          : undefined
      }
      onClick={() => void instance.logoutRedirect()}
    >
      Sign out
    </Button>
  );
}
