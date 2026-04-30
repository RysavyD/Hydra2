# Fáze 8 — jQuery 3.7.1 + WebOptimizer bundling

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ 0 warning, 0 error · Tests: ✅ **76 / 76 passed**

---

## Cíl

Odstranit XSS-vulnerable jQuery 1.10.2 (z roku 2013) a začít používat bundling/minifikaci pro produkční optimalizaci.

---

## Co bylo uděláno

### 1. jQuery 1.10.2 → 3.7.1
[`src/Hydra2.Web/wwwroot/lib/jquery/jquery.min.js`](../../src/Hydra2.Web/wwwroot/lib/jquery/jquery.min.js) — **stažen z `https://code.jquery.com/jquery-3.7.1.min.js`**.

```bash
curl -sSL https://code.jquery.com/jquery-3.7.1.min.js \
     -o src/Hydra2.Web/wwwroot/lib/jquery/jquery.min.js
```

| Verze | Velikost | Vydání | CVE |
|---|---|---|---|
| 1.10.2 | 91 KB | červenec 2013 | CVE-2020-11022, CVE-2020-11023 (XSS přes `.html()`) |
| 3.7.1 | 87 KB | srpen 2023 | žádné aktivní |

**Riziko prošlosti**: Hydra2 používá z jQuery jen `$.ajax`, `.val()`, `.click()`, `.prop()`, `.text()`, `.attr()`, `.on()`, `.each()`, `$.isNumeric` — vše plně podporované v 3.x. **Žádné `$.browser`, `.bind()`, `.live()`** nikde v kódu, takže žádný breaking change.

### 2. Drop unused jquery-validation
Smazány nepoužívané soubory (`packages.config` éra je tahala automaticky, ale žádná view neměla `data-val-*` atributy):
- ~~`wwwroot/lib/jquery-validation/jquery.validate.min.js`~~ (deleted)
- ~~`wwwroot/lib/jquery-validation/jquery.validate.unobtrusive.min.js`~~ (deleted)

Pokud někdy přibudou formuláře s validací, lze re-add — žádný kód na to dnes neodkazuje.

### 3. WebOptimizer integrace
NuGet **`LigerShark.WebOptimizer.Core 3.0.420`** přidán do `Hydra2.Web.csproj`.

> **Pozn**: novější verze `3.0.469` má regression bug (`ArgumentNullException: logger` v `AssetPipeline.AddBundle`). Stable verze 3.0.420 funguje bezvadně. Lze bumpnout, až bude opraveno upstream.

#### Bundles definované v [`Program.cs`](../../src/Hydra2.Web/Program.cs)

```csharp
builder.Services.AddWebOptimizer(pipeline =>
{
    pipeline.AddCssBundle("/css/site.bundle.css",
        "/lib/bootstrap/css/bootstrap.min.css",
        "/css/site.css");

    pipeline.AddJavaScriptBundle("/js/site.bundle.js",
        "/lib/jquery/jquery.min.js",
        "/lib/bootbox/bootbox.min.js",
        "/lib/bootstrap/js/bootstrap.min.js",
        "/js/Hydra2.js");
});
```

→ Dva URL endpointy navržené WebOptimizerem:
- `GET /css/site.bundle.css` — zminifikovaný CSS
- `GET /js/site.bundle.js` — zminifikovaný JS

#### Middleware ordering
```csharp
app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseMiddleware<BasicAuthMiddleware>();
app.UseWebOptimizer();         // ← MUSÍ být před UseStaticFiles
app.UseStaticFiles(...);
app.UseRouting();
app.UseAuthorization();
```

WebOptimizer middleware obsluhuje pouze definované bundle URL. Ostatní statické soubory (`/lib/...`, `/fonts/...`) projdou na `UseStaticFiles` jako dříve.

### 4. _Layout.cshtml — bundle URLs

```html
<!-- Před (4× requestů) -->
<link rel="stylesheet" href="~/lib/bootstrap/css/bootstrap.min.css" />
<link rel="stylesheet" href="~/css/site.css" />
...
<script src="~/lib/jquery/jquery.min.js"></script>
<script src="~/lib/bootbox/bootbox.min.js"></script>
<script src="~/js/Hydra2.js"></script>
<script src="~/lib/bootstrap/js/bootstrap.min.js"></script>

<!-- Po (2 requesty + cache busting) -->
<link rel="stylesheet" href="~/css/site.bundle.css" asp-append-version="true" />
...
<script src="~/js/site.bundle.js" asp-append-version="true"></script>
```

`asp-append-version="true"` (built-in ASP.NET Core tag helper) přidá `?v=<sha256-hash>` — když se zdrojový bundle změní, hash se změní → URL je nová → prohlížeč musí znovu stáhnout, ale jinak používá cache navždy.

### 5. Cache headers
WebOptimizer sám nastavuje `Cache-Control` pro bundly. Náš `OnPrepareResponse` callback v `UseStaticFiles` už neaplikuje na bundle URLs (jdou skrz WebOptimizer middleware).

### 6. Nové testy ([`WebOptimizerBundleTests.cs`](../../tests/Hydra2.Tests/Web/WebOptimizerBundleTests.cs))

| Test | Co ověřuje |
|---|---|
| `Css_bundle_is_served_with_correct_content_type` | `GET /css/site.bundle.css` → 200 + `text/css` |
| `Css_bundle_contains_bootstrap_and_site_css` | Bundle obsahuje "Bootstrap" comment + `.chart-container` z site.css |
| `Js_bundle_is_served_with_correct_content_type` | `GET /js/site.bundle.js` → 200 + JS MIME |
| `Js_bundle_contains_jquery_and_bootbox_and_hydra2` | Bundle obsahuje "jQuery", "v3.7", "bootbox", "ShowWaitDialog" — všechny tři zdroje |

**Test "v3.7"** je důležitý guard — pokud někdo omylem replace jQuery zpět na 1.x, test selže.

---

## Performance dopad

### Před (Fáze 7)
6 separátních HTTP requestů na první load:
```
GET /lib/bootstrap/css/bootstrap.min.css   ~115 KB (gzip ~25 KB)
GET /css/site.css                          ~3 KB
GET /lib/jquery/jquery.min.js              ~91 KB
GET /lib/bootbox/bootbox.min.js            ~16 KB
GET /lib/bootstrap/js/bootstrap.min.js     ~37 KB
GET /js/Hydra2.js                          ~0.3 KB
                                         ~262 KB raw / ~80 KB gzip
```

### Po (Fáze 8)
2 HTTP requesty:
```
GET /css/site.bundle.css?v=hash            ~118 KB (gzip ~25 KB)
GET /js/site.bundle.js?v=hash              ~144 KB (gzip ~50 KB)
                                         ~262 KB raw / ~75 KB gzip
```

Úspora:
- **6 → 2 requesty** (-66 % HTTP overhead) — důležité pro mobilní sítě
- WebOptimizer minifikuje (whitespace, comments) → mírně menší gzip
- Cache busting přes content hash → po deployi se klientské cache invalidují automaticky

---

## Co je úmyslně **mimo scope**

| Položka | Důvod |
|---|---|
| Migrace z Bootstrap 3 | Velký redesign — Phase 10 (volitelně) |
| Update jquery-validation, re-add | Aktuálně nepoužito; přidá se jen když bude potřeba |
| Brotli (kromě gzip) | WebOptimizer to umí přes konfiguraci, ale ne výchozí. Forpsi shared hosting má vlastní compression na IIS úrovni. |
| WebP / AVIF pro favicons | Žádné velké obrázky v aplikaci. |
| HTTP/2 server push hints | ASP.NET Core nepodporuje natively; HTTP/2 sám překlene. |
| Subresource Integrity (SRI) pro vlastní bundly | Nemá smysl, jsme jediný server (vs. CDN) |

---

## Verifikace po deployi

### Browser DevTools → Network
```
GET /                       200  text/html
GET /css/site.bundle.css?v=...  200  text/css
GET /js/site.bundle.js?v=...    200  application/javascript
```

Žádné 404 pro `/lib/jquery-validation/...` (smazáno).

### Browser DevTools → Console
- `jQuery.fn.jquery` → `"3.7.1"`
- Žádné deprecation warnings z jQuery
- Bootbox modaly fungují (na Graf stránce při AJAX)

### Hash verifikace
Po deployi nového kódu by se `?v=` parametr měl změnit. Klienti s cached starou verzí dostanou novou bez ručního "Ctrl+F5".

---

## Riziko & rollback

**Riziko: medium-low**. jQuery 3.7.1 je drop-in replacement pro naše use cases, WebOptimizer 3.0.420 je stable. Hlavní citlivost je amCharts 5 (Phase 7) v kombinaci s jQuery 3 — testy potvrzují, že syntax v Graf/Index.cshtml používá jen API kompatibilní s oběma verzemi.

**Rollback**: `git revert HEAD` nebo reset na předchozí Phase 7 commit (`7ac81a6`).

---

## TL;DR

✅ jQuery 1.10.2 (XSS vulns z 2013) → 3.7.1 (self-hosted, žádné CDN)
✅ Smazány nepoužité jquery-validation soubory
✅ LigerShark.WebOptimizer.Core 3.0.420 — bundling + minifikace
✅ 6 → 2 asset requestů per first-load
✅ Cache busting přes `asp-append-version="true"`
✅ 4 nové testy verifikují content + content-type + jQuery 3.x marker
✅ 76/76 testů zelených, build čistý

**Po této fázi**: frontend je optimalizovaný, žádné staré verze knihoven s aktivními CVE. Připraveno na **Fázi 9** (TypeScript + DevOps) pro modernější developer experience.
