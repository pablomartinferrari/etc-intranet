export function DataTablePanel({
  children,
  maxHeight = "min(70vh, 640px)",
}: {
  children: React.ReactNode;
  maxHeight?: string;
}): React.JSX.Element {
  return (
    <div
      className="overflow-auto rounded-md border bg-card shadow-sm"
      style={{ maxHeight }}
    >
      {children}
    </div>
  );
}

export const dataTableClass = {
  table: "min-w-full w-max",
  stickyHead: "sticky top-0 z-10 bg-muted shadow-[0_1px_0_var(--border)]",
  headCell: "whitespace-nowrap py-2 text-xs font-semibold text-muted-foreground",
  bodyCell: "align-middle py-1 text-sm",
  zebra: "even:bg-muted/60",
};

export function useDataTableStyles() {
  return dataTableClass;
}
