/** Thrown when an API call is made before MSAL has provided a token (no network request). */
export class AuthRequiredError extends Error {
  constructor(message = "Sign in required") {
    super(message);
    this.name = "AuthRequiredError";
  }
}

export function isAuthRequiredError(error: unknown): boolean {
  return error instanceof AuthRequiredError;
}
