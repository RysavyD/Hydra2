# Hydra2 — Migrační roadmap

> Aktualizováno: 2026-04-29
> Status legenda: ✅ hotovo · ⏳ čeká · 📋 naplánováno · 💤 odloženo

---

## Stav

| # | Fáze | Status | Outcome |
|---|---|---|---|
| 0 | Inventarizace | ✅ | [phase-0-inventory.md](phase-0-inventory.md) |
| 1 | Migrace na .NET 8 | ✅ | [phase-1-net8.md](phase-1-net8.md), `src/`, `Hydra2.net8.sln` |
| 2 | Logování (Serilog tuning) | ✅ | [phase-2-logging.md](phase-2-logging.md) |
| 3 | Quartz refactor + smart heartbeat | ✅ | [phase-3-quartz.md](phase-3-quartz.md) |
| 4 | Testy (xUnit, snapshot scraperů, integration) | ✅ | [phase-4-tests.md](phase-4-tests.md) |
| 5 | HTTP hardening + bezpečnost | ✅ | [phase-5-hardening.md](phase-5-hardening.md) |
| 6 | Mobile + UX fixes | ✅ | [phase-6-mobile-ux.md](phase-6-mobile-ux.md) |
| 7 | amCharts 4 → 5 | ✅ | [phase-7-amcharts5.md](phase-7-amcharts5.md) |
| 8 | jQuery 3.7 + bundling | ✅ | [phase-8-jquery-bundling.md](phase-8-jquery-bundling.md) |
| 9 | TypeScript pro Graf + DevOps | ✅ | [phase-9-typescript.md](phase-9-typescript.md) |
| 10 | PWA + dark mode | ✅ | [phase-10-pwa-darkmode.md](phase-10-pwa-darkmode.md) |
| 11 | Bootstrap 5 + drop jQuery | ✅ | [phase-11-bootstrap5.md](phase-11-bootstrap5.md) |
| 12 | Cleanup — smazání starých projektů | 💤 | Po cutoveru staging → prod |

---

## Fáze 2 — Logování ⏳ (čeká na sample logu)

**Cíl:** snížit objem logů z ~100 MB/den na < 5 MB/den a získat strukturované logy přes Serilog.

**Co:**
- Analýza sample logu (10 MB stačí) — identifikovat top 5 zdrojů řádků
- Snížit `MinimumLevel.Default` na **`Warning`** v produkci, `Information` jen pro klíčové eventy (start/stop downloadu, počet vzorků)
- Vyhodit nebo agregovat per-station Info logy (`"Stanice: X"`, `"Načten config: Y"`)
- Retention 30 dní + komprese starších přes `Serilog.Sinks.File` config
- Endpoint **`/api/admin/logs/level`** s tokenovou autorizací — runtime přepnutí na Debug bez redeployu
- (Volitelně) Serilog 8 → 10 update spolu s tímhle (major bump, kontextované testem)

**Risk:** nízké — cílí jen na config, ne na business logiku
**Effort:** 1 den
**Závislost:** sample logu

---

## Fáze 3 — Quartz refactor + smart heartbeat 📋

**Cíl:** nahradit `while(true)` v `LastSpotsLoopAsync` za skutečný cron-based scheduler s persistencí.

**Co:**
- Quartz 3.x **`AdoJobStore`** — schedule a misfire stav v SQL (přežije app pool restart)
- Per-source jobs s **vlastními cron expressions** (`*/15 * * * *` apod.) v `appsettings.json`:
  - `chmi-job` — každých 15 min
  - `pvl-job`, `pla-job`, `pmo-job`, `poh-job` — separátní intervaly
- **Misfire policy** s recovery (po probuzení z idle dohonit 1× zmeškané, ne všechny)
- Rozšíření `/api/heartbeat` o:
  - `lastSuccess` per-source (CHMI, PVL, ...)
  - `pendingMisfires` count
  - HTTP 503 když `STALE` (cron-job.org se rozmluví)
- **Trigger endpoint** `/api/jobs/trigger/{source}?token=` — externí spuštění konkrétního zdroje (3. vrstva ochrany)
- Polly retry policy pro transient HTTP errory v BaseDownloader

**Risk:** střední — fundamentálně mění data flow
**Effort:** 2-3 dny
**Závislost:** Fáze 1 (hotovo), DB schema migrace pro Quartz tabulky

---

## Fáze 4 — Testy 📋

**Cíl:** safety net před dalšími změnami, zejména pro křehké HTML scrapery.

**Co:**
- **xUnit + FluentAssertions + NSubstitute** infrastruktura
- **Snapshot testy scraperů** — uložené `.html` z reálných odpovědí v `tests/Fixtures/{chmi,pvl,...}-YYYY-MM-DD.html`
  - Když se zdroj změní, test selže = včasná detekce
- Unit testy `BaseDownloader` parsing (decimal separators, datetime formats, edge cases)
- Unit testy `SampleTableName` (whitelist regex)
- **Integration testy** přes `WebApplicationFactory` + **Testcontainers SQL Server**:
  - Smoke test na každý kontroller
  - End-to-end heartbeat test
  - Round-trip AddSample + GetSamples
- **GitHub Actions** CI workflow: build + test při push (Forpsi deploy zůstává manuální)

**Risk:** nízké — žádný produkční dopad
**Effort:** 3-4 dny
**Závislost:** žádná, čím dřív tím lépe

---

## Fáze 5 — HTTP hardening + bezpečnost 📋

**Cíl:** quick wins, malé změny s velkým dopadem na bezpečnost a SEO.

**Co:**
- **`<html lang="cs">`** v `_Layout.cshtml`
- **`<meta name="theme-color" content="...">`** pro mobile browser chrome
- **`<meta name="description">`** + **og:image, og:description, og:title** — sociální preview
- **CSP headers** (Content-Security-Policy) — strict default, allowlist amCharts CDN
- **SRI (Subresource Integrity)** atributy pro CDN scripty (amCharts, lang JS):
  ```html
  <script src="https://www.amcharts.com/lib/4/core.js"
          integrity="sha384-..." crossorigin="anonymous"></script>
  ```
- **`X-Content-Type-Options: nosniff`**, **`Referrer-Policy: strict-origin-when-cross-origin`**, **`X-Frame-Options: SAMEORIGIN`** přes middleware
- **HSTS** i pro staging environment (jen ne pro Development)
- **Cache headers** pro `wwwroot/`:
  ```csharp
  app.UseStaticFiles(new StaticFileOptions
  {
      OnPrepareResponse = ctx =>
          ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable"
  });
  ```
- **Auth na `/Adm/*`** — alespoň token check (basic auth nebo cookie session)
- **`https://`** místo `//` u protocol-relative CDN URL

**Risk:** velmi nízké
**Effort:** ~半 dne
**Závislost:** žádná

---

## Fáze 6 — Mobile + UX fixes 📋

**Cíl:** odstranit konkrétní třecí body při použití na mobilu a tabletu.

**Co:**
- **Graf chart výška**: `height: 60vh; min-height: 300px;` místo fixní 600px (responsive)
- **Tabulka `dataTable`** wrap do `<div class="table-responsive">` — neutrhne layout
- **Větší touch targety** pro checkboxy "Sledované veličiny" (větší padding na `<label>`)
- **Replace `navbar-fixed-top` → `navbar-default`** (statický), nebo CSS fix pro klávesnici
- **Loading state v grafu** — místo bootbox modal použít skeleton SVG / CSS placeholder v `#chartdiv`
- **URL parametry**: `#riverId#spotId#...` → `?riverId=X&spotId=Y&start=...&stop=...&type=...` (klikatelné, lepší pro sdílení)
- **Smazat hardcoded `id=64`** v admin tlačítku
- **Accessibility audit**:
  - `<label>` k input fields (`#river`, `#spot`, `#h`, `#Q`, `#t`)
  - `aria-label` na navbar toggle button
  - Empty `<a href="#">` placeholdery → buttons / dynamicky vytvářené až s daty
  - Smazat `eventLoginClick = true;` (broken vinout v Graf/Index.cshtml)

**Risk:** nízké — UI parita zůstává, jen drobnosti
**Effort:** ~1.5 dne
**Závislost:** žádná

---

## Fáze 7 — amCharts 4 → 5 (nebo migrace na ECharts) 🔴

**Cíl:** opustit EOL chart knihovnu. amCharts 4 už nedostává security ani bug fixy.

**Co:**

### Varianta A — amCharts 4 → amCharts 5
- Ekvivalentní funkčnost (line chart, dual Y-axis, cursor, locale)
- API se změnilo: `am4core.create` → `am5.Root.new(...)`, atd.
- ~1 den práce, vyžaduje rewrite `CreateGraph()` a `createAxisAndSeries()`
- Stejné CDN distribuce, jen jiné soubory
- Free tier (s logem) i komerční

### Varianta B — Migrace na ECharts
- **Mnohem výkonnější** pro velké datasety (jako máš ty — `MaxJsonLength = int.MaxValue`)
- MIT licence — bez logu, bez omezení
- API jiné, ale dobře zdokumentované
- ~1.5 dne práce, ale dlouhodobě lepší

### Varianta C — Chart.js
- Nejjednodušší API
- Méně schopný pro pokročilé scénáře (multi-axis je fiddly)
- Lehčí (~60 KB)
- ~半 dne

**Můj návrh: B (ECharts)** — datasety mohou být velké, ECharts to zvládá lépe a license je permissivní. Ale pokud chceš minimální change, A (amCharts 5).

**Risk:** medium — ovlivňuje hlavní feature aplikace, nutno otestovat se starými daty
**Effort:** 1-1.5 dne
**Závislost:** Fáze 4 (testy) hotové — visual regression test ručně

---

## Fáze 8 — jQuery 3.7 + bundling 📋

**Cíl:** odstranit XSS-vulnerable jQuery 1.10.2 a začít používat bundling/minification.

**Co:**
- **jQuery 1.10.2 → 3.7.1** — drop-in v 95 % případů
  - Pozor na `$.browser` (odstraněno v 3.x), `.bind()/.live()` (deprecated)
  - V Hydra2 je jen `$.ajax`, `.val()`, `.click()`, `.prop()`, `.text()`, `.attr()`, `.on()`, `.each()` — vše funguje
  - Test: smoke test po deployi staging
- **jQuery Migrate** přechodně, pokud něco selže
- **WebOptimizer** integrace:
  ```csharp
  builder.Services.AddWebOptimizer(pipeline =>
  {
      pipeline.AddCssBundle("/css/site.min.css", "lib/bootstrap/css/*.css", "css/*.css");
      pipeline.AddJavaScriptBundle("/js/site.min.js", "lib/jquery/*.js", "lib/bootbox/*.js", "js/*.js");
  });
  ```
- Cache-busting přes content hash v URL (WebOptimizer to dělá automaticky)
- (Volitelně) ESM moduly pro Hydra2.js (`<script type="module">`)

**Risk:** medium — jQuery změna může něco rozbít
**Effort:** ~1 den + smoke test
**Závislost:** Fáze 6 (UX) hotová, Fáze 7 (amCharts) ideálně dřív — méně mixování změn naráz

---

## Fáze 9 — TypeScript pro Graf + DevOps 📋

**Cíl:** moderní vývojářský komfort pro novější JS kód a CI/CD pipeline.

**Co:**
- **TypeScript** jen pro `wwwroot/js/Hydra2.js` a JS v `Graf/Index.cshtml` — extrahovat z Razor do `wwwroot/ts/graf.ts`
- **Vite** nebo **esbuild** build pipeline → `wwwroot/js/graf.min.js`
- `npm run build` jako prerequisite pro `dotnet publish` (přes Target v csproj)
- Razor view jen `<script src="~/js/graf.min.js"></script>` — bez inline JS
- **GitHub Actions** workflow:
  - `dotnet test` při PR
  - `npm run build` + `dotnet publish` při merge do main
  - (Volitelně) artifact upload pro snadný FTP deploy
- **`.editorconfig`** pro konzistentní formátování v IDE

**Risk:** nízké — TS je opt-in, nemusí pokrýt všechen JS
**Effort:** ~2 dny
**Závislost:** Fáze 7 (amCharts) ideálně dřív — nevyplatí se psát TS pro kód, který se za týden přepíše

---

## Fáze 10 — Volitelná vylepšení 💤

Tyto věci nejsou kritické, ale dělají Hydru moderní:

- **PWA manifest** (`manifest.webmanifest`) — uživatelé si přidají Hydru jako ikonu na home screen
- **Service worker** pro offline cache základních stránek
- **Dark mode** přes CSS custom properties + `prefers-color-scheme: dark` (vyžaduje Bootstrap 5 nebo custom CSS)
- **Bootstrap 3 → 5** — moderní grid (flexbox/CSS grid), 0 jQuery dependency, native dark mode
  - Nezachovává UI 1:1 — uživatelé uvidí drobné rozdíly v paddingu, fontech, barvách
- **Highcharts/ECharts** server-side rendering pro SEO (chart fallback obrázky)
- **OpenTelemetry** + Grafana Cloud free tier pro distributed tracing

**Risk:** záleží na položce (BS5 vysoké, PWA nízké)
**Effort:** 1-3 dny záleží co
**Závislost:** Fáze 9 (TypeScript) vhodné

---

## Fáze 11 — Bootstrap 5 + drop jQuery 📋

**Cíl:** Modernizovat frontend stack — odstranit jQuery dependency, přejít na Bootstrap 5 s native dark mode a vanilla-JS API. Přínos: menší bundle, žádné jQuery security vulnerabilities, modernější developer experience.

**Co:**

### A) Bootstrap 3.4.1 → Bootstrap 5.3.x
- `bootstrap.min.css` + `bootstrap.bundle.min.js` (含 Popper) — self-hosted v `wwwroot/lib/bootstrap/`
- Custom Sass build pro zachování brandových barev (`#1a3d6e`) — nebo použít CSS custom properties overrides
- Renamed utility classes:
  - `.pull-right` → `.float-end`, `.pull-left` → `.float-start`
  - `.text-right` → `.text-end`, `.text-left` → `.text-start`
  - `.hidden-xs` → `.d-none .d-sm-block` (responsive utility renaming)
- Renamed component data attrs:
  - `data-toggle` → `data-bs-toggle`
  - `data-target` → `data-bs-target`
  - `data-dismiss` → `data-bs-dismiss`
- Removed components → custom CSS shim:
  - **`.jumbotron`** → `<div class="bg-light p-5 rounded">` v Home/Index, About, Contact
  - **`.panel-default`/`.panel-body`** → `.card` (pokud někde použité)
  - **`.btn-default`** → `.btn-secondary`
- Navbar:
  - `.navbar-inverse` → `.navbar-dark .bg-dark`
  - `.navbar-toggle` → `.navbar-toggler`
  - `.collapse.navbar-collapse` zachovat, ale přepsat data-bs-* atributy

### B) Glyphicons → Bootstrap Icons
- BS5 odebral glyphicons. Místa použití (Graf/Index.cshtml):
  - `<i class="glyphicon glyphicon-calendar">` (kalendářová ikona u datepickeru) → `<i class="bi bi-calendar-event">`
- Self-hosted `bootstrap-icons.css` + `fonts/bootstrap-icons.woff2` v `wwwroot/lib/bootstrap-icons/`

### C) Drop jQuery (1.10.2 → 3.7.1 → ZERO)
**Předpoklad**: nahradit všechny jQuery callsity v kódu.

#### graf.ts (~300 jQuery-ových volání)
- `$(...).val()` → `(elem as HTMLInputElement).value`
- `$(...).text(x)` → `elem.textContent = x`
- `$(...).attr("href", x)` → `elem.setAttribute("href", x)` nebo `(elem as HTMLAnchorElement).href = x`
- `$(...).on("click", fn)` → `elem.addEventListener("click", fn)`
- `$(...).hide()/.show()` → `elem.style.display = "none"/"block"` nebo CSS class
- `$(...).empty()` → `elem.replaceChildren()` nebo `elem.innerHTML = ""`
- `$(...).append(html)` → `elem.insertAdjacentHTML("beforeend", html)`
- `$.each(arr, fn)` → `arr.forEach(fn)` nebo `for (const item of arr)`
- `$.isNumeric(x)` → `!isNaN(Number(x))` nebo `Number.isFinite(parseFloat(x))`
- `$.ajax({...})` → `fetch(url, {...})` s `await response.json()`
  - GET `?spot=1&start=...` → `new URLSearchParams({...}).toString()`
- `$(document).ready(fn)` → `document.addEventListener("DOMContentLoaded", fn)`

#### Hydra2.js
- Theme toggle už je vanilla JS (žádný jQuery use)
- ShowWaitDialog/HideWaitDialog používají bootbox → nahradit (viz E)

### D) Daterangepicker → Flatpickr (vanilla, no jQuery)
**Současný daterangepicker** je jQuery plugin. Nahrazení:

- **[Flatpickr](https://flatpickr.js.org/)** — vanilla JS, ~17 KB gzipped, czech locale (`cs.js`), single-date mode
- Self-hosted v `wwwroot/lib/flatpickr/`
- API:
  ```typescript
  flatpickr("#start", {
      dateFormat: "d/m/Y",
      locale: "cs",
      altInput: true,
      altFormat: "d. F Y"
  });
  ```
- Po změně data: `onChange: (selectedDates, dateStr) => { state.start = dateStr; writeUrlState(); }`

### E) Bootbox → custom dialog helper / native `<dialog>`
**Bootbox** je jQuery plugin pro modal alerty. Použito jen `ShowWaitDialog`/`HideWaitDialog` v `Hydra2.js` (ale Graf už používá `chart-skeleton` overlay od Fáze 6, takže prakticky nepoužito).

→ **Odstranit bootbox úplně**. ShowWaitDialog/HideWaitDialog převést na no-op nebo smazat references.

Pokud bude potřeba dialog v budoucnu, použít HTML5 `<dialog>` element (nativní, podporovaný všude od 2022).

### F) CSS úpravy
- **`style-src 'unsafe-inline'`** lze nakonec dropnout v CSP — Bootstrap 5 nepoužívá inline style atributy v komponentách. Audit zbylých `style="..."` v Razor views (~12 míst).
- BS5 má **native dark mode** (`data-bs-theme="dark"`) — **nahradí** mé custom CSS variables ve Fázi 10. Přepsat `theme-init.js` aby nastavoval `data-bs-theme` místo `data-theme`. Toggle v Hydra2.js stejně.
- Drop CSS custom properties (`--bg`, `--text`, …), použít BS5 utility classes a tokens

### G) WebOptimizer bundle — drop jQuery
```diff
  pipeline.AddJavaScriptBundle("/js/site.bundle.js",
-     "/lib/jquery/jquery.min.js",
-     "/lib/bootbox/bootbox.min.js",
      "/lib/bootstrap/js/bootstrap.bundle.min.js",
      "/js/Hydra2.js");
```

→ Bundle se zmenší ze ~144 KB na ~80 KB. jQuery 87 KB ušetřeno.

### H) Tests
- `WebOptimizerBundleTests`: aktualizovat — bundle už neobsahuje "jQuery" / "v3.7" markers, ale OBSAHUJE `bootstrap.Modal` nebo "Popper"
- `Csp_script_src_does_not_allow_unsafe_inline` zůstává platný
- **NOVÝ** test `Csp_style_src_does_not_allow_unsafe_inline` (lze přidat až po auditu inline `style=""`)
- Visual regression manuální check page-by-page (Home, Adm/Index, Adm/SpotOverView, Graf, About, Contact, HandUpdate, GetStationOverView)

**Risk:** **medium-high** — největší frontend změna v celé migraci. Bez visual regression testů je snadné přehlédnout drobnosti (padding, font-size, focus styles). Doporučený postup:
- **Page-by-page** přístup: nejdřív Home (jednoduché), pak Adm (admin, snese drobnosti), nakonec Graf (kritická feature)
- Side-by-side comparison na staging před produkcí
- 2 týdny "burn-in" period na stagingu
- Připravit jasný rollback plán (`git revert` nebo branch switch)

**Effort:** 4-6 dní + 2 dny smoke testing
- BS3 → BS5 CSS/JS: 1-2 dny
- Glyphicons → Bootstrap Icons: 0.5 den
- jQuery removal v graf.ts: 1-1.5 den
- Daterangepicker → Flatpickr: 1 den
- Bootbox removal: 0.5 den
- BS5 native dark mode adoption: 0.5 den
- CSS audit + cleanup `style="..."` atributů: 0.5 den
- Visual regression manual: 1-2 dny

**Závislost:** Fáze 7 (amCharts 5) hotová — chart už nepoužívá jQuery. Fáze 9 (TypeScript) hotová — graf.ts je dobře strukturovaný pro vanilla JS rewrite.

**Bonusy po dokončení:**
- jQuery 87 KB out of bundle → **rychlejší first paint na mobilu**
- Native dark mode přes `data-bs-theme` — kratší CSS, méně vlastní logiky
- BS5 forms mají lepší accessibility defaults (focus states, labels)
- Možné přidat `column-gap`, `row-gap` v gridu (BS5 utility)
- Připraveno na případné CSS Modules / Tailwind refactor v budoucnu

---

## Fáze 12 — Cleanup po cutoveru 💤

**Cíl:** smazat starou .NET Framework verzi po úspěšném cutoveru staging → prod.

**Co:**
- Odstranit ze solution: `Hydra2.Web/`, `Hydra2.Service/`, `Hydra2.Downloaders/`, `Hydra2.Model/`, `Hydra.DownLoad/`, `SampleTableSeparator/` (zachovat?)
- Smazat `Hydra2.sln` (nebo přejmenovat `Hydra2.net8.sln` → `Hydra2.sln`)
- Smazat `packages/` adresář (NuGet legacy)
- Smazat `Web.Release.config` (s odkazem na Smarterasp.net)
- Aktualizovat `README.md`
- Závěrečný `git tag v2.0.0`

**Trigger:** po 2-4 týdnech stabilního běhu nové verze v produkci.

---

## TL;DR — kde teď jsme

```
✅ Fáze 0 – 11   hotovo a pushnuto (15+ commitů, 82/82 testů)
💤 Fáze 12       cleanup starých .NET Framework projektů
```

**Doporučené pořadí**: po stabilním běhu Fáze 0–10 v produkci (~2 týdny) → **Fáze 11** → po dalších 2 týdnech bez regresí → **Fáze 12** (cleanup).

**Zranitelnost**: žádné aktivní security vulns. jQuery 3.7.1 je čistý, ale stále +87 KB v bundle, který by Fáze 11 odstranila.
