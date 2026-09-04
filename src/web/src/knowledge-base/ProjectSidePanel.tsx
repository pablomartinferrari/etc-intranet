import { Lightbulb, Plus, Trash2, Upload } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Spinner } from "@/components/ui/spinner";
import { AddSharePointFolderButton } from "./AddSharePointFolderSheet";
import { FileTypeIcon, fileKindFromName } from "./fileTypeIcon";
import {
  canEditProject,
  type Project,
  type Prompt,
} from "./api/knowledge";

export type ProjectDocumentRow = {
  id: string;
  title: string;
  ingestStatus: string;
  ingestDetail?: string | null;
  sourceType?: string | null;
};

export function ProjectFilesPanel({
  selectedProject,
  onPickFiles,
  onDropFiles,
  hasActiveIngest,
  documents,
  documentsLoading,
  contextDocId,
  onSelectDocument,
}: {
  selectedProject?: Project;
  onPickFiles: () => void;
  onDropFiles: (files: FileList) => void;
  hasActiveIngest: boolean;
  documents: ProjectDocumentRow[];
  documentsLoading?: boolean;
  contextDocId?: string;
  onSelectDocument: (id: string) => void;
}) {
  const canEdit = canEditProject(selectedProject);

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-2.5">
      {canEdit ? (
        <div
          className="flex min-h-11 cursor-pointer flex-col items-center gap-1.5 rounded-md border-2 border-dashed bg-card px-3 py-5 text-center hover:border-primary"
          onDragOver={(e) => e.preventDefault()}
          onDrop={(e) => {
            e.preventDefault();
            onDropFiles(e.dataTransfer.files);
          }}
          onClick={onPickFiles}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              onPickFiles();
            }
          }}
          role="button"
          tabIndex={0}
        >
          <Upload />
          <p className="text-sm">Add project files</p>
          <span className="text-xs text-muted-foreground">
            PDF, Word, Excel, text — indexes in the background
          </span>
        </div>
      ) : (
        <p className="rounded-md border bg-card px-3 py-2 text-xs text-muted-foreground">
          You can read project files. Ask the owner if you need to upload.
        </p>
      )}
      {canEdit && (
        <AddSharePointFolderButton variant="outline" size="sm" className="w-full">
          Add SharePoint folder
        </AddSharePointFolderButton>
      )}
      {hasActiveIngest && (
        <div className="flex items-center gap-2 rounded-md bg-muted px-2.5 py-2">
          <Spinner size="sm" label="Indexing… ready files are searchable now" />
        </div>
      )}
      <div className="flex min-h-0 flex-1 flex-col gap-1 overflow-y-auto">
        <p className="px-1 text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
          Project files
        </p>
        {documentsLoading && (
          <div className="flex flex-col gap-2 px-1 py-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-4/5" />
          </div>
        )}
        {!documentsLoading && documents.length === 0 && (
          <span className="px-2 py-3 text-center text-xs text-muted-foreground">
            No project files yet. Upload a document or add a SharePoint folder.
          </span>
        )}
        {!documentsLoading &&
          documents.map((doc) => (
            <DocumentRow
              key={doc.id}
              title={doc.title}
              status={doc.ingestStatus}
              detail={doc.ingestDetail}
              sourceType={doc.sourceType}
              selected={contextDocId === doc.id}
              onSelect={() => onSelectDocument(doc.id)}
            />
          ))}
      </div>
    </div>
  );
}

export function ProjectPromptsPanel({
  selectedProject,
  onSavePrompt,
  prompts,
  promptsLoading,
  onUsePrompt,
  onDeletePrompt,
}: {
  selectedProject?: Project;
  onSavePrompt: () => void;
  prompts: Prompt[];
  promptsLoading?: boolean;
  onUsePrompt: (prompt: Prompt) => void;
  onDeletePrompt: (id: string) => void;
}) {
  const canEdit = canEditProject(selectedProject);

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-2.5">
      {canEdit ? (
        <Button variant="secondary" className="h-11 w-full md:h-8" onClick={onSavePrompt}>
          <Plus />
          Save prompt
        </Button>
      ) : (
        <p className="text-xs text-muted-foreground">Saved questions for this project.</p>
      )}
      <div className="flex min-h-0 flex-1 flex-col gap-1 overflow-y-auto">
        {promptsLoading && (
          <div className="flex flex-col gap-2 px-1 py-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-3/4" />
          </div>
        )}
        {!promptsLoading && prompts.length === 0 && (
          <span className="px-2 py-3 text-center text-xs text-muted-foreground">
            Save reusable questions so the team can start chats faster.
          </span>
        )}
        {!promptsLoading &&
          prompts.map((p) => (
            <PromptItem
              key={p.id}
              prompt={p}
              canDelete={canEdit}
              onUse={() => onUsePrompt(p)}
              onDelete={() => onDeletePrompt(p.id)}
            />
          ))}
      </div>
    </div>
  );
}

function PromptItem({
  prompt,
  canDelete,
  onUse,
  onDelete,
}: {
  prompt: Prompt;
  canDelete: boolean;
  onUse: () => void;
  onDelete: () => void;
}) {
  return (
    <div className="flex items-center gap-1">
      <button
        type="button"
        className="flex min-h-11 w-full cursor-pointer items-center gap-2.5 rounded-md border-0 bg-transparent px-3 py-2.5 text-left text-sm hover:bg-muted"
        onClick={onUse}
        title={prompt.content}
      >
        <Lightbulb className="size-4 shrink-0" />
        <span className="flex-1 truncate">{prompt.title}</span>
      </button>
      {canDelete && (
        <Button variant="ghost" size="icon-sm" onClick={onDelete}>
          <Trash2 />
        </Button>
      )}
    </div>
  );
}

function sourceLabel(sourceType?: string | null): string | null {
  if (!sourceType) return null;
  const value = sourceType.toLowerCase();
  if (value === "upload") return "Upload";
  if (value === "sharepoint" || value === "agent") return "SharePoint";
  return sourceType;
}

function DocumentRow({
  title,
  status,
  detail,
  sourceType,
  selected,
  onSelect,
}: {
  title: string;
  status: string;
  detail?: string | null;
  sourceType?: string | null;
  selected: boolean;
  onSelect: () => void;
}) {
  const statusLabel =
    detail && status !== "completed" && status !== "failed" ? detail : status;
  const badgeVariant =
    status === "failed" ? "destructive" : status === "processing" ? "secondary" : "outline";
  const badgeClass =
    status === "completed"
      ? "border-green-600/40 text-green-700 dark:text-green-400"
      : status === "failed"
        ? ""
        : status === "processing"
          ? ""
          : "border-amber-500/40 text-amber-700 dark:text-amber-400";
  const source = sourceLabel(sourceType);

  return (
    <button
      type="button"
      className={`flex min-h-11 w-full cursor-pointer items-center gap-2.5 rounded-md border-0 bg-transparent px-3 py-2.5 text-left text-sm hover:bg-muted ${
        selected ? "bg-muted outline outline-1 outline-primary" : ""
      }`}
      onClick={onSelect}
      title={detail ? `${status}: ${detail}` : selected ? "Chat uses this file only" : "Focus chat on this file"}
    >
      <FileTypeIcon kind={fileKindFromName(title)} className="size-5 shrink-0 text-muted-foreground" />
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="truncate">{title}</span>
        {source && <span className="text-[10px] text-muted-foreground">{source}</span>}
      </span>
      <Badge variant={badgeVariant} className={badgeClass}>
        {statusLabel}
      </Badge>
    </button>
  );
}
