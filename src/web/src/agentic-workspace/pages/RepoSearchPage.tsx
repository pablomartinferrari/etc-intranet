import { useQuery } from "@tanstack/react-query";
import { FileText, Filter } from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";

import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { workspaceApi } from "../api";

function Highlighted({ text, terms }: { text: string; terms: string[] }) {
  if (!terms.length) return <>{text}</>;
  const pattern = new RegExp(`(${terms.map((t) => t.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|")})`, "ig");
  const parts = text.split(pattern);
  return (
    <>
      {parts.map((part, index) =>
        terms.some((t) => t.toLowerCase() === part.toLowerCase()) ? (
          <mark key={`${part}-${index}`} className="bg-yellow-200 text-inherit">
            {part}
          </mark>
        ) : (
          <span key={`${part}-${index}`}>{part}</span>
        ),
      )}
    </>
  );
}

export function WorkspaceRepoSearchPage() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const q = params.get("q") ?? "";
  const folderId = params.get("folderId") ?? undefined;
  const [types, setTypes] = useState<string[]>(["document", "spreadsheet", "image"]);
  const [modified, setModified] = useState("anytime");

  const sidebarQuery = useQuery({ queryKey: ["ws-sidebar"], queryFn: workspaceApi.sidebar });
  const searchQuery = useQuery({
    queryKey: ["ws-search", q, folderId, types, modified],
    queryFn: () => workspaceApi.search(q, folderId, types, modified),
    enabled: q.trim().length > 0,
  });

  const folderName = useMemo(
    () => sidebarQuery.data?.repository.find((f) => f.id === folderId)?.name ?? "all folders",
    [sidebarQuery.data, folderId],
  );

  function toggleType(type: string) {
    setTypes((current) => (current.includes(type) ? current.filter((t) => t !== type) : [...current, type]));
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center justify-between gap-3 px-6 py-3">
        <h1 className="text-sm font-semibold">Search</h1>
        <Input
          defaultValue={q}
          className="h-9 max-w-md rounded-full"
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              const next = new URLSearchParams(params);
              next.set("q", (e.target as HTMLInputElement).value);
              setParams(next);
            }
          }}
        />
        <select
          className="h-9 rounded-full border bg-background px-3 text-xs"
          value={folderId ?? ""}
          onChange={(e) => {
            const next = new URLSearchParams(params);
            if (e.target.value) next.set("folderId", e.target.value);
            else next.delete("folderId");
            setParams(next);
          }}
        >
          <option value="">Scope: all folders</option>
          {(sidebarQuery.data?.repository ?? []).map((folder) => (
            <option key={folder.id} value={folder.id}>
              {folder.path}
            </option>
          ))}
        </select>
      </header>
      <div className="grid min-h-0 flex-1 grid-cols-[220px_1fr] gap-4 px-6 pb-6">
        <aside className="rounded-2xl border p-4">
          <p className="mb-3 flex items-center gap-2 text-sm font-medium">
            <Filter className="size-4" /> Filters
          </p>
          {["document", "spreadsheet", "image"].map((type) => (
            <label key={type} className="mb-2 flex items-center gap-2 text-sm capitalize">
              <Checkbox checked={types.includes(type)} onCheckedChange={() => toggleType(type)} />
              {type}s
            </label>
          ))}
          <p className="mt-4 mb-2 text-xs font-medium text-muted-foreground">Modified</p>
          <select
            className="h-8 w-full rounded-md border bg-background px-2 text-sm"
            value={modified}
            onChange={(e) => setModified(e.target.value)}
          >
            <option value="anytime">Anytime</option>
            <option value="7d">Last 7 days</option>
            <option value="30d">Last 30 days</option>
            <option value="90d">Last 90 days</option>
          </select>
        </aside>
        <div className="min-h-0 overflow-y-auto">
          <p className="mb-3 text-sm text-muted-foreground">
            {searchQuery.data ? `${searchQuery.data.total} results in ${folderName}` : "Search the indexed repository"}
          </p>
          <ul className="flex flex-col gap-3">
            {(searchQuery.data?.hits ?? []).map((hit) => (
              <li key={hit.documentId}>
                <button
                  type="button"
                  className="w-full rounded-2xl border px-4 py-3 text-left hover:bg-muted/40"
                  onClick={() => navigate(`/workspace/search`)}
                >
                  <Link
                    to={`/workspace/repo?doc=${hit.documentId}`}
                    className="flex items-center gap-2 text-sm font-medium"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <FileText className="size-4 text-muted-foreground" />
                    {hit.title}
                    <span className="text-xs font-normal text-muted-foreground">{hit.path}</span>
                  </Link>
                  <p className="mt-1 text-sm text-muted-foreground">
                    <Highlighted text={hit.snippet} terms={hit.highlights} />
                  </p>
                </button>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}
