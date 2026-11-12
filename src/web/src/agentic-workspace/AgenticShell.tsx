import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Outlet, useNavigate } from "react-router-dom";

import { AgenticSidebar } from "./AgenticSidebar";
import { workspaceApi } from "./api";

export function AgenticShell() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const sidebarQuery = useQuery({ queryKey: ["ws-sidebar"], queryFn: workspaceApi.sidebar });
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
