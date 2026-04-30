# Fáze 5 — HTTP hardening + bezpečnost

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ 0 warning, 0 error · Tests: ✅ **72 / 72 passed**

---

## Cíl

Quick wins s velkým bezpečnostním dopadem — bez zásahu do business logiky. Pokrývá zranitelnosti identifikované ve Fázi 0:
- `/Adm/*` bez autorizace
- chybějící security headers
- chybějící Cache-Control pro statické soubory
- protocol-relative `//` u CDN URL
- `<html>` bez `lang="cs"`
- chybějící meta description / og: tagy

---

## Co bylo uděláno

### 1. Basic Auth pro `/Adm/*` ([Middleware/BasicAuthMiddleware.cs](../../src/Hydra2.Web/Middleware/BasicAuthMiddleware.cs))

Path-based middleware, který chrání cesty z `AdminAuth.ProtectedPaths` (default `/Adm`).

**Chování dle prostředí**:
| Environment | AdminAuth nakonfigurováno? | Výsledek |
|---|---|---|
| `Development` | (jakkoliv) | **Auth se přeskočí** — pohodlí pro lokální vývoj |
| `Production` / `Staging` / `Test` | NE (prázdný user/pass) | **503** s instrukcí "Set AdminAuth:Username and AdminAuth:Password" |
| `Production` / `Staging` / `Test` | ANO | Vyžaduje validní `Authorization: Basic` header (jinak 401) |

**Bezpečnostní detaily**:
- `CryptographicOperations.FixedTimeEquals` pro porovnání hesla — odolné proti timing attacks
- UTF-8 dekódování base64 (podporuje diakritiku v hesle)
- `WWW-Authenticate: Basic realm="Hydra2 Admin", charset="UTF-8"` při 401 — prohlížeč zobrazí native dialog
- Path matching: `/Adm`, `/Adm/`, `/Adm/anything` chráněno; `/AdmFoo` NE (testováno)

**Konfigurace** v `appsettings.json`:
```json
"AdminAuth": {
  "Username": "",
  "Password": "",
  "Realm": "Hydra2 Admin",
  "ProtectedPaths": [ "/Adm" ]
}
```

V produkci nastavit přes environment variables:
```
AdminAuth__Username=admin
AdminAuth__Password=<silné heslo>
```

### 2. Security headers ([Middleware/SecurityHeadersMiddleware.cs](../../src/Hydra2.Web/Middleware/SecurityHeadersMiddleware.cs))

Přidává na **každou odpověď** (včetně statických souborů, error stránek):

| Header | Hodnota |
|---|---|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `SAMEORIGIN` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `geolocation=(), microphone=(), camera=()` |
| `Content-Security-Policy` | viz níže |

**CSP**:
```
default-src 'self';
script-src 'self' 'unsafe-inline' https://www.amcharts.com https://cdn.amcharts.com;
style-src 'self' 'unsafe-inline';
img-src 'self' data: https:;
font-src 'self';
connect-src 'self';
frame-ancestors 'none';
base-uri 'self';
form-action 'self'
```

**Záměrné permisivnosti** (TODO před Fází 8/9):
- `script-src 'unsafe-inline'` — Razor view `Graf/Index.cshtml` má inline `<script>` blok pro graf logiku. Po extrakci do TypeScript v Fázi 9 se přejde na nonce-based CSP.
- `style-src 'unsafe-inline'` — Bootstrap a inline `style=""` atributy. Při migraci na BS5 zlepšit.
- `script-src https://www.amcharts.com` — amCharts EOL CDN. Po Fázi 8 (replacement) bude self-hosted nebo jiný CDN.

### 3. HSTS — strict
```csharp
builder.Services.AddHsts(opts =>
{
    opts.Preload = true;
    opts.IncludeSubDomains = true;
    opts.MaxAge = TimeSpan.FromDays(365);
});
```

`app.UseHsts()` aktivní v non-Development. Po prvním HTTPS requestu si prohlížeč pamatuje, že na hostu má vždy chodit HTTPS.

### 4. Cache-Control pro statické soubory
[Program.cs](../../src/Hydra2.Web/Program.cs):
```csharp
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        if (path.StartsWith("/lib/", ...) || path.StartsWith("/fonts/", ...))
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";  // 1 year
        else
            ctx.Context.Response.Headers.CacheControl = "public,max-age=3600";                // 1 hour
    },
});
```

| Cesta | TTL | Důvod |
|---|---|---|
| `/lib/jquery/...`, `/lib/bootstrap/...`, `/fonts/...` | 1 rok, immutable | Versioned (knihovní soubory se nemění bez bumpu) |
| `/css/site.css`, `/js/Hydra2.js` | 1 hodina | Vlastní kód, mění se bez verzování v URL |

### 5. Layout meta + lang ([Views/Shared/_Layout.cshtml](../../src/Hydra2.Web/Views/Shared/_Layout.cshtml))

```html
<html lang="cs">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta name="theme-color" content="#222222">
    <meta name="description" content="Hydra² — vodní stavy, průtoky a teploty českých řek a nádrží. Data od ČHMÚ, PVL, PLA, PMO a POH s historií od roku 2012.">
    <meta property="og:title" content="Hydra² — vodní stavy ČR" />
    <meta property="og:description" content="Vodní stavy, průtoky a teploty českých řek a nádrží. Historie od roku 2012." />
    <meta property="og:type" content="website" />
    <meta property="og:locale" content="cs_CZ" />
    ...
```

Bonus: navbar toggle button má `aria-label="Přepnout navigaci"` (a11y fix).

### 6. CDN URL: `//` → `https://`
[Views/Graf/Index.cshtml](../../src/Hydra2.Web/Views/Graf/Index.cshtml):
```html
<!-- před -->
<script src="//www.amcharts.com/lib/4/lang/cs_CZ.js"></script>

<!-- po -->
<script src="https://www.amcharts.com/lib/4/lang/cs_CZ.js" crossorigin="anonymous"></script>
```

Ostatní amCharts skripty už `https://` měly, přidán pouze `crossorigin="anonymous"` pro připravenost na SRI v budoucnu.

> **SRI** (Subresource Integrity) jsem **úmyslně neimplementoval** v této fázi. amCharts.com nepublikuje SRI hashe pro své CDN soubory; manuální výpočet by se musel měnit při každém update. Vzhledem k tomu, že amCharts 4 je EOL a Fáze 8 ho nahradí, nemá smysl investovat. Při Fázi 8 se buď self-hostnu, nebo použije CDN s SRI.

### 7. Token parameter handling — drobnost
Předchozí fáze měla `string token` v admin/jobs controllerech. To s `[ApiController]` validuje model před hitnutím akce a vrací 400 BadRequest na missing token. Změna na `string? token` (Fáze 4) nechá akci proběhnout a vrátit 401. Tato fáze to potvrzuje testy.

---

## Nové testy ([SecurityHeadersTests.cs](../../tests/Hydra2.Tests/Web/SecurityHeadersTests.cs), [AdminAuthTests.cs](../../tests/Hydra2.Tests/Web/AdminAuthTests.cs))

| Test | Co ověřuje |
|---|---|
| `Public_response_has_security_headers` | X-Content-Type-Options, X-Frame-Options, Referrer-Policy, CSP přítomné |
| `Csp_allows_amcharts_cdn_and_self` | CSP obsahuje `default-src 'self'`, `https://www.amcharts.com`, `frame-ancestors 'none'` |
| `Static_files_under_lib_have_immutable_cache` | `/lib/jquery/jquery.min.js` má `Cache-Control: public,max-age=31536000,immutable` |
| `Static_files_under_css_have_shorter_cache` | `/css/site.css` má `max-age=3600` |
| `Admin_paths_require_basic_auth` (×4) | `/Adm`, `/Adm/`, `/Adm/SpotOverView`, `/Adm/HandUpdate` vrací 401 + `WWW-Authenticate: Basic` |
| `Admin_with_wrong_password_returns_401` | Špatné heslo → 401 |
| `Admin_with_correct_credentials_passes_auth` | Správné kreds → ne 401 (= prošlo auth do controlleru) |
| `Public_paths_do_not_require_auth` | `/` vrací 200 bez Authorization headeru |
| `Heartbeat_does_not_require_auth` | `/api/heartbeat` ne 401 (nutné pro cron-job.org) |

`TestWebApplicationFactory` má `TestAdminUsername = "test-admin"`, `TestAdminPassword = "test-pass"` injektované přes `AddInMemoryCollection`.

---

## Co je úmyslně **mimo scope**

| Položka | Důvod | Doporučená fáze |
|---|---|---|
| SRI pro amCharts CDN | Knihovna EOL, dočasné | Fáze 8 (replacement) |
| Nonce-based CSP (drop `unsafe-inline`) | Vyžaduje extrakci inline JS | Fáze 9 (TypeScript) |
| Anti-CSRF token review | `[ValidateAntiForgeryToken]` na `/Adm/HandUpdate` POST už je. Zbytek je GET. | — |
| Login UI / cookie auth | Basic Auth stačí pro 1-osobní administraci | Fáze 10 (volitelně) |
| Real OAuth/OIDC | Overkill pro tento projekt | — |
| `og:image` | Vyžaduje skutečný obrázek (nemá mě teď k dispozici) | Volitelně později |

---

## Workflow po deployi na staging

### 1. Nastavit AdminAuth credentials ve Forpsi panelu (env vars)
```
AdminAuth__Username=admin
AdminAuth__Password=<silné heslo, ideálně z password manageru>
```

### 2. Verifikace security headers
```bash
curl -I https://hydra2.dusanrysavy.cz/
# Očekávaná hlavička: X-Content-Type-Options: nosniff, ...
```

### 3. Verifikace admin auth
```bash
# Bez auth → 401
curl -I https://hydra2.dusanrysavy.cz/Adm

# S auth → 200
curl -I -u admin:<heslo> https://hydra2.dusanrysavy.cz/Adm
```

### 4. Verifikace cache headers
```bash
curl -I https://hydra2.dusanrysavy.cz/lib/jquery/jquery.min.js
# Očekávaná: Cache-Control: public,max-age=31536000,immutable
```

### 5. Online security scanner
- https://securityheaders.com/?q=hydra2.dusanrysavy.cz — měla by být **A nebo A+**
- https://observatory.mozilla.org/?q=hydra2.dusanrysavy.cz — měla by být **B+ nebo A**

---

## TL;DR

✅ Basic Auth na `/Adm/*` (nakonfigurovaný v env, jinak 503)
✅ 5 security headers + CSP s allowlistem amCharts CDN
✅ HSTS 1 rok, preload, includeSubDomains
✅ Cache-Control: 1 rok pro `/lib/`, 1 hodina pro vlastní `/css/`, `/js/`
✅ `<html lang="cs">`, theme-color, meta description, og:tags
✅ `aria-label` na navbar toggle (a11y bonus)
✅ Všechny CDN URL `https://`
✅ 12 nových testů, celkem 72/72 passing

**Otevřené body**: SRI a nonce-based CSP počkají na Fázi 8/9 (po nahrazení amCharts a extrakci inline JS).
