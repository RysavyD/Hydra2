# Fáze 11 — Bootstrap 5 + drop jQuery

> Datum: 2026-08-04
> Větev: `net8-migration`
> Commit: `c8689b3`
> Build: ✅ 0 warning, 0 error · Tests: ✅ **82 / 82 passed**

---

## Cíl

Modernizovat frontend stack: odstranit jQuery dependency (−87 KB), přejít na Bootstrap 5
s nativním dark mode a vanilla-JS API. Přínos: menší bundle, žádné jQuery security
vulnerabilities, moderní developer experience.

---

## Co bylo uděláno

### A) Bootstrap 3.4.1 → Bootstrap 5.3.3

- `wwwroot/lib/bootstrap/css/bootstrap.min.css` → BS5 (227 KB, self-hosted)
- `wwwroot/lib/bootstrap/js/bootstrap.bundle.min.js` (79 KB, incl. Popper; nový soubor)
- BS3-specific třídy přejmenovány/odstraněny:
  - `.navbar-inverse` → `.navbar-dark.bg-dark`
  - `.navbar-toggle` → `.navbar-toggler`; `data-toggle` → `data-bs-toggle`; `data-target` → `data-bs-target`
  - `.icon-bar` × 3 → `<span class="navbar-toggler-icon">`
  - `.navbar-header` obal odstraněn
  - `.col-md-3.col-sm-6` zachovány (fungují v BS5 stejně)
  - `.btn-default` → `.btn-secondary` (Adm/Index.cshtml)
  - `style="margin-top:2em"` → utility třída `.mt-4`
  - `style="padding-top:10px"` → `.pt-2`
  - `class="form-control"` na `select` → `class="form-select"` (BS5 rozlišuje)
- Navbar: BS3 `<div class="navbar">` → BS5 `<nav class="navbar navbar-expand-md">`

### B) Glyphicons → Bootstrap Icons 1.11.3

- `wwwroot/lib/bootstrap-icons/` (self-hosted CSS + woff2 font, 84 KB CSS)
- `<i class="glyphicon glyphicon-calendar fa fa-calendar">` → `<i class="bi bi-calendar-event">`
- Kalendářové ikony v `Graf/Index.cshtml` přesunuty do `<button class="calendar-trigger">` (BS5 input-group)
- Inline `style="position: absolute..."` odstraněny z ikon

### C) Drop jQuery — vanilla DOM + fetch API

Soubor `frontend/src/graf.ts` kompletně přepsán (~400 řádků), žádný `$` symbol:

| jQuery | Vanilla |
|---|---|
| `$(function(){})` | `document.addEventListener("DOMContentLoaded", async () => {})` |
| `$.ajax({...})` | `await fetch(url)` + `response.json()` |
| `$("#id").val()` | `el.value` |
| `$(...).text(x)` | `el.textContent = x` |
| `$(...).attr("href", x)` | `el.href = x` |
| `$(...).on("click", fn)` | `el.addEventListener("click", fn)` |
| `$(...).hide()/.show()` | `el.classList.add/remove("d-none")` |
| `$(...).empty()` | `el.replaceChildren()` |
| `$(...).append(html)` | `el.insertAdjacentHTML()` nebo `createElement` |
| `$.each(arr, fn)` | `arr.forEach(fn)` nebo `for...of` |
| `$.isNumeric(x)` | `Number.isFinite(Number(x))` |
| `$("input:checked").each()` | `querySelectorAll(...).map(cb => cb.value).join("")` |

Camelcase JSON (z .NET 8 System.Text.Json) zůstává správně:
- `item.date`, `item.h`, `item.q`, `item.t` (lowercase — viz brief sekce 2)

### D) daterangepicker → Flatpickr 4.6.13

- `wwwroot/lib/flatpickr/flatpickr.min.js` (50 KB, self-hosted)
- `wwwroot/lib/flatpickr/flatpickr.min.css` (16 KB)
- `wwwroot/lib/flatpickr/l10n/cs.js` (český locale)
- `dateFormat: "d/m/Y"` = server formát `dd/MM/yyyy` (konzistentní s `ParseDateTime`)
- Kalendářová ikona (Bootstrap Icons) otevírá picker přes `startPicker.open()`/`stopPicker.open()`
- `moment.js` + `daterangepicker.js` odstraněny z loadování v `Graf/Index.cshtml`

### E) Bootbox odstraněn

- `wwwroot/js/Hydra2.js`: `ShowWaitDialog`/`HideWaitDialog` (bootbox) odstraněny
  (funkce byly prakticky nepoužívané od Fáze 6 — graf má vlastní `chart-skeleton` overlay)
- `bootbox.min.js` odstraněn z WebOptimizer bundle

### F) BS5 native dark mode

- `theme-init.js`: `data-theme` → `data-bs-theme`; přidána detekce OS preference:
  ```js
  effective = window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  document.documentElement.setAttribute("data-bs-theme", effective);
  ```
- `Hydra2.js`: toggle `applyMode()` nastavuje `data-bs-theme` na `<html>`; "auto" řeší OS preference
- `site.css`: vlastní CSS proměnné `--bg`, `--text` atd. odstraněny → nahrazeny BS5 tokeny
  `var(--bs-body-bg)`, `var(--bs-border-color)`, `var(--bs-tertiary-bg)` atd.
- `[data-theme="dark"]` → `[data-bs-theme]` (Bootstrap 5 to řeší automaticky)
- `@media (prefers-color-scheme: dark)` blok z CSS odstraněn (zpracováváno v JS)

### G) WebOptimizer bundle cleanup

```diff
  pipeline.AddCssBundle("/css/site.bundle.css",
      "/lib/bootstrap/css/bootstrap.min.css",
+     "/lib/bootstrap-icons/bootstrap-icons.min.css",   // nové
      "/css/site.css");

  pipeline.AddJavaScriptBundle("/js/site.bundle.js",
-     "/lib/jquery/jquery.min.js",                      // -87 KB
-     "/lib/bootbox/bootbox.min.js",                    // -14 KB
-     "/lib/bootstrap/js/bootstrap.min.js",             // -30 KB (BS3)
+     "/lib/bootstrap/js/bootstrap.bundle.min.js",      // +79 KB (BS5 incl. Popper)
      "/js/Hydra2.js");
```

**Výsledné velikosti bundlů:**

| Bundle | Před | Po |
|---|---|---|
| `site.bundle.js` | ~144 KB (jquery 87 + bootbox 14 + bs3 30 + hydra2) | ~81 KB (bootstrap.bundle 79 + hydra2 2) |
| `site.bundle.css` | ~260 KB (bs3 200 + site) | ~310 KB (bs5 227 + bootstrap-icons 84 = netted through minifier) |
| `graf.min.js` | 6.5 KB | 6.3 KB |

JS bundle: **-63 KB** (−44 %). První render na mobilu rychlejší.

### H) Testy aktualizovány

- `WebOptimizerBundleTests.Js_bundle_contains_bootstrap5_and_hydra2_but_not_jquery`:
  nové assertions pro BS5 (Popper), hydra2-theme; `jQuery.fn.jquery` nesmí být přítomno
- `PwaAndThemeTests.Theme_init_script_is_served`: `data-theme` → `data-bs-theme`
- `PwaAndThemeTests.Css_bundle_contains_dark_mode_tokens`: `--bg` → `--bs-body-bg`; `data-bs-theme`
- `SecurityHeadersTests.Static_files_under_lib_have_immutable_cache`: jquery → bootstrap.bundle

---

## Verifikace

### Automatická
```
dotnet build Hydra2.net8.sln → 0 warning, 0 error
dotnet test Hydra2.net8.sln  → 82/82 passed
npm run typecheck            → čistý (žádné jQuery typy)
npm run build                → graf.min.js 6.3 KB
```

### Manuální (doporučeno na staging)
- [ ] Home, About, Contact — hero sekce vypadá správně (light + dark mode)
- [ ] Navbar — hamburger menu funguje na mobilu (BS5 collapse)
- [ ] Graf — Flatpickr datepicker otevírá Czech locale, formát DD/MM/YYYY
- [ ] Graf — Načíst data: 3 série (Hladina/Průtok/Teplota), zoom/pan
- [ ] Graf — tabulka pod grafem zobrazuje datumy (camelCase JSON: `item.date`)
- [ ] Dark mode toggle: auto → světlé → tmavé → auto, perzistuje po reload
- [ ] Adm/Index, Adm/SpotOverView, Adm/HandUpdate, Adm/GetStationOverView
- [ ] `grep -ri "jquery" src/Hydra2.Web` — jen historické soubory (daterangepicker.js), žádný load

---

## Chytáky a rozhodnutí

- **Bootstrap 5 má `jQueryInterface` v bundle.js** — backward compatibility API (není jQuery library).
  Test proto hledá `"jQuery.fn.jquery"` (unique jQuery self-identification), ne pouhé `"jQuery"`.
- **Flatpickr + server ParseDateTime** — format `d/m/Y` = `dd/MM/yyyy`, konzistentní.
- **`d-none` pro show/hide** místo `style.display` — cleaner, funguje s BS5 `.d-none` utility.
- **`jumbotron`** nahrazen `bg-body-secondary p-5 rounded` — automaticky reaguje na `data-bs-theme`.
- **Bootstrap Icons font path** — CSS z jsDelivr používá relativní `fonts/` cestu, sedí s adresářem.
- **Dark mode "auto"** řešen v JS (theme-init.js + Hydra2.js matchMedia), ne v CSS — umožňuje
  BS5 nativní dark mode komponentám korektně reagovat.

---

## TL;DR

✅ Bootstrap 3.4.1 → Bootstrap 5.3.3 (navbar, utilities, dark mode)
✅ Bootstrap Icons nahrazuje Glyphicons
✅ jQuery zcela odstraněno — vanilla DOM + fetch API
✅ daterangepicker → Flatpickr (vanilla, Czech locale, správný formát)
✅ bootbox odstraněn
✅ data-bs-theme pro dark mode (BS5 native)
✅ JS bundle −63 KB (−44 %)
✅ npm typecheck čistý, 82/82 testů zelených
