# Fáze 1 — Migrace na .NET 8

> Datum: 2026-04-29
> Větev: `net8-migration`
> Build status: ✅ `dotnet build Hydra2.net8.sln` — 0 warning, 0 error

---

## Cíl fáze
Vytvořit paralelní .NET 8 strom v `src/`, aby šlo původní Hydra2 i novou verzi buildit a deploynout side-by-side. Logování ponecháno na Fázi 2; Quartz reorganizace na Fázi 3.

## Co bylo uděláno

### 1. Solution + projekty
Nové **`Hydra2.net8.sln`** s 4 SDK-style projekty (target `net8.0`):

```
src/
├── Directory.Build.props        ← společné nastavení (TargetFramework, Nullable, ImplicitUsings)
├── Hydra2.Service/              ← Dapper + Microsoft.Data.SqlClient
├── Hydra2.Downloaders/          ← HtmlAgilityPack 1.11 + IHttpClientFactory
├── Hydra2.Web/                  ← ASP.NET Core MVC 8 + Quartz 3 + Serilog
└── Hydra2.DownLoad/             ← Console tester (zachováno na žádost)
```

Originální projekty (`Hydra2.Web`, `Hydra2.Service`, ...) ponechány netknuté — `Hydra2.sln` se nezměnila. Side-by-side build.

### 2. Hydra2.Service (port)
- **Async API**: `GetRiversAsync`, `GetStationsAsync`, `GetStationAsync`, `GetSamplesAsync`, `AddSampleAsync`
- **Microsoft.Data.SqlClient 5.2** (modernější nástupce `System.Data.SqlClient`)
- **Dapper 2.1.35** s `CommandDefinition` + `CancellationToken`
- **DI ready**: rozhraní `IDataService`, `IAdminService`, `IConfigService` + `AddHydra2Services(configuration)`
- **`Hydra2Options`** s `ConnectionString` (binding na `Hydra2:ConnectionString` v `appsettings.json`)
- **`SampleTableName.ForStation(int)`** — generátor `Sample-NNN` jmen tabulek s **regex whitelistem `^Sample-\d{3}$`**. Odstraňuje SQLi anti-pattern (i když dříve byl input bezpečný, tohle je obrana proti budoucím chybám).
- **`FakeDataService`** ponechán pro dev / testy (taky async)

### 3. Hydra2.Downloaders (port)
- Třídy `Chmi`, `Pvl`, `PvlNadrze`, `PlaNadrze`, `PmoNadrze`, `PmoToky` portnuté na **async**
- **`HttpClient` přes `IHttpClientFactory`** (`AddHttpClient<T>(...)`) místo `WebClient` — moderní, lifetime-managed, automatické connection pooling
- **HtmlAgilityPack 1.11.71** (z 1.4.9 z roku 2014)
- **`IDownloaderFactory`** + **`DownloaderFactory`** — místo `Update.GetDownloader(int)` static switch
- **`UpdateService` (`IUpdateService`)** — port `Update.cs`:
  - `UpdateSpotsAsync(start, stop, ct)` — single batch
  - `LastSpotsLoopAsync(ct)` — odpovídá `while(true)` loopu, ale **respektuje `CancellationToken`** (gracefulní shutdown)
  - `UpdateNextSpotAsync(ct)` — nový, atomické "stáhni další stanici" pro trigger endpoint
- **`IUpdateProgressListener`** (s `NullUpdateProgressListener` jako default) — observer pattern pro reporting iterací do heartbeatu (Web vrstva má vlastní implementaci `TrackerProgressListener`)
- User-Agent v HttpClient: `Hydra2/2.0 (+https://hydra2.dusanrysavy.cz)`
- Timeout 30s na request

### 4. Hydra2.Web (ASP.NET Core 8)
- **`Program.cs`** s top-level statements
- **`appsettings.json`** + **`appsettings.Development.json`** + **`appsettings.Production.json`** (template — viz §6 níže)
- **`Properties/launchSettings.json`** pro F5 debugging
- **Razor views** kompletně portnuty (Layout, Home/*, Graf/Index, Adm/*, Shared/Error). Tag helpers (`asp-controller`, `asp-action`) místo `@Html.ActionLink`
- **Static files** v `wwwroot/`:
  ```
  wwwroot/
  ├── css/site.css
  ├── js/Hydra2.js
  ├── lib/jquery/jquery.min.js               ← jQuery 1.10.2 (zatím — viz §7)
  ├── lib/jquery-validation/...
  ├── lib/bootstrap/{css,js,fonts}/...        ← Bootstrap 3.4.1
  ├── lib/bootbox/bootbox.min.js
  ├── lib/daterangepicker/...
  └── favicon.ico
  ```
  amCharts načítány z CDN (jako dříve)
- **Modernizr a Respond.js vyhozeny** — neslouží žádnému účelu v moderních prohlížečích (UI se neměnilo)
- **Bundling/Minifikace zatím vypnuty** (přímé reference na .min.js soubory) — WebOptimizer / build pipeline lze přidat v některé pozdější fázi
- **DI registrace v `Program.cs`**:
  - `AddHydra2Services(configuration)`
  - `AddHydra2Downloaders()`
  - `SchedulerStateTracker` (singleton)
  - `IUpdateProgressListener` → `TrackerProgressListener`
  - Quartz: `UpdateLastJob` s triggerem `StartNow()` (zachováno chování z původu — Fáze 3 přepíše)

### 5. Heartbeat endpoint — `/api/heartbeat` (NOVÝ)
**Účel**: nahradit pingování homepage chytrou alternativou pro cron-job.org.

**Soubor**: [src/Hydra2.Web/Controllers/HeartbeatController.cs](../../src/Hydra2.Web/Controllers/HeartbeatController.cs)

**Ukázková odpověď**:
```json
{
  "now": "2026-04-29T14:32:11Z",
  "uptime": "0.01:23:45",
  "version": "1.0.0.0",
  "scheduler": {
    "running": true,
    "loopStarted": "2026-04-29T13:09:00Z",
    "lastIteration": "2026-04-29T14:31:55Z",
    "currentStationId": 234
  },
  "status": "OK"
}
```

**Status hodnoty**:
- `"OK"` — všechno běží, poslední iterace < 15 min
- `"STALE"` — loop běží ale poslední iterace > 15 min (zaseklý scrape?)
- `"STARTING"` — loop ještě nezačal iterovat
- `"DEGRADED"` — DB neresponduje (cannot read Config)

**Vlastnosti**:
- Žádná autorizace (read-only diagnostika, nic citlivého)
- Probudí app pool stejně jako jakýkoliv jiný HTTP request
- Cron-job.org může nastavit assert na status = "OK" → e-mail při výpadku

### 6. Konfigurace pro Forpsi (template)

**`appsettings.Production.json`** obsahuje template hodnoty — **vyplníš při ručním nasazení**:

```json
{
  "Hydra2": {
    "ConnectionString": "Server=__FORPSI_SQL_HOST__;Database=__DB_NAME__;User Id=__DB_USER__;Password=__DB_PASSWORD__;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Auth": {
    "SecretToken": "__GENERATE_NEW_TOKEN__"
  }
}
```

**Doporučení pro nasazení**:
- Místo úprav `appsettings.Production.json` v gitu **používej environment variables** v admin panelu Forpsi:
  - `Hydra2__ConnectionString`
  - `Auth__SecretToken`
- Tajemství tak nikdy neletí v repu
- `SecretToken` vygeneruj nový (např. `openssl rand -hex 32`), nepoužívej `myToken`

### 7. Co je úmyslně **nezměněno** v této fázi

| Co | Proč |
|---|---|
| Logování úrovně default `Information` | Phase 2 bude tunit (budeš poskytovat sample logu) |
| `LastSpotsLoopAsync` má pořád `while(!ct)` | Phase 3 přepíše na cron-based per-source jobs |
| jQuery 1.10.2 | Stability-first — Phase 5 update na 3.7.x (existují známé XSS vulns, ale UI musí zůstat) |
| Bootstrap 3.4.1 | Uživatelé zvyklí na UI |
| `UpdateLastJob` s `StartNow()` | Zachované chování — Fáze 3 přepíše |
| DB schéma `Sample-###` | Dle dohody — **nikdy** neunifikovat |

### 8. Nové bezpečnostní vylepšení **provedené v této fázi**

| Co | Kde | Dříve | Teď |
|---|---|---|---|
| SQLi whitelist `Sample-NNN` | `SampleTableName.ForStation` | Interpolace bez validace | Regex whitelist `^Sample-\d{3}$` |
| Auth na `/Api/HandUpdate` | `ApiController.HandUpdate` | **Žádná auth** | Vyžaduje `?token=` |
| Connection string mimo git | `appsettings.json` template + Forpsi env vars | Hardcoded v `Web.config` | Env var override |
| `secretToken` mimo git | Stejně | Hardcoded `myToken` | Env var override + nový token |

---

## Build & spuštění lokálně

```bash
# Build
dotnet build Hydra2.net8.sln

# Web (development)
cd src/Hydra2.Web
dotnet run

# Console tester
cd src/Hydra2.DownLoad
dotnet run
```

Default URL: `https://localhost:7001` (viz `Properties/launchSettings.json`).

---

## Změny v repu

```
docs/migration/phase-1-net8.md     ← tento dokument
Hydra2.net8.sln                    ← nová solution
src/                               ← nový strom (4 projekty + wwwroot)
```

Soubory **netknuté**: `Hydra2.sln`, `Hydra2.Web/*`, `Hydra2.Service/*`, `Hydra2.Downloaders/*`, `Hydra2.Model/*`, `Hydra.DownLoad/*`, `SampleTableSeparator/*`. Plně reverzibilní.

---

## Plán pro deploy na staging

> Budeš dělat ručně, postup pro orientaci:

1. **Připrav Forpsi staging** s .NET 8 hostingem (admin panel)
2. **Nastav environment variables** (admin panel Forpsi):
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `Hydra2__ConnectionString=Server=...;Database=...;User Id=...;Password=...;Encrypt=True`
   - `Auth__SecretToken=<vygenerovaný 32bytový hex>`
3. **Publish** lokálně:
   ```bash
   cd src/Hydra2.Web
   dotnet publish -c Release -o ./publish
   ```
4. **FTP upload** obsahu `./publish/` do staging adresáře
5. **Otestuj**:
   - `GET https://hydra2.dusanrysavy.cz/` (homepage)
   - `GET https://hydra2.dusanrysavy.cz/Graf` (graf)
   - `GET https://hydra2.dusanrysavy.cz/api/heartbeat` (status)
6. **Přesměruj cron-job.org** z `/` na `/api/heartbeat` (5 min interval, assert na `"status":"OK"`)

---

## Otevřené body pro Fázi 2 (logging)

1. Sample produkčního logu (10 MB) — **čekám**
2. Konkretní Serilog konfigurace pro snížení objemu (defaultní level, sampling, rolling rules)
3. Případně přepnutí static `NLogger` zbytků v původním kódu — tady už nejsou, vše jde přes `ILogger<T>`

---

## TL;DR

✅ .NET 8 verze sestavena, 4 projekty, 0 warning  
✅ Heartbeat endpoint `/api/heartbeat` připravený pro cron-job.org  
✅ DB schéma a UI zachovány  
✅ Connection string a token jsou jen template — vyplníš při deployi  
✅ Původní `Hydra2.sln` nezměněna — možno paralelně buildit obě verze  
⏭️  Pokračujeme Fází 2 (logging) jakmile dodáš sample logu
