import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FileText, Globe, Lock, Plus, Share2 } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";
import { workspaceApi } from "../api";
import { ShareChatDialog } from "../ShareChatDialog";

export function WorkspaceProjectPage() {
  const { projectId } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [shareOpen, setShareOpen] = useState(false);

  const projectQuery = useQuery({
    queryKey: ["ws-project", projectId],
    queryFn: () => workspaceApi.project(projectId!),
    enabled: !!projectId,
  });
  const filesQuery = useQuery({
    queryKey: ["ws-project-files", projectId],
    queryFn: () => workspaceApi.projectFiles(projectId!),
    enabled: !!projectId,
  });
  const chatsQuery = useQuery({
    queryKey: ["ws-project-chats", projectId],
    queryFn: () => workspaceApi.projectChats(projectId!),
    enabled: !!projectId,
  });

  const saveMutation = useMutation({
    mutationFn: (instructions: string) => workspaceApi.updateProject(projectId!, { instructions }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["ws-project", projectId] }),
  });

  const createChat = useMutation({
    mutationFn: () => workspaceApi.createChat({ projectId, title: "New chat" }),
    onSuccess: (chat) => {
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      navigate(`/workspace/chats/${chat.id}`);
    },
  });

  const project = projectQuery.data;
  const firstShareable = chatsQuery.data?.[0];

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center justify-between px-8 py-4">
        <h1 className="text-sm font-semibold">{project?.name ?? "Project"}</h1>
        <div className="flex gap-2">
          <Button size="sm" className="rounded-full" onClick={() => createChat.mutate()}>
            <Plus /> New chat
          </Button>
          <Button size="sm" variant="outline" className="rounded-full" onClick={() => setShareOpen(true)}>
            <Share2 /> Share
          </Button>
        </div>
      </header>
      <div className="min-h-0 flex-1 overflow-y-auto px-8 pb-8">
        <div className="grid gap-4 lg:grid-cols-2">
          <Card className="rounded-2xl shadow-sm">
            <CardHeader>
              <CardTitle className="text-sm font-medium">Instructions</CardTitle>
            </CardHeader>
            <CardContent>
              <Textarea
                key={project?.id}
                defaultValue={project?.instructions ?? ""}
                className="min-h-32"
                onBlur={(e) => {
                  if (e.target.value !== (project?.instructions ?? "")) {
                    saveMutation.mutate(e.target.value);
                  }
                }}
              />
            </CardContent>
          </Card>
          <Card className="rounded-2xl shadow-sm">
            <CardHeader>
              <CardTitle className="text-sm font-medium">Files from repository</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2 text-sm">
              {(filesQuery.data ?? []).map((file) => (
                <Link key={file.id} to={`/workspace/repo/${file.folderId}?doc=${file.id}`} className="flex items-center gap-2 hover:underline">
                  <FileText className="size-4 text-muted-foreground" />
                  {file.name}
                </Link>
              ))}
              {(filesQuery.data ?? []).length === 0 ? (
                <p className="text-sm text-muted-foreground">No linked files yet. Browse the repository and attach docs from there.</p>
              ) : null}
            </CardContent>
          </Card>
        </div>
        <Card className="mt-4 rounded-2xl shadow-sm">
          <CardHeader>
            <CardTitle className="text-sm font-medium">Chats in this project</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-[1fr_160px_140px] gap-2 px-1 pb-2 text-xs text-muted-foreground">
              <span>Chat</span>
              <span>Last activity</span>
              <span>Visibility</span>
            </div>
            <ul className="divide-y">
              {(chatsQuery.data ?? []).map((chat) => (
                <li key={chat.id}>
                  <Link
                    to={`/workspace/chats/${chat.id}`}
                    className="grid grid-cols-[1fr_160px_140px] items-center gap-2 py-3 text-sm hover:bg-muted/40"
                  >
                    <span className="truncate">{chat.title}</span>
                    <span className="text-muted-foreground">{chat.lastActivityLabel}</span>
                    <span className="flex items-center gap-1 text-muted-foreground">
                      {chat.visibility === "shared" ? <Globe className="size-3.5" /> : <Lock className="size-3.5" />}
                      {chat.visibility === "shared" ? `Shared · ${chat.shareCount}` : "Private"}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>
      <ShareChatDialog open={shareOpen} chat={firstShareable} onClose={() => setShareOpen(false)} />
    </div>
  );
}
