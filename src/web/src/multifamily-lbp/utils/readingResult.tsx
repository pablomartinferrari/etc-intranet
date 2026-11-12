import { Badge } from "@/components/ui/badge";

export const LEAD_POSITIVE_THRESHOLD = 1.0;

export function isReadingPositive(row: { leadContent: number; isPositive?: boolean }): boolean {
  return row.leadContent >= LEAD_POSITIVE_THRESHOLD || row.isPositive === true;
}

export function readingResultLabel(row: { leadContent: number; isPositive?: boolean }): "Positive" | "Negative" {
  return isReadingPositive(row) ? "Positive" : "Negative";
}

export function ReadingResultBadge({
  row,
}: {
  row: { leadContent: number; isPositive?: boolean };
}): React.JSX.Element {
  const positive = isReadingPositive(row);
  return (
    <Badge variant={positive ? "destructive" : "secondary"}>
      {positive ? "Positive" : "Negative"}
    </Badge>
  );
}
