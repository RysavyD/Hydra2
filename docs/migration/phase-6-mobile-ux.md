# Fáze 6 — Mobile + UX

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ 0 warning, 0 error · Tests: ✅ **72 / 72 passed**

---

## Cíl

Drobné UX a accessibility úpravy bez velkých zásahů do designu. Cílí na konkrétní bolesti při použití na mobilu/tabletu identifikované při auditu webové verze.

UI parita: zachováno celkové layoutu, barev a struktury. Změny jsou:
- responzivní rozměry (graf, tabulky)
- větší touch targety
- semantic HTML + a11y atributy
- modernizace URL parametrů
- odstraněna kosmetická hardcoded hodnota v adminu

---

## Co bylo uděláno

### 1. Responzivní výška grafu
[`Graf/Index.cshtml`](../../src/Hydra2.Web/Views/Graf/Index.cshtml) + [`site.css`](../../src/Hydra2.Web/wwwroot/css/site.css)

```html
<!-- před -->
<div id="chartdiv" style="width:100%; height:600px;"></div>

<!-- po -->
<div class="chart-container">
    <div id="chartdiv"></div>
    <div id="chartLoading" class="chart-skeleton is-hidden">Načítání dat…</div>
</div>
```

```css
.chart-container {
    width: 100%;
    height: 60vh;
    min-height: 320px;
    max-height: 720px;
    position: relative;
}
```

- Na mobilu (např. 800px výšky) graf zabere ~480 px → uživatel vidí i ovládací prvky a tabulku
- Na 4K monitoru (2160 px výšky) graf 720 px (cap)
- Žádné fixní 600 px

### 2. Skeleton loading state
Místo blokujícího bootbox modalu se při AJAX requestu pro graf zobrazí **non-blocking skeleton** přímo v `#chartdiv` container:

```css
.chart-skeleton {
    position: absolute;
    inset: 0;
    background: repeating-linear-gradient(45deg, #f5f7fa, #eef1f5);
    transition: opacity 0.2s ease-in-out;
}
.chart-skeleton.is-hidden { opacity: 0; visibility: hidden; }
```

- `role="status"` + `aria-live="polite"` pro screen readery
- Uživatel může mezitím prohlížet ovládací prvky / scrollovat
- Žádný overlay přes celou stránku

### 3. Table-responsive wrap
Všechny `<table>` v `Graf/Index.cshtml` zabaleny do `<div class="table-responsive">` — Bootstrap 3 přidá horizontální scroll na úzkých obrazovkách místo přetečení layoutu:

```html
<div class="col-md-12 table-responsive">
    <table id="dataTable" class="dataTable">...</table>
</div>
```

Aplikováno na:
- `dataTable` (data grafu)
- `riverInformation` tabulka SPA stupňů
- `reservoirInformation` tabulka nádrží

### 4. Touch-friendly checkboxes
[`site.css`](../../src/Hydra2.Web/wwwroot/css/site.css):
```css
.checkbox-touch {
    display: inline-flex;
    align-items: center;
    padding: 10px 14px;
    margin: 4px 6px 4px 0;
    cursor: pointer;
    min-height: 44px;        /* Apple HIG / WCAG 2.5.5 minimum */
}
.checkbox-touch input[type="checkbox"] {
    width: 20px; height: 20px; margin-right: 10px;
}
```

V Razoru obal:
```html
<label class="checkbox-touch">
    <input type="checkbox" name="type" id="h" value="h" checked /> Hladina
</label>
```

→ Touch target ~44×44 px (před tím ~14×14 px).

### 5. Accessible labels (a11y)
Před: `<select id="river">` bez labelu, screen reader říkal jen "select".

Po:
```html
<label for="river"><h4>Tok</h4></label>
@Html.DropDownList("river", Model.Rivers, new { @class = "form-control", aria_label = "Tok" })

<label for="spot"><h4>Stanice</h4></label>
<select id="spot" class="form-control" aria-label="Stanice"></select>

<label for="start"><h4>Začátek</h4></label>
<input id="start" aria-label="Datum začátku" ...>
```

`<i class="glyphicon...">` ikona dostala `aria-hidden="true"` (dekorativní, screen reader ji vynechá).

### 6. Sémantická tabulka pro data grafu
Před: `<tr><td>Datum</td><td>Hladina</td>...</tr>` jako "header" — screen readery to braly jako data row.

Po: `<thead><tr><th>...</th></tr></thead><tbody><tr><td>...</td></tr></tbody>` — proper semantics.

### 7. URL state: query string + legacy hash fallback
**Před** (Fáze 1): URL hash `#riverId#spotId#start#stop#type`. Hash je client-only, není standardní query, špatně se kopíruje, prohlížeč ho neoznačuje.

**Po**: query string `?riverId=2&spotId=5&start=23/04/2026&stop=30/04/2026&type=hQ`.
- `URLSearchParams` API, `history.replaceState()` (bez page reload)
- **Legacy hash bookmarky stále fungují** — `readUrlState()` zkusí query string nejdřív, fallback na hash format
- Sdílení URL je teď čisté

### 8. `<a href="#">` placeholdery — `rel="noopener"`
```html
<!-- před -->
<a href="#" target="_blank" id="raftLink"></a>

<!-- po -->
<a href="#" target="_blank" rel="noopener" id="raftLink"></a>
```

`rel="noopener"` brání cílové stránce ovládat původní okno (security best-practice u `target="_blank"`). Plus JS teď schovává prázdný `raftLink`, pokud stanice nemá raft.cz odkaz (nevisí prázdný link).

### 9. Reload button: `<div>` → `<button>`
```html
<!-- před -->
<div id="Reload" class="btn btn-success btn-lg">Načíst data</div>

<!-- po -->
<button type="button" id="Reload" class="btn btn-success btn-lg">Načíst data</button>
```

`<div>` jako tlačítko nepodporuje keyboard navigation a screen reader ho nečte jako interaktivní. `<button>` má vše zdarma.

Plus media query pro mobil:
```css
@media (max-width: 768px) {
    #Reload { width: 100%; font-size: 1.15em; padding: 12px 16px; }
}
```

### 10. Focus-visible pro keyboard navigation
```css
:focus-visible {
    outline: 2px solid #2479f6;
    outline-offset: 2px;
}
```

Tab navigace ukáže jasný focus ring kolem aktivního elementu — dříve nebyl viditelný.

### 11. Admin: smazaný hardcoded `id=64`
[`Adm/Index.cshtml`](../../src/Hydra2.Web/Views/Adm/Index.cshtml) — místo nesmyslného `<a asp-route-id="64">` button:

```html
<form id="stationLookupForm" method="get">
    <label for="stationLookupId" class="visually-hidden">ID stanice</label>
    <input type="number" id="stationLookupId" name="id"
           placeholder="ID stanice" min="1" max="999" required />
    <button type="submit" class="btn btn-info">Zobrazit scrap</button>
</form>
```

Plus celý Adm/Index dostal čistší layout s `<dl>` pro statistiky a sekce sdružené pod `<h4>`.

### 12. Visually-hidden helper
```css
.visually-hidden {
    position: absolute !important;
    width: 1px; height: 1px;
    padding: 0; margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
}
```

Pro labely, které potřebujeme pro screen readery, ale nechceme vizuálně.

---

## Co je úmyslně **mimo scope**

| Položka | Důvod |
|---|---|
| Migrace na Bootstrap 5 | Velký redesign — uživatelé zvyklí na BS3 vzhled. Fáze 10 (volitelně). |
| Přepis daterangepicker | Funguje, jen mírně archaický. Fáze 8/9 spolu s amCharts. |
| Loading skeleton pro datatable | Tabulka se vykreslí rychle. Skeleton jen pro graf. |
| Drag-and-zoom v grafu | amCharts 4 to umí, ale konfigurace je hluboká — Fáze 8 spolu s replacement. |
| Dark mode | Fáze 10. |
| `og:image` | Vyžaduje obrázek; nemá zatím. |

---

## Verifikační checklist po deployi

- [ ] Mobil (≤ 768 px): graf zabere ~60% výšky, ovládání přístupné bez horizontálního scrollu
- [ ] Mobil: tlačítko "Načíst data" full-width
- [ ] Mobil: checkboxy mají ~44px touch target
- [ ] Tab navigace jde přes všechna ovládání s viditelným focus ringem
- [ ] Screen reader čte "Tok", "Stanice", "Datum začátku" atd. (ne jen "combo box")
- [ ] Stará URL `#2#5#01/04/2026#08/04/2026#hQ` stále funguje (graf se načte)
- [ ] Nová URL `?riverId=2&spotId=5&start=01/04/2026&stop=08/04/2026&type=hQ` funguje a se přepíše do adresního řádku po výběru
- [ ] Admin: ID stanice se zadává přes input, ne přes hardcoded link

---

## TL;DR

✅ Graf má responzivní výšku (60vh, 320–720 px clamp), žádné fixní 600 px
✅ Skeleton loading místo blokujícího bootbox modalu
✅ Table-responsive wrapper (mobile horizontal scroll)
✅ Touch-friendly checkboxy (44×44 px)
✅ Accessible labels pro all form fields, semantic `<thead>` v datatable
✅ Query string URL parametry (legacy hash funguje pro staré bookmarky)
✅ `<button>` místo `<div>` pro Reload (a11y + keyboard)
✅ Focus ring + `:focus-visible`
✅ Admin: smazaný hardcoded id=64, nahrazený input formem
✅ `rel="noopener"` na `target="_blank"` linky
✅ 72/72 testů zelených, build čistý

**Po této fázi**: použitelnost na mobilu citelně lepší. Frontend struktura připravena na další modernizace (TypeScript, amCharts → 5, jQuery → 3.7) v dalších fázích.
