import { FileTextIcon, GlobeIcon, TableIcon } from "lucide-react";

export type FileKind = "pdf" | "excel" | "word" | "text" | "web" | "generic";

export function fileKindFromName(filename: string): FileKind {
  const ext = filename.includes(".")
    ? filename.slice(filename.lastIndexOf(".") + 1).toLowerCase()
    : "";

  if (ext === "pdf") return "pdf";
  if (ext === "xlsx" || ext === "xls" || ext === "csv") return "excel";
  if (ext === "docx" || ext === "doc") return "word";
  if (ext === "txt" || ext === "md" || ext === "html" || ext === "htm") return "text";
  return "generic";
}

export function fileKindFromFormat(format?: string): FileKind {
  if (format === "xlsx") return "excel";
  if (format === "docx") return "word";
  return "generic";
}

export function FileTypeIcon({
  kind,
  className,
}: {
  kind: FileKind;
  className?: string;
}) {
  switch (kind) {
    case "pdf":
      return <FileTextIcon className={className} />;
    case "excel":
      return <TableIcon className={className} />;
    case "word":
      return <FileTextIcon className={className} />;
    case "web":
      return <GlobeIcon className={className} />;
    default:
      return <FileTextIcon className={className} />;
  }
}
