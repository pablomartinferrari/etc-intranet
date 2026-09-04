import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, Trash2, Users } from "lucide-react";
import { useEffect, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  createProjectShare,
  deleteProjectShare,
  listProjectShares,
  searchDirectory,
  type DirectoryPrincipal,
  type Project,
} from "./api/knowledge";

export function ShareProjectDialog({
  open,
  project,
  onClose,
}: {
  open: boolean;
  project?: Project;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [query, setQuery] = useState("");
  const [debounced, setDebounced] = useState("");
  const [role, setRole] = useState<"viewer" | "editor">("viewer");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(query.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [query]);

  useEffect(() => {
    if (!open) {
      setQuery("");
      setDebounced("");
      setRole("viewer");
      setError(null);
    }
  }, [open]);

  const sharesQuery = useQuery({
    queryKey: ["kb-shares", project?.id],
    queryFn: () => listProjectShares(project!.id),
    enabled: open && !!project,
  });

  const searchQuery = useQuery({
    queryKey: ["kb-directory", debounced],
    queryFn: ({ signal }) => searchDirectory(debounced, signal),
    enabled: open && debounced.length > 0,
  });

  const addMutation = useMutation({
    mutationFn: (principal: DirectoryPrincipal) =>
      createProjectShare(project!.id, {
        principalType: principal.type,
        principalOid: principal.oid,
        role,
        displayName: principal.displayName,
        email: principal.email ?? undefined,
      }),
    onSuccess: () => {
      setError(null);
      setQuery("");
      setDebounced("");
      void queryClient.invalidateQueries({ queryKey: ["kb-shares", project?.id] });
      void queryClient.invalidateQueries({ queryKey: ["kb-projects"] });
    },
    onError: (err: Error) => setError(err.message),
  });

  const removeMutation = useMutation({
    mutationFn: (shareId: string) => deleteProjectShare(project!.id, shareId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["kb-shares", project?.id] });
      void queryClient.invalidateQueries({ queryKey: ["kb-projects"] });
    },
    onError: (err: Error) => setError(err.message),
  });

  const existingOids = new Set((sharesQuery.data ?? []).map((s) => s.principalOid.toLowerCase()));
  const results = (searchQuery.data ?? []).filter(
    (p) => !existingOids.has(p.oid.toLowerCase()) && p.oid !== project?.id,
  );

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
    >
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Share {project?.name}</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">
            Owners can share this project with Entra users or groups. Editors can chat and upload;
            viewers can chat and read files.
          </p>
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
            <div className="min-w-0 flex-1 space-y-1.5">
              <Label htmlFor="share-search">Add people or groups</Label>
              <Input
                id="share-search"
                placeholder="Search by name or email"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Role</Label>
              <Select value={role} onValueChange={(value) => setRole(value as "viewer" | "editor")}>
                <SelectTrigger className="w-full sm:w-[120px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="viewer">Viewer</SelectItem>
                  <SelectItem value="editor">Editor</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          {searchQuery.isFetching && (
            <p className="flex items-center gap-2 text-xs text-muted-foreground">
              <Loader2 className="size-3.5 animate-spin" />
              Searching directory…
            </p>
          )}
          {searchQuery.isError && (
            <p className="text-xs text-destructive">
              {(searchQuery.error as Error).message || "Directory search is unavailable."}
            </p>
          )}
          {debounced && !searchQuery.isFetching && results.length === 0 && !searchQuery.isError && (
            <p className="text-xs text-muted-foreground">No matching users or groups.</p>
          )}
          {results.length > 0 && (
            <ul className="max-h-40 overflow-y-auto rounded-md border">
              {results.map((principal) => (
                <li key={`${principal.type}:${principal.oid}`}>
                  <button
                    type="button"
                    className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-muted"
                    disabled={addMutation.isPending}
                    onClick={() => addMutation.mutate(principal)}
                  >
                    <Users className="size-4 shrink-0 text-muted-foreground" />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-medium">{principal.displayName}</span>
                      <span className="block truncate text-xs text-muted-foreground">
                        {principal.email ?? principal.oid}
                      </span>
                    </span>
                    <Badge variant="outline">{principal.type}</Badge>
                  </button>
                </li>
              ))}
            </ul>
          )}
          <div className="space-y-2">
            <h4 className="text-sm font-medium">People with access</h4>
            {sharesQuery.isPending && (
              <p className="text-xs text-muted-foreground">Loading shares…</p>
            )}
            {(sharesQuery.data ?? []).length === 0 && !sharesQuery.isPending && (
              <p className="text-xs text-muted-foreground">Only you have access right now.</p>
            )}
            <ul className="flex flex-col gap-1">
              {(sharesQuery.data ?? []).map((share) => (
                <li
                  key={share.id}
                  className="flex items-center gap-2 rounded-md border bg-card px-3 py-2"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{share.principalDisplayName}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {share.principalEmail ?? share.principalType}
                    </p>
                  </div>
                  <Badge variant="secondary">{share.role}</Badge>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    title="Remove access"
                    disabled={removeMutation.isPending}
                    onClick={() => removeMutation.mutate(share.id)}
                  >
                    <Trash2 />
                  </Button>
                </li>
              ))}
            </ul>
          </div>
          {error && <p className="text-xs text-destructive">{error}</p>}
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={onClose}>
            Done
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
