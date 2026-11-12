import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { PublicClientApplication, EventType } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
import App from "./App";
import "./index.css";
import { msalConfig } from "./authConfig";

const msalInstance = new PublicClientApplication(msalConfig);

/** Remove OAuth tokens from the address bar after Entra redirects back (reduces "dangerous site" heuristics). */
function clearAuthCodeFromUrl(): void {
  const hash = window.location.hash;
  const search = window.location.search;
  const hasAuthHash =
    hash.includes("code=") || hash.includes("error=") || hash.includes("id_token=");
  const hasAuthQuery = search.includes("code=") || search.includes("error=");
  if (!hasAuthHash && !hasAuthQuery) {
    return;
  }
  window.history.replaceState(window.history.state, document.title, window.location.pathname);
}

async function bootstrap() {
  await msalInstance.initialize();

  const redirectResult = await msalInstance.handleRedirectPromise();
  if (redirectResult?.account) {
    msalInstance.setActiveAccount(redirectResult.account);
  }

  clearAuthCodeFromUrl();

  const accounts = msalInstance.getAllAccounts();
  if (accounts.length > 0 && !msalInstance.getActiveAccount()) {
    msalInstance.setActiveAccount(accounts[0]);
  }

  msalInstance.addEventCallback((event) => {
    if (
      event.eventType === EventType.LOGIN_SUCCESS &&
      event.payload &&
      "account" in event.payload &&
      event.payload.account
    ) {
      msalInstance.setActiveAccount(event.payload.account);
    }
  });

  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <MsalProvider instance={msalInstance}>
        <App />
      </MsalProvider>
    </StrictMode>,
  );
}

void bootstrap();
