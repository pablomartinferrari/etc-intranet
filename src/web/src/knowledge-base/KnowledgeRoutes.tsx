import { Route, Routes } from "react-router-dom";
import { RequireAuth } from "../multifamily-lbp/auth/RequireAuth";
import { AgentSourcesRoute } from "./AgentSourcesPage";
import KnowledgeChatWorkspace from "./KnowledgeChatWorkspace";

export default function KnowledgeRoutes() {
  return (
    <RequireAuth>
      <Routes>
        <Route index element={<KnowledgeChatWorkspace />} />
        <Route path="sources" element={<AgentSourcesRoute />} />
      </Routes>
    </RequireAuth>
  );
}
