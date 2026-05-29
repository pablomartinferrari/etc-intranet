/** SharePoint / intranet deep link: /jobs/{jobId}/multifamily-lbp */
export const MULTIFAMILY_JOB_PATH = /^\/jobs\/([^/]+)\/multifamily-lbp(\/|$)/;

export function isMultifamilyJobEntryPath(pathname: string): boolean {
  return MULTIFAMILY_JOB_PATH.test(pathname);
}

export function parseJobIdFromReturnPath(path: string | null | undefined): string | null {
  if (!path) return null;
  const match = path.match(/^\/jobs\/([^/?#]+)\/multifamily-lbp/);
  return match?.[1] ?? null;
}

export const POST_LOGIN_NAV_KEY = "mf-post-login-nav";

export function savePostLoginReturnPath(pathname: string, search: string): void {
  sessionStorage.setItem(POST_LOGIN_NAV_KEY, `${pathname}${search}`);
}

export function readPostLoginReturnPath(): string | null {
  return sessionStorage.getItem(POST_LOGIN_NAV_KEY);
}
