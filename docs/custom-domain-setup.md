# Custom domain for the intranet (App Service)

SharePoint (`*.sharepoint.com`) and the intranet app are separate hosts. Your **SharePoint tenant does not automatically give you a subdomain** for Azure App Service, but it **does help** because:

- Users already sign in with the same **Microsoft Entra ID** tenant.
- IT already manages **Microsoft 365** and can add a DNS record for your company domain.
- You avoid “random `azurewebsites.net`” browser warnings after you use a branded URL like `https://intranet.yourcompany.com`.

This app is **one hostname** for both the React UI and the API (`/api/...` on the same origin).

---

## 1. Pick a hostname

Choose something IT is willing to publish, for example:

| Option | Example | DNS type |
|--------|---------|----------|
| Subdomain (recommended) | `intranet.etcenvironmental.com` | **CNAME** → App Service |
| Apps subdomain | `apps.etcenvironmental.com` | **CNAME** |
| Apex / root | `etcenvironmental.com` | **A** record + domain verification (harder) |

Use a **subdomain**, not the root domain, unless DNS admin prefers apex setup.

**Current App Service default host** (from your deployment):

`intranet-yfjgdqq7k75by-api.azurewebsites.net`

---

## 2. DNS (whoever manages your company domain)

In the DNS zone for `yourcompany.com` (same place M365/SharePoint DNS often lives):

| Type | Name | Value |
|------|------|--------|
| **CNAME** | `intranet` (or your chosen label) | `intranet-yfjgdqq7k75by-api.azurewebsites.net` |

- TTL: 300–3600 seconds is fine.
- Wait for propagation (minutes to a few hours).

**Optional:** Add a TXT record if Azure asks for domain ownership verification during custom domain setup (Portal shows the exact name/value).

---

## 3. Azure App Service — bind domain + TLS

### Option A — Script (recommended)

From the intranet repo, after DNS exists:

```powershell
cd c:\dev\etc\intranet
.\scripts\configure-custom-domain.ps1 `
  -CustomHostname "intranet.yourcompany.com" `
  -ResourceGroup "rg-intranet-dev" `
  -WebAppName "intranet-yfjgdqq7k75by-api"
```

This adds the hostname and a **free App Service managed certificate** (HTTPS).

### Option B — Azure Portal

1. **App Service** → `intranet-yfjgdqq7k75by-api`
2. **Settings** → **Custom domains** → **Add custom domain**
3. Enter `intranet.yourcompany.com` → validate (CNAME must resolve)
4. **Add binding** → create **App Service Managed Certificate**
5. Enable **HTTPS Only** (should already be on)

---

## 4. Microsoft Entra ID (SPA app)

In the **intranet front-end** app registration:

1. **Authentication** → **Platform: Single-page application**
2. **Redirect URIs** — add (keep the old one until cutover is done):

   `https://intranet.yourcompany.com`

   No path, no trailing slash.

3. Save. Grant admin consent if prompted.

Remove the old `azurewebsites.net` URI only after everything works on the new URL.

---

## 5. Rebuild and redeploy the intranet

Set production env for the Vite build:

```env
VITE_ENTRA_TENANT_ID=<your-tenant-guid>
VITE_ENTRA_CLIENT_ID=<spa-client-id>
VITE_API_SCOPE=api://<api-client-id>/access_as_user
VITE_ENTRA_REDIRECT_URI=https://intranet.yourcompany.com
```

```powershell
cd c:\dev\etc\intranet\src\web
npm run build
cd ..\api
dotnet publish -c Release -o .\publish
# deploy publish.zip (same as today)
```

Verify:

- `https://intranet.yourcompany.com/health/live` → 200
- Sign in → address bar stays on your domain (no long `#code=` after our app update)
- `https://intranet.yourcompany.com/jobs/555555/multifamily-lbp?import=1` → works when signed in

---

## 6. SharePoint web part

Update **Processing app URL** on the upload web part (and in the `.sppkg` default if you repackage):

`https://intranet.yourcompany.com`

Redeploy `.sppkg` or edit each page’s web part properties.

---

## 7. Checklist

- [ ] DNS CNAME points to `intranet-yfjgdqq7k75by-api.azurewebsites.net`
- [ ] Custom domain + managed cert on App Service
- [ ] Entra redirect URI includes `https://intranet.yourcompany.com`
- [ ] Production build uses `VITE_ENTRA_REDIRECT_URI`
- [ ] API redeployed
- [ ] SPFx **Processing app URL** updated
- [ ] Test from SharePoint → **Review Readings** → sign in → job opens

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Domain validation fails | CNAME not propagated; use `nslookup intranet.yourcompany.com` |
| Certificate stuck | Custom domain must be validated first; Basic B1 plan supports managed certs |
| Sign-in redirect mismatch | Entra redirect URI must match `VITE_ENTRA_REDIRECT_URI` exactly |
| Still see azurewebsites.net | Old SPFx property or old bookmark; update web part URL |
| “Dangerous site” on new domain | Rare for company domain; ensure valid TLS cert is bound |

---

## Does SharePoint give us a free subdomain?

**No.** SharePoint gives you `https://<tenant>.sharepoint.com/sites/...`.  
You still need a **company-owned DNS name** (often the same domain as email/SharePoint, e.g. `etcenvironmental.com`) with a **CNAME** to App Service.

If you do not control DNS, ask whoever set up Microsoft 365 / SharePoint to add the CNAME record above.
