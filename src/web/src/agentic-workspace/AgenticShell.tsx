import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { Outlet, useNavigate } from "react-router-dom";

import { AgenticSidebar } from "./AgenticSidebar";
import { workspaceApi } from "./api";
import { connectRepositoryHub } from "./realtime";

export function AgenticShell() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const sidebarQuery = useQuery({ queryKey: ["ws-sidebar"], queryFn: workspaceApi.sidebar });

  useEffect(() => {
    let stopped = false;
    let connection: Awaited<ReturnType<typeof connectRepositoryHub>> | null = null;
    void connectRepositoryHub(() => {
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      void queryClient.invalidateQueries({ queryKey: ["ws-repo"] });
      void queryClient.invalidateQueries({ queryKey: ["ws-chat"] });
      void queryClient.invalidateQueries({ queryKey: ["ws-folder-chats"] });
    })
      .then((hub) => {
        if (stopped) {
          void hub.stop();
          return;
        }
        connection = hub;
      })
      .catch(() => {
        /* SignalR is optional for folder-tree refresh */
      });
    return () => {
      stopped = true;
      void connection?.stop();
    };
  }, [queryClient]);
  const createChat = useMutation({
    mutationFn: () => workspaceApi.createChat({ title: "New chat" }),
    onSuccess: (chat) => {
      void queryClient.invalidateQueries({ queryKey: ["ws-sidebar"] });
      void queryClient.invalidateQueries({ queryKey: ["ws-project"] });
      navigate(`/workspace/chats/${chat.id}`);
    },
  });

  return (
    <div className="flex h-dvh overflow-hidden bg-background">
      <AgenticSidebar data={sidebarQuery.data} onNewChat={() => createChat.mutate()} />
      <main className="min-w-0 flex-1">
        <Outlet />
      </main>
    </div>
  );
}
