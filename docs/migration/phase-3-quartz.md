# Fáze 3 — Quartz refactor + smart heartbeat

> Datum: 2026-04-30
> Větev: `net8-migration`
> Build: ✅ `dotnet build Hydra2.net8.sln` — 0 warning, 0 error

---

## Cíl

Nahradit fragilní pattern „jednorázový Quartz job → `while(true)` smyčka v `LastSpotsLoopAsync`" za **opravdový cron-based scheduler**:
- Per-zdroj jobs s vlastními intervaly v `appsettings.json`
- Misfire handling pro graceful pickup po probuzení app poolu
- Polly retry pro transient HTTP errory
- Heartbeat s per-zdroj diagnostikou + HTTP 503 při STALE
- Trigger endpointy pro externí spuštění konkrétního zdroje

---

## Architektonické změny

### Před (Fáze 1+2)
```
Quartz: 1 trigger StartNow() → UpdateLastJob → LastSpotsLoopAsync()
        → while(!ct) { UpdateNextSpotAsync() → cyklus 0..650 → wraparound = nový cycle }
```

### Po (Fáze 3)
```
Quartz: 6 cron triggerů → 6× SourceUpdateJob (per DownLoadType)
        → UpdateSourceAsync(downLoadType)
        → smyčka přes Stations.Where(DownLoadType=N) → každá s retry policy
        → SourceStateTracker zaznamená výsledek
```

Žádný `while(true)`, žádná Config.Value smyčka, žádný wraparound. Každý zdroj má vlastní cron a vlastní stav.

---

## Konkrétní změny

### 1. Nový SourceCatalog ([src/Hydra2.Downloaders/SourceCatalog.cs](../../src/Hydra2.Downloaders/SourceCatalog.cs))
Stabilní mapování `DownLoadType ↔ name`:

| Name | DownLoadType | DisplayName |
|---|---|---|
| `chmi` | 1 | ČHMÚ |
| `pvl` | 2 | PVL |
| `pvlNadrze` | 3 | PVL nádrže |
| `plaNadrze` | 4 | PLA nádrže |
| `pmoNadrze` | 5 | PMO nádrže |
| `pmoToky` | 6 | PMO toky |

(POH a PLA řeky používají PVL/PvlNadrze downloader — žádná separátní položka.)

### 2. Per-zdroj cron config v `appsettings.json`
```json
"Scheduler": {
  "Sources": {
    "chmi":      { "Enabled": true, "DownLoadType": 1, "Cron": "0 0/15 * * * ?",  "Misfire": "FireAndProceed" },
    "pvl":       { "Enabled": true, "DownLoadType": 2, "Cron": "0 1/15 * * * ?",  "Misfire": "FireAndProceed" },
    "pvlNadrze": { "Enabled": true, "DownLoadType": 3, "Cron": "0 2/30 * * * ?",  "Misfire": "FireAndProceed" },
    "plaNadrze": { "Enabled": true, "DownLoadType": 4, "Cron": "0 3/30 * * * ?",  "Misfire": "FireAndProceed" },
    "pmoNadrze": { "Enabled": true, "DownLoadType": 5, "Cron": "0 4/30 * * * ?",  "Misfire": "FireAndProceed" },
    "pmoToky":   { "Enabled": true, "DownLoadType": 6, "Cron": "0 5/30 * * * ?",  "Misfire": "FireAndProceed" }
  }
}
```

**Výchozí intervaly**:
- `chmi`, `pvl` — každých 15 min (nejvíc stanic, hlavní zdroje)
- ostatní `*Nadrze`, `pmoToky` — každých 30 min (méně stanic, mírnější změny)
- Posun startovní minuty (`/0`, `/1`, `/2`, ...) rozprostře zátěž

**Pozor na Quartz cron**: 7 polí (sec min hr day-of-month month day-of-week [year]). `0 0/15 * * * ?` = každých 15 min (v minutách 0, 15, 30, 45). `?` v day-of-week protože day-of-month je `*`.

### 3. Misfire handling
Když app pool spí (např. 10 minut) a probudí se, Quartz vidí, že trigger měl být spuštěn už dříve.
- `FireAndProceed` — spustí JEDNOU teď a pokračuje normálním rozvrhem (default v naší konfiguraci)
- `Ignore` — přeskočí zmeškaný fire, čeká na další naplánovaný

V `appsettings.json` per zdroj v poli `Misfire`. Default je `FireAndProceed`.

### 4. Polly retry policy ([ServiceCollectionExtensions.cs](../../src/Hydra2.Downloaders/ServiceCollectionExtensions.cs))
Každý `HttpClient` (1 per scraper) má resilience pipeline:
```csharp
.AddResilienceHandler("scraper", pipeline =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 2,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
    });
});
```

→ Při transient chybě (5xx, 408, 429, network errors): **2× retry s exponenciálním backoff** (2s, 4s + jitter). Default `Microsoft.Extensions.Http.Resilience` strategie zachytí typické transient chyby (`HttpRequestException`, response 5xx). 

Predikce: 9 075 "connection closed" + 3 210 "500" chyb → výrazná redukce po retry, většina těchto je transient.

### 5. Per-zdroj state tracker ([SourceStateTracker.cs](../../src/Hydra2.Downloaders/SourceStateTracker.cs))
Drží per-zdroj:
- `LastRunStartedAt`, `LastRunCompletedAt`, `LastDuration`
- `LastSuccessAt`, `LastErrorAt`, `LastErrorMessage`
- `LastStationCount`, `LastOk`, `LastErrors`, `LastSamplesAdded`
- `TotalRuns`, `TotalSuccessRuns`
- `Running` flag

Použito v `HeartbeatController` a `JobsController`.

### 6. Refactor `UpdateService`
- ❌ `LastSpotsLoopAsync` (smyčka) — odstraněno
- ❌ `UpdateNextSpotAsync` (per-iteration) — odstraněno z interface (zůstává funkčnost přes `UpdateStationAsync(stationId)` v `/Api/UpdateNext` pro backward compat)
- ✅ `UpdateSourceAsync(downLoadType, ct)` — nový hlavní entry point
- ✅ `UpdateStationAsync(stationId, ct)` — single station
- ✅ `UpdateSpotsAsync(start, stop, ct)` — range (ponecháno pro `/Adm/HandUpdate`)
- Vnitřně: privátní `UpdateStationCoreAsync(Station, ct)` se sdílenou logikou (zamezí duplikaci kódu mezi metodami)

### 7. Heartbeat enriched + HTTP 503 ([HeartbeatController.cs](../../src/Hydra2.Web/Controllers/HeartbeatController.cs))
**Status logika**:
- `OK` — DB OK + žádný zdroj nemá `LastSuccessAt > 60 min nazpět`
- `STALE` — DB OK, ale alespoň 1 zdroj má poslední úspěch > 60 min
- `STARTING` — žádný zdroj zatím neběžel
- `DEGRADED` — DB nedostupná

**HTTP status code**:
- `OK` / `STARTING` → 200
- `STALE` / `DEGRADED` → **503 Service Unavailable** (cron-job.org se rozzvoní)

**Příklad odpovědi**:
```json
{
  "now": "2026-04-30T14:32:11Z",
  "uptime": "0.04:18:30",
  "version": "1.0.0.0",
  "db": "OK",
  "sources": [
    {
      "name": "chmi",
      "downLoadType": 1,
      "running": false,
      "lastRunStartedAt": "2026-04-30T14:30:00Z",
      "lastRunCompletedAt": "2026-04-30T14:30:23Z",
      "lastSuccessAt": "2026-04-30T14:30:23Z",
      "lastErrorAt": null,
      "lastErrorMessage": null,
      "lastDuration": "00:00:23",
      "stationsTotal": 145,
      "stationsOk": 142,
      "stationsErrors": 3,
      "samplesAdded": 145,
      "totalRuns": 18,
      "totalSuccessRuns": 17,
      "minutesSinceSuccess": 1.8,
      "nextScheduledAt": "2026-04-30T14:45:00Z"
    },
    ...
  ],
  "status": "OK"
}
```

### 8. Trigger endpointy ([JobsController.cs](../../src/Hydra2.Web/Controllers/JobsController.cs))
| Endpoint | Účel |
|---|---|
| `POST /api/jobs/trigger/{name}?token=` | Synchronní spuštění zdroje, čeká na dokončení (in-process) |
| `POST /api/jobs/schedule/{name}?token=` | Asynchronní fire-now přes Quartz (vrátí ihned, job běží na pozadí) |
| `GET /api/jobs/status?token=` | Stav všech triggerů (`nextFireUtc`, `state`, `cron`) |

`{name}` = `chmi` / `pvl` / `pvlNadrze` / `plaNadrze` / `pmoNadrze` / `pmoToky` (case insensitive).

Příklady:
```bash
# Manuálně spustit ČHMÚ run a počkat na výsledek
POST /api/jobs/trigger/chmi?token=<secret>

# Naplánovat ČHMÚ run okamžitě (fire-and-forget)
POST /api/jobs/schedule/chmi?token=<secret>

# Zobrazit stav všech zdrojů
GET /api/jobs/status?token=<secret>
```

### 9. AdoJobStore — záměrně NE
Quartz 3 podporuje persistenci schedule v SQL (`AdoJobStore`), ale:
- Naše schedule je v `appsettings.json`, ne v DB → persistence schedule nepřináší value
- Vyžaduje vytvoření Quartz tabulek v DB (manuální SQL)
- Sdílený hosting může mít issues s šíří DB schématu
- Současné `RAMJobStore` + `FireAndProceed` misfire policy řeší většinu praktických scénářů

→ Zůstáváme u **`RAMJobStore` (default)**. Pokud se ukáže potřeba persistovat misfire historii nebo cluster, přidáme `AdoJobStore` v separátní fázi.

### 10. Backward compat
Endpointy zachované pro plynulou migraci:
- ✅ `/Api/UpdateNext?token=…` — Funkční, ale `[Obsolete]`. Nyní volá `UpdateStationAsync(currentConfigValue)` místo původního `UpdateNextSpotAsync` loopu. Po cutoveru cron-job.org → `/api/heartbeat` lze odstranit.
- ✅ `/Api/HandUpdate/{id}?token=…` — Funkční (token check), volá `UpdateSpotsAsync(id, id)`
- ✅ `/Api/ManualData` — Funkční (token check)
- ✅ `/Api/GetLast?token=…` — Funkční (vrací hodnotu z `Hydra.Config`)
- ✅ `/Adm/HandUpdate` POST — Funkční (volá `UpdateSpotsAsync`)

---

## Změna chování — co je třeba vědět

### Frekvence stahování
**Bylo**: ~340 cyklů přes 650 stanic za den ⇒ každá stanice scrapnuta cca **každých 4 minuty**.
**Teď**: dle cron config:
- chmi/pvl: každých **15 min** (96× / den / stanice)
- ostatní: každých **30 min** (48× / den / stanice)

Důvod: scraper vrací 192 historických hourly samplů a `INSERT IF NOT EXISTS` deduplikuje. Stahovat každé 4 min = ~99% calls přidávalo 0 řádků. **Je to úsporná změna**, ne ztráta funkcionality.

Pokud uživateli vadí (čeká data dříve), stačí změnit `Cron` v `appsettings.json` (např. `0 0/5 * * * ?` = každých 5 min). Žádný redeploy kódu.

### Po app pool restartu
- Quartz se inicializuje → cron triggery se zaregistrují
- Pokud měl některý trigger fire mezi pádem a startem → `FireAndProceed` policy → spustí se **jednou hned po startu**, pak normálně dle cronu
- Žádné dohánění zmeškaných fire ze sleep period (pouze 1× catch-up per zdroj)

### Co se nestane
- ❌ `Hydra.Config.Value` se už neinkrementuje (zůstává v DB pro backward compat `/Api/GetLast` a `/Api/UpdateNext`)
- ❌ Žádný cycle summary log (nahrazeno per-source `Source X run complete: ...` po každém běhu)

### Log volume po Fázi 3
Per-source job běh:
```
INF Source chmi run started (145 stations)
WRN Station 263 failed: InvalidOperationException (next stack throttled for 1h)
... + případné stacky první za hodinu ...
INF Source chmi run complete: outcome=PartialFailure, 142 ok, 3 errors, 145 samples added
```

Při 6 zdrojích × ~70 běhů/den (kombinace 15min a 30min) = ~420 source-run summary logů/den.
+ Errors (cca 2-3% stanic per run) = ~600-2000 řádků/den (compact + první stack/hodinu).

**Predikce: ~2-3 MB/den** (ještě méně než predikované 3 MB ve Fázi 2 — Polly snižuje transient errory).

---

## Příklad kompletního cyklu (Quartz trigger → log → state)

```
2026-04-30 14:30:00.012 +02:00  Quartz: trigger "trigger-chmi" fired
2026-04-30 14:30:00.025 +02:00  INF  Source chmi run started (145 stations)
2026-04-30 14:30:00.500 +02:00  DBG  Updating station 1 (Mladotice)        ← jen na Debug
...
2026-04-30 14:30:18.420 +02:00  WRN  Station 263 failed: InvalidOperationException (next stack throttled for 1h)
                                     System.InvalidOperationException: Sequence...
2026-04-30 14:30:23.812 +02:00  INF  Source chmi run complete: outcome=PartialFailure, 142 ok, 3 errors, 145 samples added
                                     ↳ SourceStateTracker: lastRunCompletedAt updated
                                     ↳ Quartz: next fire 14:45:00
```

Heartbeat call between runs:
```bash
curl https://hydra2.dusanrysavy.cz/api/heartbeat
# 200 OK, status:"OK", chmi.minutesSinceSuccess: 5.3
```

---

## TL;DR

✅ 6 per-zdroj cron jobů (chmi, pvl, pvlNadrze, plaNadrze, pmoNadrze, pmoToky)
✅ Cron expressions konfigurovatelné v `appsettings.json` bez redeployu
✅ Misfire policy `FireAndProceed` (graceful pickup po probuzení)
✅ Polly retry — 2× s exp backoff pro transient HTTP errory
✅ Per-zdroj state tracker (lastSuccess, errors, duration, totalRuns)
✅ Heartbeat vrací **503 při STALE** (cron-job.org alert)
✅ Trigger endpointy `POST /api/jobs/trigger/{name}` a `/schedule/{name}`
✅ Žádný `while(true)`, žádný wraparound, žádný `Hydra.Config` counter loop

**Po cutoveru produkce**:
1. Cron-job.org pinguje `/api/heartbeat` (5-10 min interval)
2. Quartz si vlastní per-source schedule (uvnitř app poolu)
3. Při výpadku zdroje (web nedostupný) → status STALE → 503 → cron-job.org pošle alert
4. Manuální zásah přes `/api/jobs/trigger/chmi?token=…` nebo přes `/Adm/HandUpdate`
