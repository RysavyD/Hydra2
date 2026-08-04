# Zadání pro Fázi 11 — Bootstrap 5 + drop jQuery

> Tento dokument je **samostatné zadání** pro novou session. Agent, který ho dostane,
> nemá kontext předchozí konverzace — vše potřebné je zde nebo v odkazovaných souborech.
>
> **Nejdřív si přečti:** [roadmap.md](roadmap.md) (sekce „Fáze 11") a
> [phase-9-typescript.md](phase-9-typescript.md) (jak funguje frontend build).

---

## 1. Kontext projektu (stručně)

**Hydra2** je český systém pro stahování a vizualizaci hydrologických dat (vodní stavy,
průtoky, teploty řek a nádrží; zdroje ČHMÚ, PVL, PLA, PMO, POH). Probíhá migrace
z .NET Framework 4.6.1 (ASP.NET MVC 5) na **.NET 8** (ASP.NET Core MVC).

**Aktuální stack (.NET 8 verze v `src/`):**
- Backend: ASP.NET Core MVC 8, Dapper + Microsoft.Data.SqlClient, Quartz 3, Serilog
- Frontend: **Bootstrap 3.4.1**, **jQuery 3.7.1**, amCharts 5 (CDN), daterangepicker (jQuery plugin),
  bootbox (jQuery plugin), moment.js, WebOptimizer bundling
- Build frontend TS: `frontend/` (esbuild → `wwwroot/js/graf.min.js`)

**Hosting (DŮLEŽITÉ omezení):** Forpsi shared Windows hosting. **Žádný Node na serveru** →
buildené JS bundly (`graf.min.js`, `graf.min.js.map`) se **commitují do gitu**. Deploy je
`dotnet publish` + FTP. Žádný Docker, žádný přístup k IIS configu.

**Kde jsme:** Fáze 0–10 hotové (14+ commitů), na větvi `net8-migration`, **82/82 testů** zelených,
build čistý. Stará .NET Framework verze zůstává netknutá v root složkách (`Hydra2.Web/`,
`Hydra2.Service/`, atd.) — smaže se až ve Fázi 12 po cutoveru.

---

## 2. ⚠️ KRITICKÁ PAST — camelCase JSON (přečti než sáhneš na AJAX)

ASP.NET Core používá **System.Text.Json s camelCase** naming policy (default).
Starý ASP.NET MVC 5 používal Newtonsoft s **PascalCase** (property names 1:1).

To znamená, že C# model:
```csharp
public class Sample { public string Date; public float? h; public float? Q; public float? t; }
```
se serializuje jako `{ "date": "...", "h": ..., "q": ..., "t": ... }` — **`Date`→`date`, `Q`→`q`**.

Tohle už **jednou způsobilo bug** (datumy se nezobrazovaly v tabulce pod grafem; commit `dc4554f`
to opravil na `item.date`/`item.q` v `graf.ts`). **Fáze 11 přepisuje `$.ajax` → `fetch`,
takže se toho dotkne znovu.** Při rewritu:
- Vlastnosti odpovědi z `/Graf/GetData` a `/Graf/GetSpots` jsou **camelCase**
  (`date`, `h`, `q`, `t`, `spot.spa0`, `spot.link`, `spot.raftLink`, `spot.type`).
- `/Graf/GetSpots` vrací pole `{ id, name }` (už lowercase, anonymní typ).
- Po přepisu **ověř v prohlížeči Network tab**, že klíče v JSON odpovídají tomu, co čte JS.

---

## 3. Mise Fáze 11

Modernizovat frontend: **Bootstrap 3 → Bootstrap 5**, **úplně odstranit jQuery**
(1.10→3.7 už proběhlo, teď na nulu), nahradit jQuery pluginy vanilla alternativami,
adoptovat BS5 native dark mode. Cíl: menší bundle (−~87 KB jQuery), moderní stack,
žádné jQuery security exposure.

Detailní rozpis A–H je v [roadmap.md](roadmap.md) → sekce „Fáze 11". Shrnutí:

| Část | Co | Klíčové soubory |
|---|---|---|
| **A** | BS3 → BS5.3 (self-hosted CSS+JS bundle vč. Popper) | `wwwroot/lib/bootstrap/`, všechny Views |
| **B** | Glyphicons → Bootstrap Icons | `Graf/Index.cshtml` (2× `glyphicon-calendar`) |
| **C** | Drop jQuery v `graf.ts` (`$` → vanilla DOM, `$.ajax` → `fetch`) | `frontend/src/graf.ts` |
| **D** | daterangepicker → **Flatpickr** (vanilla) | `graf.ts`, `wwwroot/lib/flatpickr/` |
| **E** | Odstranit bootbox (prakticky nepoužité od Fáze 6) | `wwwroot/js/Hydra2.js`, bundle |
| **F** | BS5 native dark mode (`data-bs-theme`) místo custom CSS variables z Fáze 10 | `theme-init.js`, `Hydra2.js`, `site.css` |
| **G** | Vyhodit jQuery+bootbox z WebOptimizer bundle | `Program.cs` |
| **H** | Testy + manuální visual regression | `tests/Hydra2.Tests/Web/` |

---

## 4. Konkrétní místa k úpravě (inventura)

**BS3-specifické třídy nalezené v kódu (musí se přejmenovat/nahradit):**
- `_Layout.cshtml:25` — `navbar navbar-inverse navbar-fixed-top` → BS5 `navbar navbar-dark bg-dark fixed-top`
- `_Layout.cshtml:28` — `navbar-toggle` + `data-toggle="collapse"` + `data-target` → `navbar-toggler` + `data-bs-toggle` + `data-bs-target` (+ BS5 hamburger markup `<span class="navbar-toggler-icon">`)
- `Graf/Index.cshtml:32,37` — `glyphicon glyphicon-calendar fa fa-calendar` → `<i class="bi bi-calendar-event">` (pozn.: `fa fa-calendar` je mrtvý, FontAwesome se nikde neloaduje)
- `Home/Index.cshtml:5`, `Home/About.cshtml:5`, `Home/Contact.cshtml:4` — `jumbotron` (v BS5 odstraněn) → `<div class="bg-light p-5 rounded">` nebo custom `.hero` shim
- `Adm/Index.cshtml:18` — `btn btn-default` → `btn btn-secondary`
- Grid třídy `col-md-3 col-sm-6` fungují v BS5 stejně (flexbox), ale ověř chování

**jQuery callsity k přepisu na vanilla** (viz roadmap část C — kompletní mapping
`$.ajax`→`fetch`, `.val()`, `.text()`, `.attr()`, `.on()`, `.hide()/.show()`, `$.each`,
`$.isNumeric`, `$(document).ready`). Hlavní soubor: `frontend/src/graf.ts` (~350 řádků,
dobře strukturovaný — po Fázi 9). Menší: `wwwroot/js/Hydra2.js` (theme toggle je už
vanilla, jen `ShowWaitDialog`/`HideWaitDialog` používají bootbox).

**Inline `style="..."` atributy** (blokují budoucí drop `style-src 'unsafe-inline'`):
kalendářové ikony v `Graf/Index.cshtml` a pár míst v tabulkách. Přesun do `site.css`
je nice-to-have — pokud stihneš, přidej i test `Csp_style_src_does_not_allow_unsafe_inline`
a dropni `'unsafe-inline'` ze `style-src` v `SecurityHeadersMiddleware.cs`. Jinak nech na později.

---

## 5. Pracovní konvence (dodržet)

**Build & test:**
```bash
dotnet build Hydra2.net8.sln          # musí být 0 warning, 0 error
dotnet test Hydra2.net8.sln           # všechny testy zelené (teď 82/82)
```

**Frontend TS build (po každé změně `graf.ts`):**
```bash
cd frontend
npm install            # jednorázově
npm run build          # → ../src/Hydra2.Web/wwwroot/js/graf.min.js (+ .map)
npm run typecheck      # tsc --noEmit
```
⚠️ **Vždy commitni i přebuildený `graf.min.js` + `.map`** (Forpsi nemá Node). Zdroj bez
rebuildu = fix není live (to byl přesně ten date bug — TS opraven, ale bundle stale).

**Commity:**
- České commit messages, stejný styl jako předchozí fáze (`Fáze 11: ...` + odrážky)
- Každá fáze má svůj doc `docs/migration/phase-N-*.md` s TL;DR a verifikací — vytvoř
  `phase-11-bootstrap5.md`
- Aktualizuj status v `roadmap.md` (řádek Fáze 11 na ✅)
- **`.github/workflows/ci.yml` NELZE pushnout** — token nemá `workflow` scope. Když ho
  změníš, commituj vše ostatní bez něj (`git commit -- ':(exclude).github/workflows/ci.yml'`
  nebo ho nech untracked) a řekni uživateli, ať ho pushne ručně.
- Push na `origin/net8-migration` po dokončení a zelených testech.

**Postup — page-by-page (kvůli riziku):**
1. Nejdřív `_Layout.cshtml` + BS5 CSS/JS swap + navbar (základ pro vše)
2. `Home/*` (jednoduché, jumbotron shim)
3. `Adm/*` (admin, snese drobnosti)
4. `Graf/Index.cshtml` + `graf.ts` rewrite (kritická feature — nejvíc opatrnosti)
5. daterangepicker → Flatpickr, bootbox removal, dark mode adopce
6. Bundle cleanup + testy

---

## 6. Akceptační kritéria (definition of done)

- [ ] `dotnet build` — 0 warning, 0 error
- [ ] `dotnet test` — všechny zelené (uprav testy, které referencují jQuery/bootbox markery
      v bundlu — např. `WebOptimizerBundleTests` čeká „jQuery"/„v3.7")
- [ ] **Žádný jQuery** nikde: ne v `graf.ts`, ne v `Hydra2.js`, ne ve WebOptimizer bundlu,
      ne jako `<script>` v Layoutu. `grep -ri "jquery" src/Hydra2.Web` vrátí jen historické/nic.
- [ ] `graf.min.js` přebuildovaný a commitnutý; `npm run typecheck` čistý
- [ ] Graf funguje: 3 série (Hladina/Průtok/Teplota), zoom/pan, legenda, tabulka pod grafem
      **ukazuje datumy** (pozor na camelCase — část 2!)
- [ ] Dark mode přes BS5 `data-bs-theme`, toggle auto/light/dark s localStorage persistencí,
      žádný FOUC (theme-init v `<head>`)
- [ ] Mobilní hamburger menu funguje (BS5 collapse, vanilla)
- [ ] Datepicker funguje (Flatpickr, český locale, formát DD/MM/YYYY konzistentní s ParseDateTime
      na serveru — `GrafController.ParseDateTime` čeká `dd/MM/yyyy`!)
- [ ] Bundle menší (dolož číslo v phase-11 docu)
- [ ] Manuální visual check: Home, Adm/Index, Adm/SpotOverView, Adm/HandUpdate,
      Adm/GetStationOverView, Graf, About, Contact

---

## 7. Rizika & rollback

**Riziko: medium-high** — největší frontend změna v migraci. Bez visual regression testů
se snadno přehlédne kosmetika (padding, fonty, focus). Doporučení:
- Postupuj page-by-page (viz část 5), commituj po logických celcích
- Před produkcí side-by-side srovnání na stagingu (https://hydra2.dusanrysavy.cz)
- 2týdenní burn-in na stagingu
- **Rollback:** `git revert <commit>` nebo návrat na `dc4554f` (poslední stav před Fází 11)

**Časté chytáky:**
- Datepicker formát × server `ParseDateTime` (`dd/MM/yyyy`) — musí sedět
- camelCase JSON (část 2)
- BS5 nemá `.jumbotron`, `.panel`, glyphicons, `.pull-*`, `.text-right`
- Flatpickr má vlastní CSS — self-hostni `flatpickr.min.css` + `l10n/cs.js`
- BS5 JS je vanilla — modaly/collapse přes `data-bs-*` nebo `new bootstrap.Collapse(el)`

---

## 8. Kickoff prompt pro novou session

> Pracuji na projektu Hydra2 (migrace na .NET 8), větev `net8-migration`. Fáze 0–10 hotové,
> teď dělám **Fázi 11 — Bootstrap 5 + drop jQuery**. Přečti si
> `docs/migration/phase-11-brief.md` (kompletní zadání), `docs/migration/roadmap.md`
> (sekce Fáze 11) a `docs/migration/phase-9-typescript.md` (jak funguje frontend build).
> Pak postupuj page-by-page dle briefu, buildni a otestuj (`dotnet build/test Hydra2.net8.sln`,
> `cd frontend && npm run build`), a commituj v češtině stejným stylem jako předchozí fáze.
> Pozor na camelCase JSON serializaci (část 2 briefu) a na commit přebuildeného `graf.min.js`.
