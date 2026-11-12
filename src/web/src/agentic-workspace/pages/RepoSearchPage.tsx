import { useQuery } from "@tanstack/react-query";
import { FileText, Filter } from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";

import { Button } from "@/components/ui/button";
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
  const folderId = params.get("folderId") || undefined;
  // "All of repository" is a manual choice (`all=1`) or the default when search
  // was opened without a folder. Empty-state "Search all of repository" only
  // widens when the user clicks it — search never auto-widens.
  const allRepo = params.get("all") === "1" || !folderId;
  const scopedFolderId = allRepo ? undefined : folderId;
  const [types, setTypes] = useState<string[]>(["document", "spreadsheet", "image"]);
  const [modified, setModified] = useState("anytime");

  const sidebarQuery = useQuery({ queryKey: ["ws-sidebar"], queryFn: workspaceApi.sidebar });
  const searchQuery = useQuery({
    queryKey: ["ws-search", q, scopedFolderId, types, modified],
    queryFn: () => workspaceApi.search(q, scopedFolderId, types, modified),
    enabled: q.trim().length > 0,
  });

  const folderPath = useMemo(() => {
    if (searchQuery.data?.folderPath) return searchQuery.data.folderPath;
    return sidebarQuery.data?.repository.find((f) => f.id === folderId)?.path;
  }, [searchQuery.data, sidebarQuery.data, folderId]);

  const scopeLabel = scopedFolderId && folderPath ? `Searching in ${folderPath}` : "Searching all of repository";

  function setAllRepo(next: boolean) {
    const nextParams = new URLSearchParams(params);
    if (next) {
      nextParams.set("all", "1");
    } else {
      nextParams.delete("all");
      if (folderId) nextParams.set("folderId", folderId);
    }
    setParams(nextParams);
  }

  function toggleType(type: string) {
    setTypes((current) => (current.includes(type) ? current.filter((t) => t !== type) : [...current, type]));
  }

  const emptyScoped =
    !!scopedFolderId &&
    q.trim().length > 0 &&
    !searchQuery.isFetching &&
    (searchQuery.data?.total ?? 0) === 0;

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center justify-between gap-3 px-6 py-3">
        <h1 className="text-sm font-semibold">Search</h1>
        <div className="flex min-w-0 flex-1 flex-col items-stretch gap-1">
          <Input
            defaultValue={q}
            className="h-9 rounded-full"
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                const next = new URLSearchParams(params);
                next.set("q", (e.target as HTMLInputElement).value);
                setParams(next);
              }
            }}
          />
          <div className="flex flex-wrap items-center justify-between gap-2 px-1">
            <p className="text-[11px] text-muted-foreground">{scopeLabel}</p>
            {folderId ? (
              <label className="flex items-center gap-2 text-[11px]">
                <Checkbox checked={allRepo} onCheckedChange={(value) => setAllRepo(value === true)} />
                All of repository
              </label>
            ) : null}
          </div>
        </div>
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
            {searchQuery.data ? `${searchQuery.data.total} results · ${scopeLabel}` : "Search the indexed repository"}
          </p>
          {emptyScoped ? (
            <div className="mb-4 rounded-2xl border px-4 py-3 text-sm">
              <p className="text-muted-foreground">Nothing in this folder or its subfolders matched.</p>
              <Button className="mt-2 rounded-full" size="sm" onClick={() => setAllRepo(true)}>
                Search all of repository
              </Button>
            </div>
          ) : null}
          <ul className="flex flex-col gap-3">
            {(searchQuery.data?.hits ?? []).map((hit) => (
              <li key={hit.documentId}>
                <button
                  type="button"
                  className="w-full rounded-2xl border px-4 py-3 text-left hover:bg-muted/40"
                  onClick={() => navigate(`/workspace/repo/${hit.folderId}?doc=${hit.documentId}`)}
                >
                  <Link
                    to={`/workspace/repo/${hit.folderId}?doc=${hit.documentId}`}
                    className="flex items-center gap-2 text-sm font-medium"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <FileText className="size-4 text-muted-foreground" />
                    {hit.title}
                  </Link>
                  <p className="mt-1 text-xs text-muted-foreground">{hit.path}</p>
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
