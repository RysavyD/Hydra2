# Hydra2 — Migrační roadmap

> Aktualizováno: 2026-04-29
> Status legenda: ✅ hotovo · ⏳ čeká · 📋 naplánováno · 💤 odloženo

---

## Stav

| # | Fáze | Status | Outcome |
|---|---|---|---|
| 0 | Inventarizace | ✅ | [phase-0-inventory.md](phase-0-inventory.md) |
| 1 | Migrace na .NET 8 | ✅ | [phase-1-net8.md](phase-1-net8.md), `src/`, `Hydra2.net8.sln` |
| 2 | Logování (Serilog tuning) | ⏳ | Čeká na sample produkčního logu |
| 3 | Quartz refactor + smart heartbeat | 📋 | |
| 4 | Testy (xUnit, snapshot scraperů, integration) | 📋 | |
| 5 | HTTP hardening + bezpečnost | 📋 | |
| 6 | Mobile + UX fixes | 📋 | |
| 7 | jQuery + bundling | 📋 | |
| 8 | amCharts 4 → 5 (nebo ECharts) | 📋 | |
| 9 | TypeScript pro Graf + DevOps | 📋 | |
| 10 | Volitelná vylepšení (PWA, dark mode, BS5) | 💤 | |
| 11 | Cleanup — smazání starých projektů | 💤 | Po cutoveru staging → prod |

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

## Fáze 7 — jQuery 3.7 + bundling 📋

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
**Závislost:** Fáze 6 (UX) doporučená nejdřív (méně koliduje), Fáze 4 (testy) ideálně před tímto

---

## Fáze 8 — amCharts 4 → 5 (nebo migrace na ECharts) 🔴

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
**Závislost:** Fáze 4 (testy) doporučené, ale i bez — visual regression test ručně

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
**Závislost:** Fáze 8 (amCharts) ideálně dřív — nevyplatí se psát TS pro kód, který se za týden přepíše

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

## Fáze 11 — Cleanup po cutoveru 💤

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
✅ Fáze 0 + 1     hotovo (commit pushed)
⏳ Fáze 2        čeká na sample logu (slíbeno)
📋 Fáze 3        Quartz + smart heartbeat
📋 Fáze 4        testy
📋 Fáze 5        HTTP hardening (rychlé wins)
📋 Fáze 6        mobile + UX fixes
📋 Fáze 7        jQuery + bundling
🔴 Fáze 8        amCharts 4 → 5/ECharts (kritická knihovna EOL)
📋 Fáze 9        TypeScript + CI/CD
💤 Fáze 10       volitelná modernizace (PWA, dark mode, BS5)
💤 Fáze 11       cleanup starých projektů
```

**Doporučené pořadí**: 2 → 3 → 4 → **5 → 6 → 8** (UX + critical chart) → **7 → 9** (modernization).
**Možno paralelně**: Fáze 4 (testy) lze dělat kdykoliv mimo hlavní linku.
**Zranitelnost**: amCharts 4 (Fáze 8) je jediná kritická součást, jinak žádné security vulns aktivně otevřené.
