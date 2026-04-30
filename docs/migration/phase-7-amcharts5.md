# Fáze 7 — amCharts 4 → amCharts 5

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ 0 warning, 0 error · Tests: ✅ **72 / 72 passed**

---

## Cíl

Opustit **EOL knihovnu amCharts 4** (už nedostává security ani bug fixy) a přejít na podporovanou amCharts 5. Vybráno před alternativami (ECharts / Chart.js) z důvodu:
- nejmenší skok v UX a vzhledu
- nejnižší riziko regrese
- amCharts 5 vyšší výkon na velkých datasetech (užitečné pro `MaxJsonLength = int.MaxValue` data)
- API se hlásí jako stabilní pro dlouhodobou podporu

---

## Co bylo uděláno

### 1. CDN scripty
[`Graf/Index.cshtml`](../../src/Hydra2.Web/Views/Graf/Index.cshtml):

| Před (v4, `www.amcharts.com/lib/4/`) | Po (v5, `cdn.amcharts.com/lib/5/`) |
|---|---|
| `core.js` | `index.js` |
| `charts.js` | `xy.js` |
| `themes/animated.js` | `themes/Animated.js` (PascalCase!) |
| `lang/cs_CZ.js` | `locales/cs_CZ.js` |

### 2. Přepsaný chart kód
amCharts 5 má **velmi jiné API** než v4. Klíčové body přepisu:

```javascript
// ----- v4 -----
am4core.useTheme(am4themes_animated);
chart = am4core.create("chartdiv", am4charts.XYChart);
chart.data = data;
var dateAxis = chart.xAxes.push(new am4charts.DateAxis());
chart.legend = new am4charts.Legend();
chart.cursor = new am4charts.XYCursor();

// ----- v5 -----
root = am5.Root.new("chartdiv");
root.setThemes([am5themes_Animated.new(root)]);
chart = root.container.children.push(am5xy.XYChart.new(root, {...}));
xAxis = chart.xAxes.push(am5xy.DateAxis.new(root, {...}));
chart.set("cursor", am5xy.XYCursor.new(root, {...}));
var legend = chart.children.push(am5.Legend.new(root, {...}));
legend.data.setAll(chart.series.values);
```

Hlavní změny:
- **Explicit `Root`** — každý chart má vlastní `am5.Root`, který musí být `dispose()`-nutý před rebuildem (jinak memory leak při opakovaném načítání dat)
- **Fluent factory** — `Object.new(root, config)` místo `new Object()` + property setters
- **Data setting** — `series.data.setAll([...])` místo `chart.data = [...]`
- **Setters/getters** — `chart.set("cursor", ...)` / `chart.get("cursor")` místo property assignment
- **Date format** — `valueXField` očekává `number` (epoch ms), ne `Date` objekt → `new Date(item.Date).getTime()`

### 3. Vlastnosti zachovány a přidány

| Funkce | v4 | v5 |
|---|---|---|
| 3 série (h, Q, t) s vlastními Y axes | ✅ | ✅ |
| Smoothed lines (`tensionX = 0.8`) | ✅ | ✅ (přes `SmoothedXLineSeries` + `tension`) |
| Dual Y axes (left + right) | ✅ | ✅ (přes `opposite: true`) |
| Cursor s tooltipy | ✅ | ✅ |
| Legend pod grafem | ✅ | ✅ |
| Animovaný theme | ✅ | ✅ |
| Czech locale | ✅ | ✅ (přes `am5locales_cs_CZ`) |
| **Wheel zoom (X-axis)** | ❌ | **✅ nová** |
| **Pan (drag X-axis)** | ❌ | **✅ nová** |
| **Pinch-zoom na mobilu** | ❌ | **✅ nová** |
| **Cursor zoom (drag-select)** | ❌ | **✅ nová** přes `cursor.behavior: "zoomX"` |
| Lifecycle disposal | implicitní | explicitní `disposeChart()` před rebuildem |

### 4. Disposal pattern

```javascript
function disposeChart() {
    if (root) { root.dispose(); root = null; chart = null; xAxis = null; }
}

function CreateGraph(data) {
    disposeChart();   // ← ČHM pro opakované volání bez memory leaku
    root = am5.Root.new("chartdiv");
    ...
}
```

V v4 byl chart silně managovaný; volat `am4core.create` opakovaně (např. když uživatel klikne "Načíst data" znovu) v praxi fungovalo, ale v5 vyžaduje explicitní `dispose()` aby se uvolnily SVG nodes a event listenery.

### 5. CSP zúženo
[`SecurityHeadersMiddleware.cs`](../../src/Hydra2.Web/Middleware/SecurityHeadersMiddleware.cs):

```diff
- script-src 'self' 'unsafe-inline' https://www.amcharts.com https://cdn.amcharts.com;
+ script-src 'self' 'unsafe-inline' https://cdn.amcharts.com;
```

`www.amcharts.com` (v4) už není v allowlistu — pokud by se tam někdo pokusil mířit, browser to zablokuje. Bezpečnostní přínos.

### 6. About.cshtml
- "AmCharts" → "amCharts 5" (correct casing + version)
- Ručně vytvořený link na CSS sekci `http://...` → `https://...` + `target="_blank"` + `rel="noopener"`

---

## Co je úmyslně **mimo scope**

| Položka | Důvod |
|---|---|
| Self-hosting amCharts 5 | CDN je rychlejší (edge caching), CSP allowlist stačí. Self-host přijde s Phase 9 (build pipeline). |
| Server-side rendered chart fallback | Není potřeba pro tento use case. |
| Migrace na ECharts (Variant B) | amCharts 5 zvládá současné objemy + lepší UX. Pokud by se ukázal nedostatečný, Phase 10 (volitelně). |
| Plná dark theme | Phase 10 (Dark mode + Bootstrap 5). |
| TypeScript chart kód | Phase 9. |

---

## Verifikace po deployi

### 1. Síťová kontrola
DevTools → Network → ověř, že `cdn.amcharts.com/lib/5/...` se loaduje a žádný 404. Nebudou tam 4× `www.amcharts.com/lib/4/...` requesty.

### 2. Funkční test
- Načti graf pro libovolnou stanici
- 3 čáry (Hladina, Průtok, Teplota) s vlastními Y osami se vykreslí
- Tooltip při hoveru funguje
- Legend pod grafem
- **Zkus wheel scroll na grafu** — měl by zoomovat X-osu (NOVĚ)
- **Zkus drag horizontálně** — měl by panovat (NOVĚ)
- **Na mobilu pinch zoom** — měl by zoomovat (NOVĚ)
- Změň výběr (jiná stanice / datum) → graf se rebuilduje bez memory leak

### 3. Console kontrola
DevTools → Console — neměly by být `am4core` errory (kdyby zůstal někde nepřepsaný kód).

### 4. CSP kontrola
Network → response headers → CSP musí obsahovat `https://cdn.amcharts.com`.

---

## Riziko a rollback

**Riziko: medium**. amCharts 5 API je značně jiné. I když přepis prošel statickou kontrolou (build + testy), vizuální regrese se ukáže až v prohlížeči se skutečnými daty. Doporučený workflow:

1. **Deploy na staging** (https://hydra2.dusanrysavy.cz)
2. Manuální verifikace na desktop + mobil
3. Pokud vše OK → produkce
4. Pokud problém → rollback (předchozí commit `af574c3` = Fáze 6)

```bash
# Rollback (pokud to budeš někdy potřebovat):
git revert HEAD
git push
# nebo
git reset --hard af574c3
git push --force-with-lease
```

---

## TL;DR

✅ amCharts 4 (EOL) → amCharts 5 (LTS) plně přepsáno
✅ CDN URL z `www.amcharts.com/lib/4` → `cdn.amcharts.com/lib/5`
✅ `am5.Root` + explicitní `dispose()` lifecycle
✅ `am5xy.SmoothedXLineSeries` zachovává smoothing (tension 0.8)
✅ Czech locale, Animated theme, Legend, Cursor, Tooltip — all preserved
✅ **Bonus**: wheel zoom, pan, pinch-zoom na mobilu, drag-select cursor zoom
✅ CSP zúženo (jen `cdn.amcharts.com`)
✅ About.cshtml aktualizováno
✅ 72/72 testů passing (CSP test aktualizovaný)

**Po této fázi**: hlavní feature aplikace je na podporované knihovně. EOL riziko vyřešeno.
