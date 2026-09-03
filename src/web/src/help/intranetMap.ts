import catalog from "./intranet-map.json" with { type: "json" };

/** Keep in sync with Intranet.Api.Help.IntranetMap (see IntranetMapTests). */

export type HelpLink = {
  label: string;
  path: string;
};

export type HelpAskResponse = {
  answer: string;
  links: HelpLink[];
  source: "map" | "llm";
  provider?: string | null;
  model?: string | null;
};

type CatalogPlace = {
  id: string;
  title: string;
  path: string;
  aliases: string[];
  purpose: string;
  commonQuestions: string[];
  fallbackAnswer: string;
};

type Catalog = {
  suggestedQuestions: string[];
  places: CatalogPlace[];
};

const CATALOG = catalog as Catalog;

export const SUGGESTED_HELP_QUESTIONS = CATALOG.suggestedQuestions;

const STOPWORDS = new Set([
  "a",
  "an",
  "the",
  "is",
  "are",
  "am",
  "do",
  "does",
  "did",
  "to",
  "of",
  "in",
  "on",
  "at",
  "for",
  "and",
  "or",
  "if",
  "from",
  "with",
  "about",
  "where",
  "what",
  "whats",
  "how",
  "why",
  "who",
  "which",
  "go",
  "get",
  "i",
  "me",
  "my",
  "we",
  "you",
  "your",
  "it",
  "this",
  "that",
  "can",
  "please",
]);

function normalize(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim();
}

function tokensOf(normalized: string): Set<string> {
  return new Set(normalized.split(/\s+/).filter((token) => token && !STOPWORDS.has(token)));
}

function scorePlace(place: CatalogPlace, normalized: string, tokens: Set<string>): number {
  let score = 0;
  const title = normalize(place.title);
  if (title && normalized.includes(title)) {
    score += 8;
  }
  for (const part of title.split(/\s+/).filter(Boolean)) {
    if (tokens.has(part)) {
      score += part.length >= 5 ? 4 : 3;
    }
  }
  for (const alias of place.aliases) {
    const key = normalize(alias);
    if (!key) {
      continue;
    }
    if (key.includes(" ")) {
      if (normalized.includes(key)) {
        score += 7;
      }
      continue;
    }
    if (tokens.has(key)) {
      score += key.length >= 5 ? 5 : 3;
    }
  }
  for (const common of place.commonQuestions) {
    const needle = normalize(common);
    if (!needle) {
      continue;
    }
    if (normalized.includes(needle) || (needle.length > 12 && needle.includes(normalized))) {
      score += 12;
      continue;
    }
    const cqTokens = [...tokensOf(needle)];
    const overlap = cqTokens.filter((token) => tokens.has(token)).length;
    if (overlap >= 2 && overlap * 10 >= cqTokens.length * 6) {
      score += 8;
    }
  }
  const haystack = normalize(`${place.purpose} ${place.fallbackAnswer}`);
  const hayTokens = new Set(haystack.split(/\s+/).filter(Boolean));
  for (const token of tokens) {
    if (hayTokens.has(token)) {
      score += token.length >= 5 ? 2 : 1;
    }
  }
  return score;
}

function overview(): HelpAskResponse {
  return {
    answer:
      "This intranet has four Home apps: Chat (knowledge-base ChatGPT), Lead (LBP / XRF jobs), Sales (Bids and Pipeline), and Feature Requests (suggest and track intranet improvements). Sign in with Microsoft on Home. Ask where you want to go.",
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

function linksFor(ids: string[]): HelpLink[] {
  const links: HelpLink[] = [];
  const seen = new Set<string>();
  for (const id of ids) {
    const place = CATALOG.places.find((item) => item.id === id);
    if (!place?.path || seen.has(place.path)) {
      continue;
    }
    seen.add(place.path);
    links.push({ label: place.title, path: place.path });
  }
  return links;
}

/** Client-side map match used when /api/help/ask is unreachable. */
export function matchHelpLocally(question: string): HelpAskResponse {
  const normalized = normalize(question);
  if (!normalized) {
    return overview();
  }

  const tokens = tokensOf(normalized);
  const scored = CATALOG.places
    .map((place) => ({ place, score: scorePlace(place, normalized, tokens) }))
    .filter((row) => row.score > 0)
    .sort((a, b) => b.score - a.score || a.place.title.localeCompare(b.place.title));

  if (scored.length === 0 || scored[0].score < 2) {
    return overview();
  }

  const comparison =
    normalized.includes(" vs ") ||
    normalized.includes(" versus ") ||
    normalized.includes("difference") ||
    (normalized.includes("bids") && normalized.includes("pipeline"));

  if (comparison && scored.length >= 2) {
    const ids = [scored[0].place.id, scored[1].place.id];
    if (ids.includes("bids") && ids.includes("pipeline")) {
      ids.push("sales");
    }
    return {
      answer:
        "Bids is the CLEATUS opportunity list. Pipeline is deals ETC is already pursuing, plus close-out. Both start from Sales.",
      links: linksFor(ids),
      source: "map",
    };
  }

  if (scored.length >= 2 && scored[1].score >= 5 && scored[1].score * 4 >= scored[0].score * 3) {
    const top = scored.slice(0, 3);
    return {
      answer: `I can help with ${top.map((row) => row.place.title).join(", ")}. Ask about one of those, or open a link below.`,
      links: linksFor(top.map((row) => row.place.id)),
      source: "map",
    };
  }

  const best = scored[0].place;
  return {
    answer: best.fallbackAnswer,
    links: linksFor([best.id]),
    source: "map",
  };
}

export function helpAnswerSourceLabel(result: HelpAskResponse): string {
  if (result.source !== "llm") {
    return "Answered from the intranet map.";
  }

  const model = result.model?.trim();
  const provider = result.provider?.trim();
  if (provider === "ollama") {
    return model ? `Answered by ${model} (local).` : "Answered by the local model.";
  }
  if (provider === "openai" || provider === "azure-openai") {
    return model ? `Answered by ${model} (hosted).` : "Answered by the hosted model.";
  }
  if (model) {
    return `Answered by ${model}.`;
  }
  return "Answered with AI from the intranet map.";
}
