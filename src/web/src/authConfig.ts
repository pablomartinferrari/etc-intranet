import { LogLevel, type Configuration } from "@azure/msal-browser";

const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID ?? "common";
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID ?? "";
const apiScope = import.meta.env.VITE_API_SCOPE ?? "";

/** In production, always use the served origin so Azure deploys are not tied to dev .env. */
const redirectUri = import.meta.env.PROD
  ? window.location.origin
  : import.meta.env.VITE_ENTRA_REDIRECT_URI || window.location.origin;

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
    navigateToLoginRequestUrl: false,
  },
  cache: {
    cacheLocation: "sessionStorage",
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
    },
  },
};

export const loginRequest = {
  scopes: ["openid", "profile", "email"],
};

export const apiRequest = {
  scopes: apiScope ? [apiScope] : [],
};

/** Redirect login — includes API scope so the first sign-in can call /api without a failed request. */
export const signInRequest = {
  scopes: [...new Set([...loginRequest.scopes, ...apiRequest.scopes])],
};
