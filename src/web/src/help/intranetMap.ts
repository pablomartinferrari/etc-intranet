export type HelpLink = {
  label: string;
  path: string;
};

export type HelpAskResponse = {
  answer: string;
  links: HelpLink[];
  source: "map" | "llm";
};

export const SUGGESTED_HELP_QUESTIONS = [
  "Where do I go to create a chat?",
  "Where are bids?",
  "How do I request a feature?",
  "What's Pipeline vs Bids?",
] as const;

type Place = {
  title: string;
  path: string;
  keywords: string[];
  answer: string;
};

const PLACES: Place[] = [
  {
    title: "Home",
    path: "/",
    keywords: ["home", "intranet", "applications", "apps"],
    answer:
      "Home is the intranet landing page. After you sign in you will see Chat, Lead, Sales, and Feature Requests.",
  },
  {
    title: "Chat",
    path: "/knowledge",
    keywords: ["chat", "chatgpt", "knowledge", "conversation", "project"],
    answer:
      "Open Chat from Home. Create a project with New project if you do not have one, then click New chat.",
  },
  {
    title: "Lead",
    path: "/lead-inspection",
    keywords: ["lead", "lbp", "xrf", "inspection", "job"],
    answer:
      "Lead is the multifamily LBP workspace. From Home open Lead, then enter a job number.",
  },
  {
    title: "Sales",
    path: "/sales",
    keywords: ["sales"],
    answer: "Sales is the hub for Bids and Pipeline. Open the Sales card on Home.",
  },
  {
    title: "Bids",
    path: "/opportunities",
    keywords: ["bids", "bid", "opportunities", "cleatus"],
    answer:
      "Bids live under Sales. From Home open Sales, then Bids — recommended government opportunities from CLEATUS.",
  },
  {
    title: "Pipeline",
    path: "/pipeline",
    keywords: ["pipeline", "pursuit", "pursuits", "closeout", "close-out"],
    answer:
      "Pipeline tracks pursuits and close-outs. From Home open Sales, then Pipeline.",
  },
  {
    title: "Feature Requests",
    path: "/requests",
    keywords: ["request", "requests", "feature", "change", "ticket"],
    answer:
      "Open the Feature Requests card on Home. Use Add feature request to suggest an intranet improvement — Chat, Lead, Sales, General, or another topic. Review the queue on that same page.",
  },
  {
    title: "Agent sources",
    path: "/knowledge/sources",
    keywords: ["sources", "sharepoint", "knowledge", "context", "ingest", "folder"],
    answer:
      "In Chat, click Add SharePoint folder (Help has the same button). Paste a site URL and folder path, review the size estimate, and connect. Manage connected folders from Manage sources.",
  },
];

function normalize(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim();
}

/** Client-side map match used when /api/help/ask is unreachable. */
export function matchHelpLocally(question: string): HelpAskResponse {
  const normalized = normalize(question);
  if (!normalized) {
    return {
      answer:
        "This intranet has Chat, Lead, Sales, and Feature Requests on Home. Ask where you want to go.",
      links: [{ label: "Home", path: "/" }],
      source: "map",
    };
  }

  if (
    normalized.includes("pipeline vs bids") ||
    normalized.includes("bids vs pipeline") ||
    (normalized.includes("pipeline") &&
      normalized.includes("bids") &&
      (normalized.includes("vs") || normalized.includes("difference")))
  ) {
    return {
      answer:
        "Bids is the CLEATUS opportunity list. Pipeline is deals ETC is already pursuing, plus close-out. Both start from Sales.",
      links: [
        { label: "Bids", path: "/opportunities" },
        { label: "Pipeline", path: "/pipeline" },
        { label: "Sales", path: "/sales" },
      ],
      source: "map",
    };
  }

  const tokens = new Set(normalized.split(/\s+/).filter(Boolean));
  let best: Place | undefined;
  let bestScore = 0;
  for (const place of PLACES) {
    let score = 0;
    for (const keyword of place.keywords) {
      if (tokens.has(keyword) || normalized.includes(keyword)) {
        score += keyword.length >= 5 ? 2 : 1;
      }
    }
    if (score > bestScore) {
      best = place;
      bestScore = score;
    }
  }

  if (best && bestScore > 0) {
    return {
      answer: best.answer,
      links: [{ label: best.title, path: best.path }],
      source: "map",
    };
  }

  return {
    answer:
      "This intranet has four Home apps: Chat (knowledge-base ChatGPT), Lead (LBP / XRF jobs), Sales (Bids and Pipeline), and Feature Requests (suggest and track intranet improvements).",
    links: [
      { label: "Home", path: "/" },
      { label: "Chat", path: "/knowledge" },
      { label: "Lead", path: "/lead-inspection" },
      { label: "Sales", path: "/sales" },
      { label: "Feature Requests", path: "/requests" },
    ],
    source: "map",
  };
}
