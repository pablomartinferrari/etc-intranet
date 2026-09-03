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
        "flex flex-wrap items-center justify-between gap-3 bg-black px-4 py-3 md:gap-4 md:px-6 md:py-4",
        rounded && "rounded-xl shadow-lg",
      )}
    >
      <RouterLink to="/" className="leading-none">
        <img
          alt="Environmental Testing & Consulting"
          className="block h-9 w-auto max-w-[min(100%,240px)] object-contain md:h-12 md:max-w-[min(100%,300px)]"
          src={etcLogo}
        />
      </RouterLink>
      {actions ? (
        <div className="flex min-w-0 flex-wrap items-center justify-end gap-2">{actions}</div>
      ) : null}
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
