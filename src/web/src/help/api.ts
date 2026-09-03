import { apiPost } from "../multifamily-lbp/api/client";
import { matchHelpLocally, type HelpAskResponse } from "./intranetMap";

export type { HelpAskResponse, HelpLink } from "./intranetMap";

export async function askHelp(question: string): Promise<HelpAskResponse> {
  try {
    return await apiPost<HelpAskResponse>("/help/ask", { question });
  } catch {
    return matchHelpLocally(question);
  }
}
