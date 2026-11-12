import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, Users } from "lucide-react";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { workspaceApi, type DirectoryPrincipal, type WorkspaceChat, type WorkspaceRole } from "./api";

export function ShareChatDialog({
  open,
  chat,
  onClose,
}: {
  open: boolean;
  chat?: WorkspaceChat;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [query, setQuery] = useState("");
  const [debounced, setDebounced] = useState("");
  const [role, setRole] = useState<WorkspaceRole>("editor");
  const [realtime, setRealtime] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(query.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [query]);

  useEffect(() => {
    if (!open) {
      setQuery("");
      setDebounced("");
      setError(null);
      return;
    }
    setRealtime(!!chat?.realtimeCollaboration);
  }, [open, chat?.realtimeCollaboration]);

  const sharesQuery = useQuery({
    queryKey: ["ws-shares", chat?.id],
    queryFn: () => workspaceApi.shares(chat!.id),
    enabled: open && !!chat,
  });

  const searchQuery = useQuery({
    queryKey: ["ws-directory", debounced],
    queryFn: () => workspaceApi.directory(debounced),
    enabled: open && debounced.length > 0,
  });

  const addMutation = useMutation({
    mutationFn: (principal: DirectoryPrincipal) =>
      workspaceApi.addShare(chat!.id, {
        principalType: principal.type,
        principalOid: principal.oid,
        role,
        displayName: principal.displayName,
        email: principal.email ?? undefined,
      }),
    onSuccess: () => {
      setQuery("");
      setDebounced("");
      setError(null);
      void queryClient.invalidateQueries({ queryKey: ["ws-shares", chat?.id] });
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      void queryClient.invalidateQueries({ queryKey: ["ws-chat", chat?.id] });
    },
    onError: (err: Error) => setError(err.message),
  });

  const removeMutation = useMutation({
    mutationFn: (shareId: string) => workspaceApi.removeShare(chat!.id, shareId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["ws-shares", chat?.id] });
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
    },
  });

  const realtimeMutation = useMutation({
    mutationFn: (next: boolean) => workspaceApi.updateChat(chat!.id, { realtimeCollaboration: next }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["ws-chat", chat?.id] });
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
    },
  });

  const existing = new Set((sharesQuery.data ?? []).map((s) => s.principalOid.toLowerCase()));
  const results = (searchQuery.data ?? []).filter((p) => !existing.has(p.oid.toLowerCase()));

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Share chat</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-4">
          <div className="flex gap-2">
            <Input
              placeholder="Add people or groups"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
            <Select value={role} onValueChange={(value) => setRole(value as WorkspaceRole)}>
              <SelectTrigger className="w-[130px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="editor">Can edit</SelectItem>
                <SelectItem value="viewer">Can view</SelectItem>
              </SelectContent>
            </Select>
          </div>
          {searchQuery.isFetching ? (
            <p className="flex items-center gap-2 text-xs text-muted-foreground">
              <Loader2 className="size-3.5 animate-spin" />
              Searching directory…
            </p>
          ) : null}
          {results.length > 0 ? (
            <ul className="max-h-40 overflow-y-auto rounded-md border">
              {results.map((principal) => (
                <li key={`${principal.type}:${principal.oid}`}>
                  <button
                    type="button"
                    className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-muted"
                    onClick={() => addMutation.mutate(principal)}
                  >
                    <Users className="size-4 text-muted-foreground" />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-medium">{principal.displayName}</span>
                      <span className="block truncate text-xs text-muted-foreground">
                        {principal.email ?? (principal.memberCount ? `${principal.memberCount} members` : principal.type)}
                      </span>
                    </span>
                    <Badge variant="outline">{principal.type}</Badge>
                  </button>
                </li>
              ))}
            </ul>
          ) : null}
          <div>
            <p className="mb-2 text-sm font-medium">People with access</p>
            <ul className="flex flex-col gap-1">
              <li className="flex items-center justify-between rounded-md px-1 py-1.5 text-sm">
                <span>You</span>
                <span className="text-muted-foreground">Owner</span>
              </li>
              {(sharesQuery.data ?? []).map((share) => (
                <li key={share.id} className="flex items-center gap-2 rounded-md px-1 py-1.5 text-sm">
                  <span className="min-w-0 flex-1 truncate">{share.principalDisplayName}</span>
                  <Select
                    value={share.role}
                    onValueChange={() => undefined}
                  >
                    <SelectTrigger className="h-8 w-[120px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="editor">Can edit</SelectItem>
                      <SelectItem value="viewer">Can view</SelectItem>
                    </SelectContent>
                  </Select>
                  <Button variant="ghost" size="sm" onClick={() => removeMutation.mutate(share.id)}>
                    Remove
                  </Button>
                </li>
              ))}
            </ul>
          </div>
          <div className="flex items-center justify-between rounded-lg border px-3 py-3">
            <div>
              <p className="text-sm font-medium">Real-time collaboration</p>
              <p className="text-xs text-muted-foreground">Participants can chat with the agent together</p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={realtime}
              className={`relative h-6 w-10 rounded-full transition ${realtime ? "bg-foreground" : "bg-muted-foreground/30"}`}
              onClick={() => {
                const next = !realtime;
                setRealtime(next);
                realtimeMutation.mutate(next);
              }}
            >
              <span className={`absolute top-0.5 size-5 rounded-full bg-background transition ${realtime ? "left-4" : "left-0.5"}`} />
            </button>
          </div>
          {error ? <p className="text-xs text-destructive">{error}</p> : null}
        </div>
        <DialogFooter className="sm:justify-between">
          <Button variant="outline" disabled>
            Copy link
          </Button>
          <div className="flex gap-2">
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button onClick={onClose}>Share</Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
