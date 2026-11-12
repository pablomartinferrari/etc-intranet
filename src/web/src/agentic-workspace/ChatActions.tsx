import { useMutation, useQueryClient } from "@tanstack/react-query";
import { MoreHorizontal } from "lucide-react";
import { useState, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { workspaceApi, type WorkspaceChat } from "./api";

export const CHAT_TITLE_MAX_LENGTH = 200;

function afterDeletePath(chat: WorkspaceChat) {
  return chat.folderId ? `/workspace/repo/${chat.folderId}` : "/workspace";
}

export function ChatOverflowMenu({
  chat,
  tone = "page",
}: {
  chat: WorkspaceChat;
  tone?: "page" | "sidebar";
}) {
  const navigate = useNavigate();
  const { chatId: openChatId } = useParams();
  const queryClient = useQueryClient();
  const isOwner = chat.role === "owner";
  const canLeave = !!chat.myShareId;
  const [renameOpen, setRenameOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [leaveOpen, setLeaveOpen] = useState(false);
  const [title, setTitle] = useState(chat.title);
  const [error, setError] = useState<string | null>(null);

  const renameMutation = useMutation({
    mutationFn: (next: string) => workspaceApi.updateChat(chat.id, { title: next }),
    onSuccess: async () => {
      setRenameOpen(false);
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      await queryClient.invalidateQueries({ queryKey: ["ws-chat", chat.id] });
    },
    onError: (err: Error) => setError(err.message),
  });

  const deleteMutation = useMutation({
    mutationFn: () => workspaceApi.deleteChat(chat.id),
    onSuccess: async () => {
      setDeleteOpen(false);
      await queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      await queryClient.invalidateQueries({ queryKey: ["ws-chat"] });
      if (openChatId === chat.id) {
        navigate(afterDeletePath(chat));
      }
    },
    onError: (err: Error) => setError(err.message),
  });

  const leaveMutation = useMutation({
    mutationFn: () => workspaceApi.leaveChat(chat.id),
    onSuccess: async () => {
      setLeaveOpen(false);
      await queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      await queryClient.invalidateQueries({ queryKey: ["ws-chat"] });
      if (openChatId === chat.id) {
        navigate("/workspace");
      }
    },
    onError: (err: Error) => setError(err.message),
  });

  if (!isOwner && !canLeave) return null;

  function submitRename(event: FormEvent) {
    event.preventDefault();
    const next = title.trim();
    if (!next) {
      setError("Title is required.");
      return;
    }
    renameMutation.mutate(next);
  }

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            type="button"
            size="icon-xs"
            variant="ghost"
            aria-label={`Chat actions for ${chat.title}`}
            className={cn(
              "shrink-0",
              tone === "sidebar" &&
                "text-sidebar-foreground/60 hover:bg-sidebar-accent hover:text-sidebar-foreground",
            )}
            onClick={(event) => event.stopPropagation()}
            onPointerDown={(event) => event.stopPropagation()}
          >
            <MoreHorizontal />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" onClick={(event) => event.stopPropagation()}>
          {isOwner ? (
            <>
              <DropdownMenuItem
                onSelect={() => {
                  setTitle(chat.title);
                  setError(null);
                  setRenameOpen(true);
                }}
              >
                Rename
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                variant="destructive"
                onSelect={() => {
                  setError(null);
                  setDeleteOpen(true);
                }}
              >
                Delete
              </DropdownMenuItem>
            </>
          ) : (
            <DropdownMenuItem
              onSelect={() => {
                setError(null);
                setLeaveOpen(true);
              }}
            >
              Remove from Shared with me
            </DropdownMenuItem>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      <Dialog
        open={renameOpen}
        onOpenChange={(open) => {
          setRenameOpen(open);
          if (!open) setError(null);
        }}
      >
        <DialogContent>
          <form className="contents" onSubmit={submitRename}>
            <DialogHeader>
              <DialogTitle>Rename chat</DialogTitle>
            </DialogHeader>
            <div className="py-3">
              <Label htmlFor={`rename-chat-${chat.id}`}>Title</Label>
              <Input
                id={`rename-chat-${chat.id}`}
                className="mt-1.5"
                autoFocus
                maxLength={CHAT_TITLE_MAX_LENGTH}
                value={title}
                onChange={(e) => setTitle(e.target.value)}
              />
            </div>
            {error ? <p className="text-xs text-destructive">{error}</p> : null}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setRenameOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={renameMutation.isPending || !title.trim()}>
                Save
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog
        open={deleteOpen}
        onOpenChange={(open) => {
          setDeleteOpen(open);
          if (!open) setError(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete “{chat.title}”?</DialogTitle>
            <DialogDescription>This can&apos;t be undone.</DialogDescription>
          </DialogHeader>
          {error ? <p className="text-xs text-destructive">{error}</p> : null}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setDeleteOpen(false)}>
              Cancel
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={deleteMutation.isPending}
              onClick={() => deleteMutation.mutate()}
            >
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={leaveOpen}
        onOpenChange={(open) => {
          setLeaveOpen(open);
          if (!open) setError(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove “{chat.title}” from Shared with me?</DialogTitle>
            <DialogDescription>You will lose access unless someone shares it with you again.</DialogDescription>
          </DialogHeader>
          {error ? <p className="text-xs text-destructive">{error}</p> : null}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setLeaveOpen(false)}>
              Cancel
            </Button>
            <Button type="button" disabled={leaveMutation.isPending} onClick={() => leaveMutation.mutate()}>
              Remove
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
