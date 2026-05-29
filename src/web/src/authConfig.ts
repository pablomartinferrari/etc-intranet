import { LogLevel, type Configuration } from "@azure/msal-browser";

const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID ?? "common";
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID ?? "";
const apiScope = import.meta.env.VITE_API_SCOPE ?? "";

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri: import.meta.env.VITE_ENTRA_REDIRECT_URI || window.location.origin,
    postLogoutRedirectUri: import.meta.env.VITE_ENTRA_REDIRECT_URI || window.location.origin,
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
