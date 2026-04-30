# Fáze 2 — Logování

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ `dotnet build Hydra2.net8.sln` — 0 warning, 0 error

---

## Vstupní stav (analýza produkčního logu 2026-04-29)

| Metrika | Hodnota |
|---|---|
| Velikost log souboru | **109 MB** |
| Počet řádků | 1 628 730 |
| Info | 1 053 890 (64,7 %) |
| Debug | 418 928 (25,7 %) |
| Error (vč. stack traces) | ~150 000 |
| Cyklů (otoček 0..650) | ~340 / den |

### Top opakující se zprávy
| Zpráva | Per den | Per stanice |
|---|---|---|
| `Načten config: N` | 222 024 | každá iterace |
| `Stanice: X` | 220 319 | každá iterace |
| `Stahuji ze stránky:` | 220 318 | každá iterace |
| `DownloaderType:` | 220 318 | každá iterace |
| `Vzorků nalezeno:` | 195 394 | po úspěchu |
| `Uloženo` | 195 380 | po úspěchu |
| `Vzorků: N, ukládám` | 195 380 | po úspěchu |

### Distribuce výjimek
| Výjimka | Počet/den | Diagnóza |
|---|---|---|
| `InvalidOperationException: Sequence contains no matching element` | 11 595 | Scraper nenajde očekávaný element — buď zdrojový web změnil layout, nebo stanice má broken Link |
| `WebException: connection closed` | 9 075 | TLS / network transient |
| `WebException: 500` | 3 210 | Server chyba na zdrojovém webu |
| `WebException: 410 Gone` | 682 | Stanice trvale zmizela |
| `IndexOutOfRangeException` | 174 | Tabulka má méně sloupců než scraper čeká |
| `NullReferenceException` | 62 | `GetElementbyId` vrátí null |

---

## Co bylo uděláno

### 1. Per-station Info logy → Debug (`UpdateService` + `BaseDownloader`)
Při defaultním `MinimumLevel: Information` v produkci se per-stanice logy **NEZAPISUJÍ**. Každý log řádek per stanice má teď úroveň:
- `Debug` — `"Updating station X (Spot)"`, `"Station X ok, N samples added"`, `"Downloading from {Link}"`, `"Parsed N samples from {Link}"`

Runtime přepnutí na Debug možné přes `/api/admin/logs/level` (viz §4).

### 2. Cycle summary log (1 řádek ~340×/den)
[`UpdateService.LastSpotsLoopAsync`](../../src/Hydra2.Downloaders/UpdateService.cs) detekuje wraparound čítače `Hydra.Config.Value` (650 → 0) a emituje:
```
Cycle complete: 650 attempts, 575 ok, 5 skipped, 70 errors, 110 000 samples added, took 00:04:13
```

Statistiky drží singleton **`ICycleStats` / `CycleStats`** ([src/Hydra2.Downloaders/CycleStats.cs](../../src/Hydra2.Downloaders/CycleStats.cs)):
- `RecordIteration(stationId, outcome, samplesAdded)` per stanice (called from `UpdateSingleStationAsync`)
- `CompleteCycle()` při wraparound — vrátí snapshot a resetuje counter
- `GetCurrent()` / `GetLastCompleted()` pro heartbeat endpoint

### 3. Throttled error logging (`StationErrorTracker`)
[src/Hydra2.Downloaders/StationErrorTracker.cs](../../src/Hydra2.Downloaders/StationErrorTracker.cs) drží:
- Per-stanice: error count, success count, first/last error, errors-by-type
- Per `(stationId, exceptionType)`: kdy byl naposled zalogován stack trace

Při chybě v `UpdateService.UpdateSingleStationAsync`:
- **První výskyt** kombinace `(stationId, ExceptionType)` v okně 1h → log s plným stack trace (`LogWarning(ex, ...)`)
- **Opakované výskyty** → kompaktní 1-line `Station 234 failed: WebException: The remote server returned (500)`

Throttle window: **1 hodina** (konstanta `StackTraceThrottle` v `StationErrorTracker`).

Predikované úspory:
- 25 000 chyb × 5 řádků = 125 000 řádků dnes
- Po Fázi 2: ~25 000 chyb, většina kompaktní, ~30 stanic × 7 typů = ~210 stack traces / hodinu max teoretický strop = ~5 000 řádků realisticky

### 4. Runtime přepnutí log levelu
[`AdminApiController.SetLogLevel`](../../src/Hydra2.Web/Controllers/AdminApiController.cs):

```bash
# Aktuální stav
GET /api/admin/logs/level?token=<token>
→ { "current": "Information" }

# Přepnutí na Debug (např. pro 30min diagnostiku)
POST /api/admin/logs/level?token=<token>&level=Debug
→ { "previous": "Information", "current": "Debug" }

# Zpět na Information
POST /api/admin/logs/level?token=<token>&level=Information
```

Implementováno přes Serilog **`LoggingLevelSwitch`** + `MinimumLevel.ControlledBy()` v `Program.cs`. Změna se projeví **okamžitě bez redeployu**.

### 5. Failing stations endpoint
[`GET /api/admin/failing-stations?token=<>&topN=20&minErrorRate=0.5`](../../src/Hydra2.Web/Controllers/AdminApiController.cs):

```json
{
  "count": 12,
  "generatedAt": "2026-04-30T10:23:45Z",
  "stations": [
    {
      "stationId": 263,
      "errors": 415,
      "successes": 0,
      "errorRate": 1.0,
      "firstError": "2026-04-30T08:15:02Z",
      "lastError": "2026-04-30T10:22:11Z",
      "errorsByType": {
        "InvalidOperationException": 415
      }
    },
    ...
  ]
}
```

Použití pro DB cleanup — stanice s `errorRate=1.0` a stovkami chyb jsou kandidáti na:
- Aktualizaci `Hydra.Station.Link` (zdroj přesunul URL)
- Změnu `Hydra.Station.DownLoadType` (zdroj změnil layout, jiný scraper sedí)
- Označení neaktivní (zatím nemáš sloupec `Active` — Phase 3 kandidát)

> **Pozor**: tracker je v paměti, resetuje se při restartu app poolu. Persistentní tracking se hodí udělat ve Fázi 3 spolu s AdoJobStore.

### 6. Heartbeat enriched
`/api/heartbeat` teď vrací i cycle stats:
```json
{
  "now": "2026-04-30T10:23:45Z",
  "uptime": "0.04:18:30",
  "version": "1.0.0.0",
  "scheduler": { "running": true, "loopStarted": "...", "lastIteration": "...", "currentStationId": 234 },
  "cycle": {
    "inProgress": { "startedAt": "...", "attempts": 234, "ok": 200, "skipped": 4, "errors": 30, "samplesAdded": 38000 },
    "lastCompleted": { "startedAt": "...", "completedAt": "...", "duration": "00:04:08", "attempts": 650, "ok": 575, ... }
  },
  "status": "OK"
}
```

### 7. Serilog retention + file sizing
`appsettings.json`:
- `rollingInterval: Day` — denní rotace
- `retainedFileCountLimit: 30` — 30 dní retention (auto-mazání starších)
- `fileSizeLimitBytes: 52428800` — 50 MB max per soubor
- `rollOnFileSizeLimit: true` — pokud denní soubor přeroste 50 MB, vytvoří se další (`log-20260430_001.log`, `_002.log`, ...)
- `shared: true` — kompatibilní s vícenásobným otevřením (publish/copy bez locku)

### 8. Override per namespace
```json
"Override": {
  "Microsoft": "Warning",
  "Microsoft.AspNetCore": "Warning",
  "Microsoft.Hosting.Lifetime": "Information",
  "System.Net.Http.HttpClient": "Warning",
  "Hydra2.Downloaders.BaseDownloader": "Warning"
}
```

`BaseDownloader` na Warning znamená že i kdyby někdo nastavil global na Information, scraper logy se neukážou. Přepnout na Debug = celé `Default` level → Debug.

---

## Predikovaný výsledek

| | 2026-04-29 (původní) | Po Fázi 2 |
|---|---|---|
| Řádků / den | 1 628 730 | **~25 000** |
| MB / den | 109 | **~3 MB** |
| Cycle summary | (žádný) | **340/den** |
| Per-stanice Info | 1,32M | **0** (jen na Debug) |
| Stack traces | ~25k (per chyba) | **~600/den** (jen first per (station,type)/h) |
| Compact errors | 0 | ~24 000 (1 řádek) |

> Reálné měření po deployi na staging — teprve dokáže verifikovat, že implementace držet odhady.

---

## Příklad srovnání log řádků

### Před (12 řádků pro úspěšnou iteraci jedné stanice)
```
2026-04-29 00:00:00.6314;Info;Načten config: 259
2026-04-29 00:00:00.6471;Info;Stanice: Les Království
2026-04-29 00:00:00.6471;Debug;DownloaderType: Hydra2.DownLoaders.Chmi
2026-04-29 00:00:00.6471;Info;Stahuji ze stránky: http://hydro.chmi.cz/...
2026-04-29 00:00:00.8502;Info;Vzorků nalezeno: 192
2026-04-29 00:00:00.6158;Debug;Vzorků: -192, ukládám ...
2026-04-29 00:00:00.6158;Info;Uloženo
```

### Po (na úrovni Information): **0 řádků** — vše jde na Debug, summary až na konci cyklu
### Po (na úrovni Debug, runtime přepnuto): 4 řádky
```
2026-04-30 00:00:00.631 +02:00;DBG;Hydra2.Downloaders.UpdateService;Updating station 259 (Les Království)
2026-04-30 00:00:00.647 +02:00;DBG;Hydra2.Downloaders.Chmi;Downloading from http://hydro.chmi.cz/...
2026-04-30 00:00:00.850 +02:00;DBG;Hydra2.Downloaders.Chmi;Parsed 192 samples from http://hydro.chmi.cz/...
2026-04-30 00:00:00.616 +02:00;DBG;Hydra2.Downloaders.UpdateService;Station 259 ok, 192 samples added
```

### Cyklus dokončen
```
2026-04-30 00:04:13.121 +02:00;INF;Hydra2.Downloaders.UpdateService;Cycle complete: 650 attempts, 575 ok, 5 skipped, 70 errors, 110000 samples added, took 00:04:13
```

### Chyba (první výskyt — se stackem)
```
2026-04-30 00:00:02.928 +02:00;WRN;Hydra2.Downloaders.UpdateService;Station 263 failed: InvalidOperationException (next stack throttled for 1h)
System.InvalidOperationException: Sequence contains no matching element
   at System.Linq.Enumerable.First[TSource]...
```

### Chyba (opakovaná v rámci 1h — kompaktní)
```
2026-04-30 00:01:15.220 +02:00;WRN;Hydra2.Downloaders.UpdateService;Station 263 failed: InvalidOperationException: Sequence contains no matching element
```

---

## Workflow pro debugging ad-hoc

1. Něco se rozbilo → uživatel hlásí "graf neukazuje data za včerejšek"
2. Admin zavolá `POST /api/admin/logs/level?token=…&level=Debug` → na 30 min se loguje vše
3. Sleduje `App_Data/log-20260430.log` v reálném čase
4. Po diagnóze: `POST /api/admin/logs/level?token=…&level=Information` → zpět na produkční úroveň
5. Nebo: pokud najde konkrétní problémovou stanici → `GET /api/admin/failing-stations?token=…&topN=10`

---

## Co zůstává na pozdější fáze

| Položka | Fáze |
|---|---|
| Persistence error trackeru přes restarty (DB tabulka) | 3 |
| Polly retry policy pro transient `WebException` | 3 |
| Compression archivovaných logů (gzip starší než 7 dní) | později |
| Serilog 8 → 10 major bump | později |
| OpenTelemetry traces | 9 |
| `Hydra.Station.Active` sloupec v DB pro označení mrtvých stanic | 3 nebo dle DB cleanup |

---

## TL;DR

✅ Per-station Info logy → Debug (vypnuté v produkci, runtime přepnutelné)  
✅ Cycle summary 1 řádek per cyklus  
✅ Compact errors + throttled stack traces (1h window)  
✅ `/api/admin/logs/level` runtime level switch  
✅ `/api/admin/failing-stations` pro DB cleanup workflow  
✅ Retention 30 dní, file size limit 50 MB  
✅ Heartbeat obohacený o cycle stats  

**Predikce: 109 MB → ~3 MB / den.** Verifikace po deployi na staging.
