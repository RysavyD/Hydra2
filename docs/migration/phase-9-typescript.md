# Fáze 9 — TypeScript pro Graf + DevOps polish

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ 0 warning, 0 error · Tests: ✅ **77 / 77 passed**

---

## Cíl

Přesunout klientský kód z inline `<script>` bloků v Razor views do TypeScript modulu, vytvořit build pipeline (esbuild), zpřísnit CSP odebráním `'unsafe-inline'` z `script-src`. Závěrečný úklid dev-experience prostřednictvím `.editorconfig`.

---

## Co bylo uděláno

### 1. Frontend stack ([`frontend/`](../../frontend/))

```
frontend/
├── package.json        npm scripts + devDependencies
├── tsconfig.json       TS config (strict, ES2020)
├── build.mjs           esbuild script (jeden bundle, sourcemap)
├── README.md           jak builduje se / watch
├── .gitignore          node_modules, *.tsbuildinfo
└── src/
    └── graf.ts         extrakce z Graf/Index.cshtml (~330 řádek)
```

**Volba `esbuild`** (nikoliv Vite/webpack):
- Single-purpose: jeden modul → jeden bundle
- 12 ms build, žádný HMR overhead
- ESM moduly v Node 24+, žádné CJS šaškování

```json
"devDependencies": {
    "@types/jquery": "^3.5.30",
    "esbuild": "^0.24.0",
    "typescript": "^5.6.3"
}
```

### 2. TypeScript extrakce — `frontend/src/graf.ts`

Veškerá inline JS logika z `Graf/Index.cshtml` (CreateGraph, generateChartData, createAxisAndSeries, daterangepicker wiring, URL state handling, ShowSpotInformation, GenerateDataTable, …) přepsána do modulárního TS souboru.

**Typovací strategie**:
- jQuery má kompletní `@types/jquery` typy
- amCharts 5 globals deklarovány jako `declare const am5: any` (loose) — knihovna je velká a nepoužíváme detail typování
- Vlastní data structures (`Sample`, `SpotInfo`, `GetDataResponse`, `UrlState`) mají interface

```typescript
declare const am5: any;
declare const am5xy: any;
declare const am5themes_Animated: any;
declare const am5locales_cs_CZ: any;

interface Sample { Date: string; h: number | null; Q: number | null; t: number | null; }

interface SpotInfo { type: number; spa0: number | null; ...; raftLink: string | null; }
```

**Bonus změny** vůči původnímu inline JS:
- `var` → `const`/`let` (ES6+)
- `for (var i = 0; i < arr.length; i++)` → `for...of`
- Globální proměnné → modulární scoped `state` objekt
- jQuery `.click(fn)` → `.on("click", fn)` (preferovaný v jQuery 3+)
- Razor `@Url.Content("~/Graf/GetSpots/")` interpolace → konstanty `SPOTS_URL = "/Graf/GetSpots/"` (path-based, app není pod sub-pathem)

### 3. Build pipeline

```bash
cd frontend
npm install            # one-time
npm run build          # produces ../src/Hydra2.Web/wwwroot/js/graf.min.js (~6.5 KB) + .map
npm run watch          # rebuild on save (dev)
npm run typecheck      # tsc --noEmit (CI-friendly TS validation)
```

**Output velikost**:
| Soubor | Velikost | Komprese |
|---|---|---|
| `graf.min.js` | 6.5 KB | minified |
| `graf.min.js.map` | 22 KB | sourcemap pro debugging |

esbuild target `es2018` pro kompatibilitu s drtivou většinou prohlížečů. `legalComments: "none"` — žádné copyright notice v bundle output.

### 4. Razor views vyčištěny od inline JS

#### `Views/Graf/Index.cshtml`
**Před**: ~360 řádků inline JS + 1 inline `<style>` blok
**Po**: 0 inline JS + 0 inline style. Section `scripts` má jen externí reference:

```html
@section scripts {
    <script src="https://cdn.amcharts.com/lib/5/index.js" crossorigin="anonymous"></script>
    <script src="https://cdn.amcharts.com/lib/5/xy.js" crossorigin="anonymous"></script>
    <script src="https://cdn.amcharts.com/lib/5/themes/Animated.js" crossorigin="anonymous"></script>
    <script src="https://cdn.amcharts.com/lib/5/locales/cs_CZ.js" crossorigin="anonymous"></script>
    <script src="~/lib/daterangepicker/moment.min.js" asp-append-version="true"></script>
    <script src="~/lib/daterangepicker/daterangepicker.js" asp-append-version="true"></script>
    <script src="~/js/graf.min.js" asp-append-version="true"></script>
}
```

Inline `#chartdiv { width: 100%; height: 100%; }` přesunut do [`site.css`](../../src/Hydra2.Web/wwwroot/css/site.css).

#### `Views/Adm/Index.cshtml`
**Před**: malý inline script s `addEventListener('submit', ...)` který přesměrovával na `/Adm/GetStationOverView/{id}`.
**Po**: form `method="get"` s `asp-action="GetStationOverView"`. Browser submit přidá `?id=N`. ASP.NET Core MVC sváže `int id` parametr z query stringu stejně jako z route.

```html
<form class="form-inline" method="get"
      asp-controller="Adm" asp-action="GetStationOverView">
    <input type="number" name="id" required ... />
    <button type="submit">Zobrazit scrap</button>
</form>
```

Žádný JS potřebný.

### 5. CSP zpřísněno — drop `'unsafe-inline'` ve `script-src`

```diff
- script-src 'self' 'unsafe-inline' https://cdn.amcharts.com;
+ script-src 'self' https://cdn.amcharts.com;
```

Po extrakci je `script-src 'self'` dostatečný:
- `~/js/site.bundle.js` — z bundleru (Phase 8)
- `~/js/graf.min.js` — z TS bundleru (Phase 9)
- `~/lib/daterangepicker/*.js` — staticky servované
- `https://cdn.amcharts.com/lib/5/...` — explicitně povoleno

`style-src 'unsafe-inline'` zatím **zachováno** — Bootstrap 3 + Razor views mají `style="..."` atributy v 12+ místech. Migrace na external CSS by byla samostatná drobná fáze.

### 6. Test guard proti regresi

Nový test [`SecurityHeadersTests.Csp_script_src_does_not_allow_unsafe_inline`](../../tests/Hydra2.Tests/Web/SecurityHeadersTests.cs):

```csharp
var csp = response.Headers.GetValues("Content-Security-Policy").Single();
var scriptSrc = csp.Split(';').Single(d => d.TrimStart().StartsWith("script-src"));
scriptSrc.Should().NotContain("'unsafe-inline'");
```

Pokud někdo v budoucnu přidá inline JS a "rychle" otevře CSP zpět na `'unsafe-inline'`, test selže.

### 7. `.editorconfig` v repo rootu

[`.editorconfig`](../../.editorconfig) — sjednocuje formátování napříč editory (Visual Studio, VS Code, Rider, vim s pluginem):

| File pattern | indent |
|---|---|
| C# (`.cs`, `.csproj`, `.sln`) | 4 spaces |
| Web (`.html`, `.cshtml`, `.css`, `.js`, `.ts`) | 2 spaces |
| JSON / YAML | 2 spaces |
| Bash (`.sh`) | LF (override CRLF default) |

Plus C# konvence: `csharp_new_line_before_open_brace = all`, `dotnet_sort_system_directives_first = true`.

### 8. `.gitignore` doplněno

```
# Frontend build artifacts (Phase 9 — TypeScript)
frontend/node_modules/
frontend/*.tsbuildinfo
```

`graf.min.js` v `wwwroot/js/` **JE commitnuto** — Forpsi shared hosting nemá Node, deploy je `dotnet publish` + FTP. Když se mění TS source, dev musí spustit `npm run build` a commit i nový bundle.

---

## Workflow pro vývoj

### Změna server-side kódu (C#)
Standardní `dotnet build` + `dotnet test`. Žádný npm krok potřeba.

### Změna client-side kódu (graf, jQuery wiring)
1. Edit `frontend/src/graf.ts`
2. `cd frontend && npm run watch` (auto-rebuild) **nebo** `npm run build` jednorázově
3. `dotnet run --project src/Hydra2.Web` — F5 v prohlížeči
4. Po dokončení: commit i `frontend/src/graf.ts`, **i** `wwwroot/js/graf.min.js` + `.map`

### CI workflow (`.github/workflows/ci.yml`)
Stávající CI workflow běží `dotnet test`, který používá **commitnutou verzi** `graf.min.js`. Není potřeba Node v CI prostředí. Pokud se v budoucnu chce, lze přidat `npm install && npm run build` step do CI před `dotnet test` — pak by `graf.min.js` mohl být v `.gitignore` taky.

---

## Vedlejší benefity

1. **Type safety** — TS chytí překlepy v jQuery selektorech (`$("#riverID")` vs `$("#riverId")`) na úrovni typu
2. **Modular state** — všechny proměnné (`riverId`, `spotId`, ...) v `state` objektu místo globálních `var`
3. **Sourcemaps** — DevTools ukáže původní `.ts` při debugu, ne minified bundle
4. **CSP A+ ready** — bez `unsafe-inline` v script-src je [securityheaders.com](https://securityheaders.com) skóre vyšší
5. **Editor podpora** — `.editorconfig` zajišťuje konzistentní formátování i bez VS settings

---

## Co je úmyslně **mimo scope**

| Položka | Důvod |
|---|---|
| `style-src` drop `'unsafe-inline'` | Bootstrap 3 + 12 inline style="" v Razor → samostatná velká fáze (Phase 10 BS5 nebo cherry-picked refactor) |
| Nonce-based CSP | Vyžaduje server-side generování per-request nonce; pro static-only inline JS není potřeba |
| Bundle TS spolu s Hydra2.js | Hydra2.js (3 funkce kolem bootbox) je triviální; mít ho separátně je clean |
| Path-aware URL helpers (gen z Razor) | App běží na `/`, hardcoded paths jsou OK. Pokud někdy pod sub-path → upgrade na `<meta name="basepath">` reading. |
| TypeScript pro `Hydra2.js` (bootbox helpers) | 3 řádky JS, není value v migraci |
| MSBuild Target pro auto npm build | Vyžadovalo by Node v každém build prostředí; commit-built je pragmatičtější |

---

## Verifikace po deployi

### Browser DevTools → Network
```
GET /js/graf.min.js?v=...      200  6.5 KB
```

### Browser DevTools → Console
```
> jQuery.fn.jquery
"3.7.1"
> typeof am5
"object"
```
Žádné `Refused to execute inline script because it violates ...` chyby — protože žádný inline script už neexistuje.

### CSP scanner
[securityheaders.com/?q=hydra2.dusanrysavy.cz](https://securityheaders.com/?q=hydra2.dusanrysavy.cz) — očekávám zlepšení skóre na **A nebo A+** (předtím by mohlo být A− kvůli `'unsafe-inline'`).

### Sourcemaps debugging
F12 → Sources → `webpack://` nebo `original` → mělo by ukázat `graf.ts` zdrojový kód při breakpointu.

---

## Statistika

| Metrika | Hodnota |
|---|---|
| Inline `<script>` v Razor views | 0 (předtím 3 bloky, ~360 řádků) |
| Inline `<style>` v Razor views | 0 (1 přesunut do site.css) |
| TypeScript zdrojů | 1 soubor, 332 řádků |
| Generovaný JS | `graf.min.js` 6.5 KB + sourcemap 22 KB |
| Testů | **77 / 77 passing** |
| Build čas | dotnet ~6 s, esbuild 12 ms |

---

## TL;DR

✅ Inline JS z Razor views (~360 řádků) → modulární `frontend/src/graf.ts`
✅ esbuild pipeline (12 ms build) → `wwwroot/js/graf.min.js` (6.5 KB)
✅ TypeScript strict mode + jQuery typy
✅ Razor views: 0 inline `<script>`, 0 inline `<style>`
✅ CSP zpřísněn: `script-src` bez `'unsafe-inline'`
✅ Test guard proti budoucí regresi CSP
✅ `.editorconfig` pro konzistentní formátování
✅ 77/77 testů passing

**Po této fázi**: web vrstva je modernizovaná, bezpečnostní skóre vyšší, dev experience lepší. Zbývá jen cleanup starých .NET Framework projektů (Phase 11) po cutoveru staging → produkce.
