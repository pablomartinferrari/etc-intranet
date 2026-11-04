import { useQuery } from "@tanstack/react-query";
import { Calendar, CheckSquare, Lightbulb, Reply } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { workspaceApi, type DaySection } from "../api";
import { Composer } from "../Composer";

const ICONS = {
  meetings: Calendar,
  tasks: CheckSquare,
  "follow-ups": Reply,
  suggestions: Lightbulb,
} as const;

function SectionCard({ section }: { section: DaySection }) {
  const Icon = ICONS[section.id as keyof typeof ICONS] ?? Lightbulb;
  return (
    <Card className="rounded-2xl shadow-sm">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <CardTitle className="flex items-center gap-2 text-sm font-medium">
          <Icon className="size-4 text-muted-foreground" />
          {section.title}
        </CardTitle>
        <span className="text-xs text-muted-foreground">{section.items.length}</span>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {section.items.map((item) => (
          <div key={item.id} className="min-w-0">
            {item.href ? (
              <Link to={item.href} className="text-sm font-medium hover:underline">
                {item.title}
              </Link>
            ) : (
              <p className="text-sm font-medium">{item.title}</p>
            )}
            {item.detail ? <p className="text-xs text-muted-foreground">{item.detail}</p> : null}
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

export function WorkspaceHomePage() {
  const navigate = useNavigate();
  const dayQuery = useQuery({ queryKey: ["ws-day"], queryFn: workspaceApi.day });
  const sidebarQuery = useQuery({ queryKey: ["ws-sidebar"], queryFn: workspaceApi.sidebar });

  const day = dayQuery.data;
  const firstName = (day?.userDisplayName ?? "there").split(/\s+/)[0];

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center justify-between px-8 py-5">
        <div />
        <span className="rounded-full border px-3 py-1 text-xs text-muted-foreground">Today</span>
      </header>
      <div className="min-h-0 flex-1 overflow-y-auto px-8 pb-6">
        <h1 className="text-3xl font-semibold tracking-tight">
          {day?.greeting ?? "Good morning"}, {firstName}
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">Here is a summary of your day</p>
        <div className="mt-8 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {dayQuery.isPending
            ? Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-48 rounded-2xl" />)
            : (day?.sections ?? []).map((section) => <SectionCard key={section.id} section={section} />)}
        </div>
        <div className="mt-6 grid gap-4 lg:grid-cols-2">
          <Card className="rounded-2xl shadow-sm">
            <CardHeader>
              <CardTitle className="text-sm font-medium">Recent chats &amp; projects</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2 text-sm">
              {(sidebarQuery.data?.myChats ?? []).slice(0, 4).map((chat) => (
                <Link key={chat.id} to={`/workspace/chats/${chat.id}`} className="truncate hover:underline">
                  {chat.title}
                  {chat.projectName ? <span className="text-muted-foreground"> · {chat.projectName}</span> : null}
                </Link>
              ))}
            </CardContent>
          </Card>
          <Card className="rounded-2xl shadow-sm">
            <CardHeader>
              <CardTitle className="text-sm font-medium">Recently shared with me</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2 text-sm">
              {(sidebarQuery.data?.sharedWithMe ?? []).slice(0, 4).map((chat) => (
                <Link key={chat.id} to={`/workspace/chats/${chat.id}`} className="truncate hover:underline">
                  {chat.title}
                </Link>
              ))}
            </CardContent>
          </Card>
        </div>
        {day?.sources?.length ? (
          <p className="mt-4 text-[11px] text-muted-foreground">Your day sources: {day.sources.join(", ")}</p>
        ) : null}
      </div>
      <Composer
        onSend={async (text) => {
          const chat = await workspaceApi.createChat({ title: "New chat" });
          await workspaceApi.send(chat.id, text);
          navigate(`/workspace/chats/${chat.id}`);
        }}
      />
    </div>
  );
}
