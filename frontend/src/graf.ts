// Graf page — chart rendering + UI wiring for Hydra2 Graf/Index.cshtml.
// Bundled via esbuild to wwwroot/js/graf.min.js.
//
// Globals expected at runtime (loaded via <script src=>):
// - amCharts 5 (`am5`, `am5xy`, `am5themes_Animated`, `am5locales_cs_CZ`)
// - Flatpickr (`flatpickr`) + Czech locale (`flatpickr.l10ns.cs`)
// NO jQuery dependency — vanilla DOM + fetch API only.

declare const am5: any;
declare const am5xy: any;
declare const am5themes_Animated: any;
declare const am5locales_cs_CZ: any;
declare const flatpickr: any;

interface Sample {
    date: string;
    h: number | null;
    q: number | null;
    t: number | null;
}

interface SpotInfo {
    type: number;
    spa0: number | null;
    spa1: number | null;
    spa2: number | null;
    spa3: number | null;
    spa3e: number | null;
    link: string | null;
    raftLink: string | null;
}

interface GetDataResponse {
    spot: SpotInfo;
    samples: Sample[];
}

interface UrlState {
    riverId: string | null;
    spotId: string | null;
    start: string | null;
    stop: string | null;
    type: string | null;
}

// ----- Chart lifecycle -------------------------------------------------------
let root: any = null;
let chart: any = null;
let xAxis: any = null;

function disposeChart(): void {
    if (root) {
        root.dispose();
        root = null;
        chart = null;
        xAxis = null;
    }
}

function generateChartData(data: Sample[]): Array<{ date: number; h: number | null; Q: number | null; t: number | null }> {
    return data.map((item) => ({
        date: new Date(item.date).getTime(),
        h: item.h,
        Q: item.q,
        t: item.t,
    }));
}

function createAxisAndSeries(field: "h" | "Q" | "t", name: string, opposite: boolean, color: string, chartData: ReturnType<typeof generateChartData>): void {
    const yRenderer = am5xy.AxisRendererY.new(root, {
        opposite,
        stroke: am5.color(color),
        strokeOpacity: 1,
        strokeWidth: 2,
    });
    yRenderer.grid.template.set("forceHidden", true);
    yRenderer.labels.template.set("fill", am5.color(color));

    const yAxis = chart.yAxes.push(am5xy.ValueAxis.new(root, { renderer: yRenderer }));

    const series = chart.series.push(
        am5xy.SmoothedXLineSeries.new(root, {
            name,
            xAxis,
            yAxis,
            valueXField: "date",
            valueYField: field,
            stroke: am5.color(color),
            fill: am5.color(color),
            tension: 0.8,
            tooltip: am5.Tooltip.new(root, { labelText: "{name}: [bold]{valueY}[/]" }),
        })
    );
    series.strokes.template.set("strokeWidth", 1);
    series.data.setAll(chartData);
    series.appear(500);
}

function createGraph(data: Sample[]): void {
    disposeChart();

    root = am5.Root.new("chartdiv");
    root.setThemes([am5themes_Animated.new(root)]);
    if (typeof am5locales_cs_CZ !== "undefined") {
        root.locale = am5locales_cs_CZ;
    }

    chart = root.container.children.push(
        am5xy.XYChart.new(root, {
            layout: root.verticalLayout,
            panX: true,
            panY: false,
            wheelX: "panX",
            wheelY: "zoomX",
            pinchZoomX: true,
        })
    );

    xAxis = chart.xAxes.push(
        am5xy.DateAxis.new(root, {
            baseInterval: { timeUnit: "minute", count: 1 },
            renderer: am5xy.AxisRendererX.new(root, { minGridDistance: 60 }),
            tooltip: am5.Tooltip.new(root, {}),
        })
    );
    xAxis.get("dateFormats")["day"] = "yyyy-MM-dd";
    xAxis.get("periodChangeDateFormats")["day"] = "yyyy-MM-dd";

    const chartData = generateChartData(data);
    createAxisAndSeries("h", "Hladina", false, "#009933", chartData);
    createAxisAndSeries("Q", "Průtok", true, "#0000cc", chartData);
    createAxisAndSeries("t", "Teplota", true, "#cc0000", chartData);

    chart.set("cursor", am5xy.XYCursor.new(root, { xAxis, behavior: "zoomX" }));

    const legend = chart.children.push(
        am5.Legend.new(root, { centerX: am5.percent(50), x: am5.percent(50) })
    );
    legend.data.setAll(chart.series.values);

    chart.appear(800, 100);
}

// ----- UI helpers ------------------------------------------------------------
function showChartLoading(): void {
    document.getElementById("chartLoading")?.classList.remove("is-hidden");
}
function hideChartLoading(): void {
    document.getElementById("chartLoading")?.classList.add("is-hidden");
}

// ----- DOM helpers -----------------------------------------------------------
function elById<T extends HTMLElement>(id: string): T {
    return document.getElementById(id) as T;
}

function setText(id: string, value: string | null): void {
    const el = document.getElementById(id);
    if (el) el.textContent = value ?? "";
}

function setLink(id: string, href: string | null, text?: string): void {
    const el = document.getElementById(id) as HTMLAnchorElement | null;
    if (!el) return;
    el.textContent = text ?? href ?? "";
    el.href = href || "#";
}

/** Show element (clears inline display:none so CSS/BS5 takes over). */
function showEl(id: string): void {
    const el = document.getElementById(id);
    if (el) el.classList.remove("d-none");
}

/** Hide element. */
function hideEl(id: string): void {
    const el = document.getElementById(id);
    if (el) el.classList.add("d-none");
}

// ----- URL state -------------------------------------------------------------
// Primary: query string ?riverId=2&spotId=5&start=…&stop=…&type=hQ
// Legacy fallback: hash format #2#5#…#…#hQ (preserves old bookmarks).
function isNumericString(s: string | null | undefined): boolean {
    return s != null && s !== "" && Number.isFinite(Number(s));
}

function readUrlState(): UrlState {
    const p = new URLSearchParams(window.location.search);
    if (p.has("riverId") || p.has("spotId")) {
        return {
            riverId: p.get("riverId"),
            spotId: p.get("spotId"),
            start: p.get("start"),
            stop: p.get("stop"),
            type: p.get("type"),
        };
    }

    const hashes = window.location.hash.split("#");
    return {
        riverId: hashes.length > 1 && isNumericString(hashes[1]) ? hashes[1] : null,
        spotId: hashes.length > 2 ? hashes[2] : null,
        start: hashes.length > 3 ? hashes[3] : null,
        stop: hashes.length > 4 ? hashes[4] : null,
        type: hashes.length > 5 ? hashes[5] : null,
    };
}

// Module-level mutable selection state.
const state: { riverId?: string; spotId?: string; start?: string; stop?: string; type?: string } = {};

function writeUrlState(): void {
    if (!isNumericString(state.riverId) || !isNumericString(state.spotId)) return;
    const p = new URLSearchParams();
    p.set("riverId", state.riverId!);
    p.set("spotId", state.spotId!);
    if (state.start) p.set("start", state.start);
    if (state.stop) p.set("stop", state.stop);
    if (state.type) p.set("type", state.type);
    history.replaceState(null, "", "?" + p.toString());
}

// ----- Spot info panels ------------------------------------------------------
function showSpotInformation(spot: SpotInfo): void {
    if (spot.type === 0) {
        hideEl("reservoirInformation");
        setText("river0", spot.spa0 == null ? "" : String(spot.spa0));
        setText("river1", spot.spa1 == null ? "" : String(spot.spa1));
        setText("river2", spot.spa2 == null ? "" : String(spot.spa2));
        setText("river3", spot.spa3 == null ? "" : String(spot.spa3));
        setText("river3e", spot.spa3e == null ? "" : String(spot.spa3e));
        setLink("riverLink", spot.link);
        if (spot.raftLink) {
            setLink("raftLink", spot.raftLink);
            showEl("raftLink");
        } else {
            hideEl("raftLink");
        }
        showEl("riverInformation");
    } else {
        hideEl("riverInformation");
        setText("korunaHrazeLbl", spot.spa3e == null ? "" : String(spot.spa3e));
        setText("kotaPrelivuLbl", spot.spa1 == null ? "" : String(spot.spa1));
        setText("maxRetHladinaLbl", spot.spa3 == null ? "" : String(spot.spa3));
        setText("hladZasProstLbl", spot.spa2 == null ? "" : String(spot.spa2));
        setText("hladStalNadtLbl", spot.spa0 == null ? "" : String(spot.spa0));
        setLink("reservoirLink", spot.link);
        if (spot.raftLink) {
            setLink("raftLink2", spot.raftLink);
            showEl("raftLink2");
        } else {
            hideEl("raftLink2");
        }
        showEl("reservoirInformation");
    }
}

function generateDataTable(samples: Sample[]): void {
    let html = "<thead><tr><th>Datum</th><th>Hladina</th><th>Průtok</th><th>Teplota</th></tr></thead><tbody>";
    for (const item of samples) {
        html += "<tr><td>" + item.date + "</td>"
            + "<td>" + (item.h != null ? item.h : "") + "</td>"
            + "<td>" + (item.q != null ? item.q : "") + "</td>"
            + "<td>" + (item.t != null ? item.t : "") + "</td></tr>";
    }
    html += "</tbody>";
    elById("dataTable").innerHTML = html;
}

function getSampleTypes(): string {
    return Array.from(document.querySelectorAll<HTMLInputElement>("input[type=checkbox]:checked"))
        .map(cb => cb.value)
        .join("");
}

// ----- HTTP helpers ----------------------------------------------------------
const SPOTS_URL = "/Graf/GetSpots/";
const DATA_URL = "/Graf/GetData/";

async function fetchSpots(riverId: string): Promise<Array<{ id: number; name: string }>> {
    const resp = await fetch(SPOTS_URL + riverId);
    if (!resp.ok) throw new Error(await resp.text());
    return resp.json() as Promise<Array<{ id: number; name: string }>>;
}

async function fetchData(spot: string, start: string, stop: string, type: string): Promise<GetDataResponse> {
    const params = new URLSearchParams({ spot, start, stop, type });
    const resp = await fetch(DATA_URL + "?" + params.toString());
    if (!resp.ok) throw new Error(await resp.text());
    return resp.json() as Promise<GetDataResponse>;
}

function populateSpotSelect(spots: Array<{ id: number; name: string }>): void {
    const sel = elById<HTMLSelectElement>("spot");
    sel.replaceChildren();
    for (const r of spots) {
        const opt = document.createElement("option");
        opt.value = String(r.id);
        opt.textContent = r.name;
        sel.appendChild(opt);
    }
}

// ----- Wiring ----------------------------------------------------------------
document.addEventListener("DOMContentLoaded", async () => {
    const riverEl = elById<HTMLSelectElement>("river");
    const spotEl = elById<HTMLSelectElement>("spot");
    const startEl = elById<HTMLInputElement>("start");
    const stopEl = elById<HTMLInputElement>("stop");
    const reloadEl = elById<HTMLButtonElement>("Reload");
    const hEl = elById<HTMLInputElement>("h");
    const qEl = elById<HTMLInputElement>("Q");
    const tEl = elById<HTMLInputElement>("t");

    // ----- Flatpickr init ---------------------------------------------------
    const fpConfig = {
        dateFormat: "d/m/Y",               // matches server ParseDateTime: dd/MM/yyyy
        locale: flatpickr?.l10ns?.cs ?? "default",
        allowInput: false,
        disableMobile: true,               // always use Flatpickr (not native mobile datepicker)
    };

    const startPicker = flatpickr(startEl, {
        ...fpConfig,
        onChange: (_: Date[], dateStr: string) => {
            state.start = dateStr;
            writeUrlState();
        },
    });

    const stopPicker = flatpickr(stopEl, {
        ...fpConfig,
        onChange: (_: Date[], dateStr: string) => {
            state.stop = dateStr;
            writeUrlState();
        },
    });

    // Calendar icon buttons open the respective pickers
    document.querySelectorAll<HTMLButtonElement>(".calendar-trigger").forEach(btn => {
        btn.addEventListener("click", () => {
            const target = btn.dataset["target"];
            if (target === "start") startPicker.open();
            else if (target === "stop") stopPicker.open();
        });
    });

    // ----- River select change ----------------------------------------------
    riverEl.addEventListener("change", async () => {
        state.riverId = riverEl.value;
        state.spotId = undefined;
        writeUrlState();

        try {
            const spots = await fetchSpots(state.riverId);
            populateSpotSelect(spots);
            if (spots.length === 1) {
                state.spotId = String(spots[0].id);
                writeUrlState();
            }
        } catch (err) {
            alert("Nastala chyba\n\n" + String(err));
        }
    });

    // ----- Spot select change -----------------------------------------------
    spotEl.addEventListener("change", () => {
        state.spotId = spotEl.value;
        writeUrlState();
    });

    // ----- Checkbox change --------------------------------------------------
    document.querySelectorAll<HTMLInputElement>("input[type=checkbox]").forEach(cb => {
        cb.addEventListener("change", () => {
            state.type = getSampleTypes();
            writeUrlState();
        });
    });

    // ----- Reload button ----------------------------------------------------
    reloadEl.addEventListener("click", async () => {
        const spotText = spotEl.selectedOptions[0]?.text ?? "";
        if (!spotText) {
            alert("Není vybrána žádná stanice!");
            return;
        }
        if (!state.type) {
            alert("Není vybrána žádná veličina!");
            return;
        }

        showChartLoading();
        try {
            const result = await fetchData(spotEl.value, startEl.value, stopEl.value, state.type);
            createGraph(result.samples);
            showSpotInformation(result.spot);
            generateDataTable(result.samples);
        } catch (err) {
            alert("Nastala chyba\n\n" + String(err));
        } finally {
            hideChartLoading();
        }
    });

    // ----- Initial state + auto-load if spotId in URL -----------------------
    const initial = readUrlState();
    state.riverId = initial.riverId ?? undefined;
    state.spotId = initial.spotId ?? undefined;
    state.start = initial.start ?? startEl.value;
    state.stop = initial.stop ?? stopEl.value;

    if (initial.type) {
        state.type = initial.type;
        hEl.checked = state.type.includes("h");
        qEl.checked = state.type.includes("Q");
        tEl.checked = state.type.includes("t");
    } else {
        state.type = getSampleTypes();
    }

    if (state.spotId) {
        showChartLoading();
        riverEl.value = state.riverId!;

        // Restore date inputs (Flatpickr reads current value on init,
        // but set explicitly via API to ensure the picker state matches).
        if (state.start) {
            startEl.value = state.start;
            startPicker.setDate(state.start, false);
        }
        if (state.stop) {
            stopEl.value = state.stop;
            stopPicker.setDate(state.stop, false);
        }

        try {
            const spots = await fetchSpots(state.riverId!);
            populateSpotSelect(spots);
            spotEl.value = state.spotId!;
            reloadEl.click();
        } catch {
            hideChartLoading();
        }
    }
});
