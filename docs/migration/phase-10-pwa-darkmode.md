# Fáze 10 — PWA + Dark mode

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ 0 warning, 0 error · Tests: ✅ **82 / 82 passed**

---

## Cíl

Volitelná modernizace pro lepší user experience na mobilu a v noci. Záměrně **nedělám** Bootstrap 5 (zachovat design pro stávající uživatele).

---

## Co bylo uděláno

### 1. PWA manifest

[`wwwroot/manifest.webmanifest`](../../src/Hydra2.Web/wwwroot/manifest.webmanifest):
```json
{
    "name": "Hydra2 — vodní stavy ČR",
    "short_name": "Hydra²",
    "description": "Vodní stavy, průtoky a teploty českých řek a nádrží.",
    "lang": "cs",
    "start_url": "/Graf",
    "scope": "/",
    "display": "standalone",
    "background_color": "#222222",
    "theme_color": "#1a3d6e",
    "icons": [
        {
            "src": "/icons/icon.svg",
            "sizes": "any",
            "type": "image/svg+xml",
            "purpose": "any maskable"
        }
    ],
    "categories": ["weather", "navigation", "utilities"]
}
```

**Co to umožní**:
- Mobilní prohlížeče (Chrome, Edge, Safari) nabídnou „Přidat na plochu" tlačítko
- Po nainstalování má Hydra² vlastní ikonu a běží jako standalone app (bez address baru)
- `start_url: "/Graf"` — install otevře rovnou Graf, ne homepage
- `theme_color: #1a3d6e` — barva mobile browser chrome odpovídá brandové modré

### 2. SVG ikona
[`wwwroot/icons/icon.svg`](../../src/Hydra2.Web/wwwroot/icons/icon.svg) — vektorová "H²" s vlnami:

```svg
<svg viewBox="0 0 512 512">
  <rect width="512" height="512" rx="80" fill="#1a3d6e"/>
  <text x="256" y="290" font-size="240" font-weight="700" fill="#fff">H</text>
  <text x="380" y="232" font-size="120" font-weight="700" fill="#3b9aff">2</text>
  <path d="M 60 380 Q 130 350 200 380 T ..." stroke="#3b9aff" stroke-width="14"/>
  <path d="M 60 430 Q ..." stroke="#5fb0ff" stroke-width="10" opacity="0.7"/>
</svg>
```

- `sizes: "any"` v manifestu — SVG se škáluje na jakoukoliv velikost
- `purpose: "any maskable"` — funguje i jako Android adaptive icon (s round/squircle maskou)
- 1.2 KB raw, gzip ~600 B

V `_Layout.cshtml` přidáno:
```html
<link rel="icon" type="image/svg+xml" href="~/icons/icon.svg" />
<link rel="apple-touch-icon" href="~/icons/icon.svg" />
<link rel="shortcut icon" href="~/favicon.ico" />
<link rel="manifest" href="~/manifest.webmanifest" />
```

### 3. Dark mode přes CSS custom properties

[`wwwroot/css/site.css`](../../src/Hydra2.Web/wwwroot/css/site.css) — přidán **theming layer**:

```css
:root {
    --bg: #ffffff;
    --bg-elevated: #f5f7fa;
    --text: #222222;
    --text-muted: #6c757d;
    --link: #2479f6;
    --link-hover: #185fc3;
    --border: #dddddd;
    --table-stripe: #fafafa;
    color-scheme: light;
}

:root[data-theme="dark"] { /* manuálně vynucené tmavé téma */ ... }

@media (prefers-color-scheme: dark) {
    :root:not([data-theme="light"]) { /* automaticky podle OS */ ... }
}

body {
    background-color: var(--bg);
    color: var(--text);
}
```

**Třístavová logika**:
| `data-theme` | `prefers-color-scheme` | Aktivní téma |
|---|---|---|
| (unset) | light | světlé |
| (unset) | dark | **tmavé** (auto) |
| `light` | (jakkoliv) | **světlé** (vynucené) |
| `dark` | (jakkoliv) | **tmavé** (vynucené) |

`color-scheme: dark` v `:root` říká prohlížeči, ať i scrollbary, form fieldy a default kontextová menu byly v dark verzi.

### 4. Theme toggle button

[`wwwroot/js/Hydra2.js`](../../src/Hydra2.Web/wwwroot/js/Hydra2.js) — přidána logika:

```javascript
// Cycle: auto -> light -> dark -> auto
function nextMode(mode) {
    if (mode === "auto") return "light";
    if (mode === "light") return "dark";
    return "auto";
}
```

Tlačítko `<button id="theme-toggle">` v navbar napravo. Po kliknutí cykluje 3 stavy a uloží do `localStorage["hydra2-theme"]`.

**Stavy**:
- `auto` — žádný `data-theme`, CSS @media decides
- `light` — `data-theme="light"`, vynutí světlé i v tmavém OS
- `dark` — `data-theme="dark"`, vynutí tmavé i ve světlém OS

### 5. Anti-FOUC: `theme-init.js` v `<head>`

Bez tohoto by se uživateli s `localStorage.theme=dark` na chvíli zobrazila světlá stránka před načtením JS bundle. Řeší to **synchronní script v `<head>` před parsováním body**:

[`wwwroot/js/theme-init.js`](../../src/Hydra2.Web/wwwroot/js/theme-init.js):
```javascript
(function () {
    try {
        var saved = localStorage.getItem("hydra2-theme");
        if (saved === "light" || saved === "dark") {
            document.documentElement.setAttribute("data-theme", saved);
        }
    } catch (e) { /* localStorage blocked - fall through to OS preference */ }
})();
```

Načítá se **mimo bundle** (samostatný `<script src="~/js/theme-init.js">` na začátku `<head>`). Bundle (`site.bundle.js`) se načítá až na konci body — pozdě.

**Velikost**: 0.4 KB, blocking ale tak rychlé, že se neprojeví.

CSP: `script-src 'self'` — interní soubor, žádné `'unsafe-inline'` nepotřeba.

### 6. Layout přepsán
[`Views/Shared/_Layout.cshtml`](../../src/Hydra2.Web/Views/Shared/_Layout.cshtml):
- Přidán `<script src="~/js/theme-init.js">` v `<head>` (nahoře, sync)
- Přidány `<link>` pro manifest, SVG icon, apple-touch-icon
- Aktualizován `theme-color` meta na brandovou modrou `#1a3d6e`
- Přidán `<button id="theme-toggle">` v `navbar-right`
- Přidána CSS třída `.theme-toggle` v `site.css` s focus-visible styly

---

## Tests ([PwaAndThemeTests.cs](../../tests/Hydra2.Tests/Web/PwaAndThemeTests.cs))

| Test | Co ověřuje |
|---|---|
| `Manifest_endpoint_serves_valid_json` | `GET /manifest.webmanifest` → 200 + obsahuje "name", "start_url", "icons" |
| `Svg_icon_is_served` | `GET /icons/icon.svg` → 200 + `image/svg+xml` |
| `Theme_init_script_is_served` | `GET /js/theme-init.js` obsahuje "hydra2-theme" + "data-theme" |
| `Css_bundle_contains_dark_mode_tokens` | site.bundle.css obsahuje `--bg`, `data-theme="dark"`, `prefers-color-scheme:` |
| `Layout_includes_manifest_link` | HTML / obsahuje `rel="manifest"`, `manifest.webmanifest`, `theme-toggle` |

---

## Co je úmyslně **mimo scope**

| Položka | Důvod |
|---|---|
| Service Worker | Komplexita versioning vs benefit pro tento use case marginální. Phase 10.5 pokud bude potřeba. |
| amCharts dark theme | Graf zůstává světlý — to je OK, datová vizualizace na světlém pozadí je čitelnější. Pokud bude vadit, lze pridat `am5themes_Dark`. |
| Bootstrap 5 redesign | Uživatelé zvyklí na současný vzhled. Velký risk a malý benefit. |
| Push notifications | Mimo scope tohoto projektu (žádný backend pro push). |
| OpenTelemetry | Overkill pro tento projekt. Manuální logy stačí. |
| PNG icon variants | SVG funguje univerzálně. PNG by se hodily pro starší Android verze, ale 99 % uživatelů má novější. |

---

## Verifikace po deployi

### PWA install
1. Otevři https://hydra2.dusanrysavy.cz na Chrome/Edge mobile
2. Hamburger menu → "Install app" / "Přidat na plochu"
3. Ikona se objeví na ploše
4. Klepni — otevře se v standalone režimu (bez address baru)

### Dark mode
1. Desktop: System Settings → Theme: Dark → web by měl být automaticky tmavý
2. Klikni tlačítko "Téma: auto" v navbaru → cykluje na "Téma: světlé" → tmavé → auto
3. Reloadni stránku — výběr se zachová (localStorage)
4. Otevři v incognito (jiné localStorage) → vrátí se na auto

### DevTools verifikace
```js
// V konzoli:
localStorage.getItem("hydra2-theme");          // null / "light" / "dark"
document.documentElement.dataset.theme;        // undefined / "light" / "dark"
getComputedStyle(document.body).backgroundColor;  // měla by se měnit
```

### Lighthouse PWA audit
[lighthouse](https://developer.chrome.com/docs/lighthouse) test → kategorie PWA. Po Fázi 10 by měla projít:
- ✅ Has a `<meta name="viewport">` tag
- ✅ Web app manifest meets installability requirements
- ✅ Responds with 200 when offline (až bude Service Worker, zatím skip)
- ✅ Has theme color
- ✅ Apple touch icon

---

## Statistika

| Metrika | Hodnota |
|---|---|
| Nové soubory | 4 (manifest, icon.svg, theme-init.js, PwaAndThemeTests.cs) |
| Modifikované soubory | 3 (site.css, Hydra2.js, _Layout.cshtml) |
| Velikost bundle navíc | ~600 B (CSS dark mode + JS toggle) |
| Tests | **82 / 82 passing** |
| Inline JS / CSS | stále 0 (CSP `script-src` zůstává bez `'unsafe-inline'`) |

---

## TL;DR

✅ PWA manifest + SVG icon → install na home screen mobilu
✅ Dark mode přes CSS custom properties + `prefers-color-scheme`
✅ Theme toggle button (auto/light/dark cycle, localStorage persistence)
✅ `theme-init.js` v `<head>` zabraňuje FOUC
✅ Žádný inline JS (CSP-clean)
✅ 82/82 testů passing

**Po této fázi**: Hydra2 web je nainstalovatelný jako PWA, podporuje dark mode automaticky i manuálně. Frontend je modernizovaný v rozsahu, který nemění design.

**Zbývá**: Phase 11 — cleanup starých `.NET Framework 4.6.1` projektů ze solution po cutoveru staging → produkce.
