import { useQuery } from "@tanstack/react-query";
import { Calendar, CheckSquare, Lightbulb, Reply, Sparkles, Video } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { workspaceApi, type DayItem, type DaySection } from "../api";
import { Composer } from "../Composer";

const ICONS = {
  meetings: Calendar,
  tasks: CheckSquare,
  "follow-ups": Reply,
  suggestions: Lightbulb,
} as const;

function MeetingRow({ item }: { item: DayItem }) {
  return (
    <div key={item.id} className="min-w-0 space-y-1">
      {item.href ? (
        <Link to={item.href} className="text-sm font-medium hover:underline">
          {item.title}
        </Link>
      ) : (
        <p className="text-sm font-medium">{item.title}</p>
      )}
      {item.detail ? <p className="text-xs text-muted-foreground">{item.detail}</p> : null}
      {item.organizer ? (
        <p className="text-xs text-muted-foreground">Organizer · {item.organizer}</p>
      ) : null}
      {item.attendees?.length ? (
        <p className="text-xs text-muted-foreground">
          With {item.attendees.slice(0, 4).join(", ")}
          {item.attendees.length > 4 ? ` +${item.attendees.length - 4}` : ""}
        </p>
      ) : null}
      {item.teamsLink ? (
        <a
          href={item.teamsLink}
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center gap-1 text-xs text-primary hover:underline"
        >
          <Video className="size-3" />
          Join Teams
        </a>
      ) : null}
      {item.bodyPreview ? (
        <p className="line-clamp-2 text-xs text-muted-foreground">{item.bodyPreview}</p>
      ) : null}
    </div>
  );
}

function SectionCard({ section }: { section: DaySection }) {
  const Icon = ICONS[section.id as keyof typeof ICONS] ?? Lightbulb;
  const isMeetings = section.id === "meetings";
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
        {section.items.length === 0 ? (
          <p className="text-xs text-muted-foreground">
            {isMeetings ? "No meetings on your calendar today." : "Nothing here."}
          </p>
        ) : (
          section.items.map((item) =>
            isMeetings ? (
              <MeetingRow key={item.id} item={item} />
            ) : (
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
            ),
          )
        )}
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
  const calendar = day?.calendar;
  const needsConsent = calendar?.state === "consent_required";
  const calendarIssue =
    calendar?.state === "unavailable" || calendar?.state === "consent_required" ? calendar.message : null;

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
        {needsConsent || calendarIssue ? (
          <Alert className="mt-4">
            <Calendar className="size-4" />
            <AlertTitle>
              {needsConsent ? "Calendar permission needed" : "Outlook calendar unavailable"}
            </AlertTitle>
            <AlertDescription>{calendarIssue}</AlertDescription>
          </Alert>
        ) : null}
        {day?.brief ? (
          <Card className="mt-6 rounded-2xl shadow-sm">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="flex items-center gap-2 text-sm font-medium">
                <Sparkles className="size-4 text-muted-foreground" />
                Today’s meeting brief
              </CardTitle>
              <Badge variant="outline">
                {day.brief.source === "llm" ? "Agent" : "Summary"}
              </Badge>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-sm leading-relaxed">{day.brief.text}</p>
              {day.brief.highlights.length ? (
                <div className="flex flex-wrap gap-2">
                  {day.brief.highlights.map((item) => (
                    <Badge key={item} variant="secondary">
                      {item}
                    </Badge>
                  ))}
                </div>
              ) : null}
            </CardContent>
          </Card>
        ) : null}
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
          <p className="mt-4 text-[11px] text-muted-foreground">
            Your day sources: {day.sources.join(", ")}
            {day.timeZone ? ` · ${day.timeZone}` : ""}
            {calendar?.state === "ok" ? " · your Outlook calendar" : null}
            {calendar?.state === "mock" ? " · sample meetings (dev/demo)" : null}
          </p>
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
