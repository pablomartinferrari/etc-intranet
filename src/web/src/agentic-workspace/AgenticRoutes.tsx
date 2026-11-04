import { Route, Routes } from "react-router-dom";

import { AgenticShell } from "./AgenticShell";
import { WorkspaceChatPage } from "./pages/ChatPage";
import { WorkspaceHomePage } from "./pages/HomePage";
import { WorkspaceProjectPage } from "./pages/ProjectPage";
import { WorkspaceRepoBrowsePage } from "./pages/RepoBrowsePage";
import { WorkspaceRepoSearchPage } from "./pages/RepoSearchPage";

export default function AgenticRoutes() {
  return (
    <Routes>
      <Route element={<AgenticShell />}>
        <Route index element={<WorkspaceHomePage />} />
        <Route path="chats/:chatId" element={<WorkspaceChatPage />} />
        <Route path="projects/:projectId" element={<WorkspaceProjectPage />} />
        <Route path="repo" element={<WorkspaceRepoBrowsePage />} />
        <Route path="repo/:folderId" element={<WorkspaceRepoBrowsePage />} />
        <Route path="search" element={<WorkspaceRepoSearchPage />} />
      </Route>
    </Routes>
  );
}
