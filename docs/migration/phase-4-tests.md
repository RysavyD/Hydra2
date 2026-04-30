# Fáze 4 — Testy

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ `dotnet build Hydra2.net8.sln` — 0 warning, 0 error
> Tests: ✅ **60 / 60 passed** (~700 ms)

---

## Cíl

Safety net před dalšími změnami — zejména pro křehké HTML scrapery a per-source jobs.

---

## Co bylo uděláno

### 1. Test projekt
[`tests/Hydra2.Tests/Hydra2.Tests.csproj`](../../tests/Hydra2.Tests/Hydra2.Tests.csproj) — single test project pokrývající všechny vrstvy.

**NuGet závislosti**:
- xUnit 2.9 — runner
- FluentAssertions 6.12 — readable assertions (`should().be(...)`)
- NSubstitute 5.3 — mocking (preferováno před Moq)
- Microsoft.AspNetCore.Mvc.Testing 8.0 — `WebApplicationFactory<Program>`
- Microsoft.Extensions.TimeProvider.Testing 8.10 — `FakeTimeProvider` pro testy s časovým oknem
- coverlet.collector 6.0 — code coverage (XPlat Code Coverage)

**Globální usings** v csproj:
```xml
<Using Include="FluentAssertions" />
<Using Include="Xunit" />
```

### 2. Pokrytí

#### Pure-logic unit tests
- **SampleTableNameTests** (8 testů) — padding 3 číslic, validace rozsahu 0-999
- **SourceCatalogTests** (10 testů) — unikátní DownLoadType, mapování, case-insensitive lookup
- **SourceStateTrackerTests** (7 testů) — Success/Failure/PartialFailure outcome handling
- **StationErrorTrackerTests** (9 testů) — throttle window, různé exception typy, GetReport ordering

#### Scraper testy
- **BaseDownloaderTests** (5 testů) — synthetic HTML s comma/dot decimal, skip non-zero minutes, missing temperature column, unparseable datetime, empty cells
- **ScraperSnapshotTests** (4 testů) — fixture-based testy pro Chmi/Pvl/PmoNadrze
  - HTML fixture v `tests/Hydra2.Tests/Fixtures/`
  - StaticHandler s injektovanou HTML response (mimo HTTP)
  - Verifikuje konkrétní vzorky (Level/Flow/Temperature) a počty

#### Web/integration testy
- **HomeControllerTests** (3 testy) — `/`, `/Home/About`, `/Home/Contact` vrací 200
- **HeartbeatTests** (3 testy):
  - 200 OK když DB healthy
  - 503 když ConfigService.GetFirstConfigAsync hodí výjimku
  - Sources array obsahuje 6 položek
- **ApiAuthorizationTests** (7 testů):
  - `/api/admin/logs/level`, `/api/admin/failing-stations`, `/api/jobs/status` vrací 401 bez tokenu
  - Stejné endpointy 401 s wrong tokenem
  - `/api/jobs/trigger/{name}` 404 pro neznámé jméno

#### TestWebApplicationFactory
[`tests/Hydra2.Tests/Web/TestWebApplicationFactory.cs`](../../tests/Hydra2.Tests/Web/TestWebApplicationFactory.cs)
- Substituuje `IDataService`, `IConfigService`, `IAdminService`, `IUpdateService` přes NSubstitute
- Test může konfigurovat substituty per-test
- Nepoužívá DB — vše v paměti
- `Program.cs` má `public partial class Program;` aby `WebApplicationFactory<Program>` fungoval

### 3. Snapshot/fixture pattern pro scrapery

Tři fixture soubory v `tests/Hydra2.Tests/Fixtures/`:
- `chmi-station.html` — formát ČHMÚ s `<div class="tborder center_text">` a vnořenou tabulkou, dot decimal separator
- `pvl-mereni.html` — `<table id="ObsahCPH_DataMereniGV">`, comma decimal, čeština DD.MM.YYYY
- `pmoNadrze-mereni.html` — `<table width="300">` (ne první matching table), comma decimal

```csharp
[Fact]
public async Task Chmi_parses_fixture_correctly()
{
    var html = await ReadFixture("chmi-station.html");
    var scraper = new Chmi(MakeClient(html), NullLogger<Chmi>.Instance);

    var records = await scraper.GetRecordsAsync("http://test/");

    records.Should().HaveCount(3); // jen minute=0 záznamy
    records.Should().Contain(r => r.Level == 118f && r.Flow == 5.42f);
}
```

**Když zdrojový web změní layout** → fixture se musí re-saveovat z reálné HTTP response, snapshot test selže = včasná detekce regrese parsingu.

### 4. Souběžné opravy v src/ které testy odhalily

#### a) `BaseDownloader.LoadData` — culture-aware DateTime parsing
Před opravou: `DateTime.TryParse(tds[0], out var dt)` používal `CurrentCulture`. V produkci bylo `cs-CZ` nastaveno per request (přes `Update.LastSpotsLoopAsync`), v testech zůstávalo invariant → DD.MM.YYYY z PVL/PMO neparsovalo.

Fix: explicitní culture fallback v `BaseDownloader`:
```csharp
private static readonly CultureInfo CzechCulture = CultureInfo.GetCultureInfo("cs-CZ");
...
if (!DateTime.TryParse(tds[0], CzechCulture, DateTimeStyles.None, out var dt) &&
    !DateTime.TryParse(tds[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
    continue;
```

Plus defensivní `if (tds.Length < 3) continue;` před přístupem k `tds[1]`, `tds[2]` — řeší 174 `IndexOutOfRangeException` z prod logu (Fáze 0 inventarizace).

#### b) `StationErrorTracker.GetReport(topN)` — invalid runtime cast
```csharp
// Před (failing):
if (topN.HasValue) query = (IOrderedEnumerable<StationErrorReport>)query.Take(topN.Value);

// Po:
IEnumerable<StationErrorReport> query = ...;
if (topN.HasValue) query = query.Take(topN.Value);
```

Test `GetReport_topN_limits_results` chytil chybu která by jinak prošla na produkci.

#### c) `StationErrorTracker` — `TimeProvider` pro deterministické testy
```csharp
public StationErrorTracker(TimeSpan stackTraceThrottle, TimeProvider timeProvider) { ... }
```
Defaultní konstruktor používá `TimeProvider.System` + `TimeSpan.FromHours(1)`. Test používá `FakeTimeProvider` z `Microsoft.Extensions.TimeProvider.Testing`:
```csharp
var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
var tracker = new StationErrorTracker(TimeSpan.FromHours(1), time);
tracker.RecordError(1, ex).Should().BeTrue();
time.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));
tracker.RecordError(1, ex).Should().BeTrue(); // throttle vypršel
```

#### d) Admin/Jobs controllery — `string?` místo `string` pro token parametr
S `[ApiController]` + non-nullable `string token` parametrem ASP.NET Core 8 vrací **400 BadRequest** při chybějícím tokenu (model validation). Naše IsAuthorized očekávalo `null` token a vrátilo by 401. Test `Admin_endpoints_require_token` to chytil.

Fix: `string? token` ve všech admin/jobs endpointech.

### 5. GitHub Actions CI
[`.github/workflows/ci.yml`](../../.github/workflows/ci.yml):
- Triggers: push do master/main/net8-migration, PR do master/main, ručně
- Setup .NET 8 SDK
- `dotnet restore` + `dotnet build --configuration Release`
- `dotnet test` s TRX logger + code coverage
- Upload test results jako artifact (14 dní retention)

Build status badge se přidá do README po prvním passing buildu.

---

## Statistika

| | Počet |
|---|---|
| Test projektů | 1 |
| Test souborů | 9 |
| Testů celkem | **60** |
| Failed | 0 |
| Skipped | 0 |
| Doba běhu | ~700 ms |
| Líne testovaného kódu (src/) | ~1 300 |

### Distribuce testů
| Kategorie | Testů |
|---|---|
| Pure-logic unit tests | 34 |
| Scraper unit (BaseDownloader) | 5 |
| Scraper snapshot tests | 4 |
| WebApplicationFactory smoke | 6 |
| Authorization | 7 |
| Misc | 4 |

---

## Co je úmyslně **mimo scope** Fáze 4

| Položka | Důvod |
|---|---|
| Testcontainers SQL Server | Vyžaduje Docker, brutálně zvyšuje run time, sdílený hosting nemá Docker. Přijde s persistencí Quartz (Phase 3.5+). |
| End-to-end browser tests | Není potřeba pro tento UI. Manuální smoke testing po deployi stačí. |
| Performance / load testy | Hydra má ~5 uživatelů. Není to issue. |
| Mutation testing | Overkill pro projekt téhle velikosti. |
| Test pro `DataService` přes reálnou DB | Integration s reálnou Forpsi DB se hodí udělat ručně po deployi staging. |

---

## Příklad spuštění

```bash
# Build + všechny testy
dotnet test Hydra2.net8.sln

# Jen unit testy
dotnet test Hydra2.net8.sln --filter "FullyQualifiedName~Hydra2.Tests.Downloaders"

# S verbose output
dotnet test Hydra2.net8.sln --logger "console;verbosity=detailed"

# Code coverage report
dotnet test Hydra2.net8.sln --collect "XPlat Code Coverage"
```

---

## TL;DR

✅ 60 testů / 0 failure / běh ~700 ms
✅ xUnit + FluentAssertions + NSubstitute + WebApplicationFactory
✅ Snapshot fixture pattern pro 3 hlavní scrapery (Chmi, Pvl, PmoNadrze)
✅ FakeTimeProvider pro deterministické testy throttle window
✅ 4 reálné bugy chytlé testy a opraveny (culture-parsing, runtime cast, NRE protection, 400→401)
✅ GitHub Actions CI workflow — build + test při push

**Po této fázi**: jakýkoliv refaktor v Phase 5+ (HTTP hardening) má safety net. Pokud něco rozbiji, CI to chytí dřív, než se to dostane na produkci.
