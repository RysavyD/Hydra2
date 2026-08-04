// Graf page — chart rendering + UI wiring for Hydra2 Graf/Index.cshtml.
// Bundled via esbuild to wwwroot/js/graf.min.js.
//
// Globals expected at runtime (loaded via <script src=>):
// - jQuery 3.7+ (`$`)
// - amCharts 5 (`am5`, `am5xy`, `am5themes_Animated`, `am5locales_cs_CZ`)
// - moment.js (`moment`)
// - daterangepicker (jQuery plugin attached to `$.fn`)

declare const am5: any;
declare const am5xy: any;
declare const am5themes_Animated: any;
declare const am5locales_cs_CZ: any;

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
    $("#chartLoading").removeClass("is-hidden");
}
function hideChartLoading(): void {
    $("#chartLoading").addClass("is-hidden");
}

// ----- URL state -------------------------------------------------------------
// Primary: query string ?riverId=2&spotId=5&start=…&stop=…&type=hQ
// Legacy fallback: hash format #2#5#…#…#hQ (preserves old bookmarks).
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
        riverId: hashes.length > 1 && $.isNumeric(hashes[1]) ? hashes[1] : null,
        spotId: hashes.length > 2 ? hashes[2] : null,
        start: hashes.length > 3 ? hashes[3] : null,
        stop: hashes.length > 4 ? hashes[4] : null,
        type: hashes.length > 5 ? hashes[5] : null,
    };
}

// Module-level mutable selection state. Kept as a tuple for clarity.
const state: { riverId?: string; spotId?: string; start?: string; stop?: string; type?: string } = {};

function writeUrlState(): void {
    if (!$.isNumeric(state.riverId) || !$.isNumeric(state.spotId)) return;
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
        $("#reservoirInformation").hide();
        $("#river0").text(spot.spa0 == null ? "" : String(spot.spa0));
        $("#river1").text(spot.spa1 == null ? "" : String(spot.spa1));
        $("#river2").text(spot.spa2 == null ? "" : String(spot.spa2));
        $("#river3").text(spot.spa3 == null ? "" : String(spot.spa3));
        $("#river3e").text(spot.spa3e == null ? "" : String(spot.spa3e));
        $("#riverLink").text(spot.link ?? "").attr("href", spot.link || "#");
        if (spot.raftLink) {
            $("#raftLink").text(spot.raftLink).attr("href", spot.raftLink).show();
        } else {
            $("#raftLink").hide();
        }
        $("#riverInformation").show();
    } else {
        $("#riverInformation").hide();
        $("#korunaHrazeLbl").text(spot.spa3e == null ? "" : String(spot.spa3e));
        $("#kotaPrelivuLbl").text(spot.spa1 == null ? "" : String(spot.spa1));
        $("#maxRetHladinaLbl").text(spot.spa3 == null ? "" : String(spot.spa3));
        $("#hladZasProstLbl").text(spot.spa2 == null ? "" : String(spot.spa2));
        $("#hladStalNadtLbl").text(spot.spa0 == null ? "" : String(spot.spa0));
        $("#reservoirLink").text(spot.link ?? "").attr("href", spot.link || "#");
        if (spot.raftLink) {
            $("#raftLink2").text(spot.raftLink).attr("href", spot.raftLink).show();
        } else {
            $("#raftLink2").hide();
        }
        $("#reservoirInformation").show();
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
    $("#dataTable").html(html);
}

function getSampleTypes(): string {
    let selected = "";
    $("input[type=checkbox]:checked").each(function () {
        selected += $(this).val();
    });
    return selected;
}

// ----- Daterangepicker config (unchanged from inline version) ----------------
const dateRangeOptions: any = {
    showDropdowns: true,
    autoApply: true,
    locale: {
        format: "DD/MM/YYYY",
        separator: " - ",
        applyLabel: "Apply",
        cancelLabel: "Cancel",
        fromLabel: "From",
        toLabel: "To",
        customRangeLabel: "Custom",
        daysOfWeek: ["Ne", "Po", "Út", "St", "Čt", "Pá", "So"],
        monthNames: ["Leden", "Únor", "Březen", "Duben", "Květen", "Červen", "Červenec", "Srpen", "Září", "Říjen", "Listopad", "Prosinec"],
        firstDay: 1,
    },
    singleDatePicker: true,
    linkedCalendars: false,
    autoUpdateInput: false,
    buttonClasses: "btn btn-lg",
};

// ----- Wiring ----------------------------------------------------------------
const SPOTS_URL = "/Graf/GetSpots/";
const DATA_URL = "/Graf/GetData/";

$(function () {
    $("#river").on("change", function () {
        state.riverId = $(this).val() as string;
        state.spotId = undefined;
        writeUrlState();

        $.ajax({
            type: "GET",
            url: SPOTS_URL + state.riverId,
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: (result: Array<{ id: number; name: string }>) => {
                const sel = $("#spot");
                sel.empty();
                for (const r of result) {
                    sel.append('<option value="' + r.id + '">' + r.name + "</option>");
                }
                if (result.length === 1) {
                    state.spotId = String(result[0].id);
                    writeUrlState();
                }
            },
            error: (xhr) => {
                alert("Nastala chyba \n\n" + xhr.responseText);
            },
        });
    });

    $("#spot").on("change", function () {
        state.spotId = $(this).val() as string;
        writeUrlState();
    });

    $(".rangeinput i").on("click", function () {
        $(this).parent().find("input").trigger("click");
    });

    ($("#start") as any).daterangepicker(dateRangeOptions, function (s: any) {
        $("#start").val(s.format("DD/MM/YYYY"));
    });
    $("#start").on("apply.daterangepicker", function () {
        state.start = $("#start").val() as string;
        writeUrlState();
    });

    ($("#stop") as any).daterangepicker(dateRangeOptions, function (s: any) {
        $("#stop").val(s.format("DD/MM/YYYY"));
    });
    $("#stop").on("apply.daterangepicker", function () {
        state.stop = $("#stop").val() as string;
        writeUrlState();
    });

    $("input[type=checkbox]").on("change", function () {
        state.type = getSampleTypes();
        writeUrlState();
    });

    $("#Reload").on("click", function () {
        const spot = $("#spot option:selected");
        if (spot.text() === "") {
            alert("Není vybrána žádná stanice!");
            return;
        }
        if (!state.type) {
            alert("Není vybrána žádná veličina!");
            return;
        }

        showChartLoading();
        $.ajax({
            type: "GET",
            url: DATA_URL,
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            data: { spot: spot.val(), start: $("#start").val(), stop: $("#stop").val(), type: state.type },
            success: (result: GetDataResponse) => {
                createGraph(result.samples);
                showSpotInformation(result.spot);
                generateDataTable(result.samples);
                hideChartLoading();
            },
            error: (xhr) => {
                hideChartLoading();
                alert("Nastala chyba \n\n" + xhr.responseText);
            },
        });
    });

    // ---- Initial state + auto-load if spotId in URL ----
    const initial = readUrlState();
    state.riverId = initial.riverId ?? undefined;
    state.spotId = initial.spotId ?? undefined;
    state.start = initial.start ?? ($("#start").val() as string);
    state.stop = initial.stop ?? ($("#stop").val() as string);

    if (initial.type) {
        state.type = initial.type;
        $("#h").prop("checked", state.type.includes("h"));
        $("#Q").prop("checked", state.type.includes("Q"));
        $("#t").prop("checked", state.type.includes("t"));
    } else {
        state.type = getSampleTypes();
    }

    if (state.spotId) {
        showChartLoading();
        $("#river").val(state.riverId!);
        $("#start").val(state.start!);
        $("#stop").val(state.stop!);

        $.ajax({
            type: "GET",
            url: SPOTS_URL + state.riverId,
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: (result: Array<{ id: number; name: string }>) => {
                const sel = $("#spot");
                sel.empty();
                for (const r of result) {
                    sel.append('<option value="' + r.id + '">' + r.name + "</option>");
                }
                $("#spot").val(state.spotId!);
                $("#Reload").trigger("click");
            },
            error: () => {
                hideChartLoading();
            },
        });
    }
});
