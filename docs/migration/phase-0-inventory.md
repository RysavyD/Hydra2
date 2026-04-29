# Fáze 0 — Inventarizace stávajícího stavu

> Datum: 2026-04-29
> Větev: `net8-migration`
> Účel: Zachytit stávající stav před migrací na .NET 8, identifikovat rizika a překvapení.

---

## 1. Solution a projekty

| Projekt | Typ | Target Framework | Účel |
|---|---|---|---|
| `Hydra2.Web` | ASP.NET MVC 5 + Web API 5.2.7 | net4.6.1 | Webové UI, controllery, Quartz scheduler |
| `Hydra2.Service` | Class library | net4.6.1 | DataService (Dapper), AdminService, ConfigService, NLogger |
| `Hydra2.Downloaders` | Class library | net4.6.1 | Scrapery (CHMI/PVL/PLA/PMO), Update orchestrace |
| `Hydra2.Model` | Class library | **net4.5** | **Mrtvé** — zbytek po EF, neodkazuje na něj nikdo |
| `Hydra.DownLoad` | Console exe | net4.6.1 | Standalone tester downloaderů |
| `SampleTableSeparator` | Console exe | **netcoreapp3.1** (SDK-style) | Tools — jediný projekt už modernizovaný |

**Závislosti:** `Web → {Downloaders, Service}`, `Downloaders → Service`, `DownLoad → Downloaders`. Model je odpojený.

**Nesoulad cílových frameworků** v Web.config: `<compilation targetFramework="4.6.1">` ale `<httpRuntime targetFramework="4.5">` — neškodné, ale nečisté.

---

## 2. NuGet balíčky — co bude potřeba nahradit

### Hydra2.Web (29 balíčků)
| Současné | Verze | Stav | Náhrada v .NET 8 |
|---|---|---|---|
| Microsoft.AspNet.Mvc | 5.2.3 | EOL | `Microsoft.AspNetCore.Mvc` (built-in) |
| Microsoft.AspNet.WebApi.* | 5.2.7 | EOL | `Microsoft.AspNetCore.Mvc` (sjednocené) |
| Microsoft.AspNet.Razor | 3.2.3 | EOL | Built-in |
| Microsoft.AspNet.Web.Optimization | 1.1.3 | EOL | `WebOptimizer` (LigerShark) |
| Microsoft.AspNet.WebPages | 3.2.3 | EOL | Built-in / `Microsoft.AspNetCore.Mvc.RazorPages` |
| Quartz | **2.3.3** (2016) | velmi staré | `Quartz` 3.x + `Quartz.Extensions.Hosting` |
| NLog + NLog.Config + NLog.Schema | 3.2.0.0 (2014) | velmi staré | **Serilog** 4.x (přejít zcela) |
| Common.Logging + Common.Logging.Core + Common.Logging.NLog32 | 3.2.0 | EOL | Odstranit (řeší `Microsoft.Extensions.Logging`) |
| elmah.corelibrary + Elmah.Mvc | 1.2 / 2.1.2 | EOL | Odstranit, nahradit Serilog + ProblemDetails |
| EntityFramework | 6.1.3 | EOL pro EF6 na Core | **Odstranit** (Dapper stačí) |
| Newtonsoft.Json | **6.0.4** (2014) | dnes 13.x | `System.Text.Json` (built-in) |
| Microsoft.CodeDom.Providers.DotNetCompilerPlatform + Microsoft.Net.Compilers | 1.0.0 | legacy | Odstranit (Roslyn integrovaný v SDK) |
| WebGrease, Antlr | — | jen tranzitivní pro WebOptimization | Odstranit |
| jQuery | **1.10.2** (2013) | XSS vulns | jQuery 3.7.x (CDN) |
| bootstrap | 3.4.1 | Bootstrap 3 EOL 2019 | Ponechat 3.4.1 (zachovat UI), pouze sec-patches |
| jQuery.Validation + Microsoft.jQuery.Unobtrusive.Validation | 1.11.1 / 3.2.3 | staré | Aktualizovat na nejnovější kompatibilní |
| amcharts | 3.14.5 | nepoužito (loaded z CDN v `Graf/Index.cshtml`) | Možno odstranit z packages |
| Modernizr | 2.6.2 | 2013 | Vyhodit (moderní prohlížeče Modernizr nepotřebují) |
| Respond | 1.2.0 | pro IE9 polyfill | Vyhodit |

### Hydra2.Service
- `Dapper` 2.0.30 → 2.1+ (kompatibilní, jen update)
- `NLog` → Serilog

### Hydra2.Downloaders
- `EntityFramework` 6.1.3 — **referenced ale nepoužito**, k vyhození
- `HtmlAgilityPack` **1.4.9** (2014) → 1.11+
- `NLog` → Serilog

### Hydra2.Model
- `EntityFramework` 6.1.3 — celý projekt na vyhození (nikdo ho nepoužívá)

---

## 3. Web.config — co řešit při migraci

### Připojovací řetězce
Tři connection strings:
```
Hydra2Entities    → LAPTOP-SCPOKV5J (lokální dev EF6, EDMX)
Hydra2Connection  → LAPTOP-SCPOKV5J (lokální dev Dapper)
Hydra2Entities2   → SQL5021.Smarterasp.net (STARÉ HOSTING!)
```

⚠️ **Pozor:** žádný connection string pro Forpsi v Web.config není. Web.Release.config přepisuje na Smarterasp.net (zastaralé). **Nutno potvrdit, jak je Forpsi DB dnes konfigurováno** (přes panel jako runtime override, nebo jen .Release.config potřebuje update?).

### appSettings k migraci
| Klíč | Hodnota | Poznámka |
|---|---|---|
| `secretToken` | `myToken` | Klíč pro trigger endpointy — jde do user-secrets / env |
| `webpages:Version`, `webpages:Enabled` | — | ASP.NET WebPages, neřešit (zaniká) |
| `ClientValidationEnabled`, `UnobtrusiveJavaScriptEnabled` | true | jQuery validation — přenést do logiky |
| `elmah.mvc.*` (8×) | — | Elmah konfigurace, vše vyhodit |

### `<system.web>` / `<system.webServer>`
- HttpModules: 3× Elmah (ErrorLog, ErrorMail, ErrorFilter) — vyhodit
- `<globalization culture="en-GB" uiCulture="en-GB" />` — přenést do `Program.cs` middleware
- `<httpRuntime targetFramework="4.5" />` — zaniká
- `<handlers>` — `ExtensionlessUrlHandler` zaniká s ASP.NET Core
- `<entityFramework>` — celé vyhodit
- `<assemblyBinding>` redirects — všechny zaniknou (SDK-style csproj řeší automaticky)
- `<system.codedom>` (Roslyn provider) — zaniká

### Co zachovat v `appsettings.json`
- `secretToken` (zatím, později do env)
- Connection string (`Hydra2Connection`)

---

## 4. Quartz scheduler — KLÍČOVÉ ZJIŠTĚNÍ

📌 **Quartz není používán jako scheduler — je to jen launcher pro nekonečnou smyčku.**

`Hydra2.Web/App_Start/JobsScheduler.cs:54-57`:
```csharp
ITrigger TriggerLastUpdate = TriggerBuilder.Create()
    .WithIdentity("TriggerLastUpdate")
    .StartNow()
    .Build();   // ← žádný .WithSimpleSchedule, žádné .RepeatForever
```

Trigger se spustí **jednou při startu app poolu**, žádné opakování.

Job `UpdateLastJob` volá `Update.LastSpots()`, což je v `Hydra2.Downloaders/Update.cs:54-104`:
```csharp
public static void LastSpots()
{
    while (true)   // ← INFINITE LOOP
    {
        // čte config.Value (čítač, 0..650), inkrementuje, stahuje další stanici
        ...
    }
}
```

### Implikace
1. **Externí ping NENÍ jen budíček** — když app pool umře, celý download mechanismus přestane existovat. Ping ho znovu nahodí (UpdateLastJob při startu → smyčka).
2. **Continuous polling**: ~650 stanic v rotaci, nelze vypnout, neexistuje "off hours".
3. **Žádné time-based plánování** — interval mezi opakovaným měřením stejné stanice = doba kolem celého kola (záleží na rychlosti scrapingu, nedeterminické).
4. **Při migraci to bude nutné přepsat fundamentálně** (ne jen update Quartz verze) — chceme:
   - Per-source jobs s vlastními cron expressions (`*/15 * * * *` apod.)
   - Persistent JobStore s misfire recovery
   - Žádný `while(true)` v jobu

### Komentované joby (FirstJob…FourthJob, lines 81-122)
Stará architektura — paralelně po 200 stanicích. Pravděpodobně přerod na `LastSpots()` while-loop.

---

## 5. Logování — kořen 100 MB/den

### `Hydra2.Web/NLog.config`
```xml
<targets>
  <target name="fileAll" xsi:type="File" encoding="utf-8"
      layout="${longdate};${level};${message}"
      fileName="${basedir}/App_Data/${shortdate}.log" />
</targets>
<rules>
  <logger name="*" writeTo="fileAll" />   <!-- ŽÁDNÝ minlevel filter! -->
</rules>
```

Loguje úplně vše (Trace+).

### Hot-spotové logy (vždy `LogLevel.Info`!)

`Update.LastSpots()`:
- "Načten config: {value}" — **per stanice** (650× za otočku)
- "Stanice: {Spot}" — **per stanice**
- "Uloženo" — **per stanice**

`BaseDownloader.GetRecords()`:
- "Stahuji ze stránky: {link}" — per stanice
- "Vzorků nalezeno: {count}" — per stanice

`Update.UpdateSpots()`:
- "Index: {i}" — per index v rozsahu

➡️ **Odhad:** 5 řádků per stanice × 650 stanic × ? otoček/den. Při 1 minutě/stanici = ~2.2 otočky/den = ~7 200 řádků/den. Při 10s/stanici = ~13 otoček = ~42 000 řádků. **Časová značka + level + message ~150-300 B → 1.5-12 MB/den**. Pro 100MB tam musí být buď výrazně víc opakování, nebo dlouhé `ex.ToString()` výjimky které se opakují (timeout na zdroji?). **Bez vzorku logu si nedovolím přesněji odhadnout.**

### Žádná retention / archive / komprese / max size
Soubor `${shortdate}.log` přepisuje denně, ale staré nemaže.

---

## 6. SQL injection vektory (table-name interpolation)

⚠️ Vstup je **vždy `int.ToString("000")`**, takže prakticky bezpečné, **ale anti-pattern**:

| Soubor:řádek | Kód |
|---|---|
| `Hydra2.Service/DataService.cs:69` | `FROM [Hydra].[Sample-{spotString}]` |
| `Hydra2.Service/DataService.cs:85-86` | `FROM/INTO [Hydra].[Sample-{spotString}]` |
| `Hydra2.Service/AdminService.cs:71` | `FROM [Hydra].[Sample-{spotString}]` |
| `Hydra2.Service/AdminService.cs:86` | `FROM [Hydra].[Sample-{spotString}]` |

**Plán:** v Fázi 2 přidat whitelist (load sample table names z `Hydra.Station` při startu), použít regex `^Sample-\d{3}$` validaci.

---

## 7. Endpointy a autorizace

### Veřejné (žádná auth)
| Route | Akce | Riziko |
|---|---|---|
| `GET /` | Home/Index | Statická stránka |
| `GET /Graf/Index` | Chart UI | OK (read-only) |
| `GET /Graf/GetSpots/{id}` | Vrátí stanice | OK |
| `GET /Graf/GetData?spot=&start=&stop=&type=` | Časová řada (až `int.MaxValue` JSON) | OK, ale DoS přes velký range možný |
| `GET /Graf/ExportRaft?spot=&start=&stop=` | XML export | OK |
| **`GET /Adm/Index`** | Admin dashboard | ⚠️ **Měl by být chráněný!** |
| **`GET /Adm/SpotOverView`** | Detaily stanic | ⚠️ Měl by být chráněný |
| **`GET /Adm/HandUpdate`** | Form ručního stažení | ⚠️ POST `/Adm/HandUpdate` má AntiForgeryToken — částečně OK |
| **`GET /Adm/GetStationOverView/{id}`** | Spustí scrape! | ⚠️ Veřejně volá downloader |
| **`GET /Api/HandUpdate/{id}`** | Spustí update stanice! | ⚠️ **ŽÁDNÁ AUTH** — kdokoli může triggerovat |

### Token-protected (`?token=myToken`)
- `GET /Api/UpdateNext?token=` — spustí `LastSpots()` (= ping endpoint)
- `POST /Api/ManualData` — vloží data (params: stationId, token, records[])
- `GET /Api/GetLast?token=` — vrátí aktuální config.Value

**Token = `"myToken"`** v Web.config. **Externí ping volá `/Api/UpdateNext?token=myToken`** — to je dnešní heartbeat mechanismus.

---

## 8. Frontend assety

| Knihovna | Verze | Plán |
|---|---|---|
| jQuery | 1.10.2 | Update na 3.7+ (kompatibilní s jQuery validation 1.19+) |
| jQuery Validation | 1.11.1 | Update kompatibilně |
| Bootstrap CSS/JS | 3.4.1 | Ponechat (zachování UI) |
| Modernizr | 2.6.2 | **Vyhodit** (zbytečné v moderních prohlížečích) |
| Respond.js | 1.2.0 | **Vyhodit** (IE9 polyfill, EOL prohlížeč) |
| daterangepicker | (vendored) | Update + zachovat |
| moment.min.js | (vendored) | Update (případně nahradit Day.js — menší) |
| amCharts 4 | CDN | Funkční, **5 dostupný** — zvážit upgrade později |
| bootbox | (vendored) | Update |
| Hydra2.js | custom | Možno přepsat do TypeScript v Fázi 6 |

### Bundles (`BundleConfig.cs`)
- `~/bundles/jquery` (jQuery + bootbox + Hydra2.js)
- `~/bundles/jqueryval` (jQuery validation)
- `~/bundles/modernizr` (Modernizr)
- `~/bundles/bootstrap` (Bootstrap + Respond)
- `~/Content/css` (bootstrap.css + site.css)

V .NET 8 nahradit **WebOptimizer** nebo přejít na build pipeline (Vite/esbuild).

---

## 9. Dead code / cleanup příležitosti

| Co | Kde | Akce |
|---|---|---|
| Celý `Hydra2.Model` projekt | — | Odebrat ze solution |
| EF reference v `Hydra2.Downloaders.csproj:34-41` | — | Odebrat |
| Komentované Quartz joby | `JobsScheduler.cs:15-62` | Odebrat |
| Komentované bullety v amCharts | `Graf/Index.cshtml:227-264` | Odebrat |
| `FakeDataService` | `Hydra2.Service/FakeDataService.cs` | Ponechat pro dev / testy |
| `Hydra.DownLoad` projekt | — | Zhodnotit — používá se? Nebo přesunout do tests |
| `SampleTableSeparator` | — | Tooling — nechat |
| `Hydra2Entities`, `Hydra2Entities2` connection strings | Web.config | Odebrat (EF se vyhazuje) |
| `Web.Release.config` Smarterasp.net | — | Přepsat na Forpsi nebo zrušit (env vars) |
| `secretToken = "myToken"` | Web.config | Generovat nový, do user-secrets |

---

## 10. Existující publish profily

`Hydra2.Web/Properties/PublishProfiles/`:
- `FolderProfile.pubxml`
- `WebDeploy.pubxml`

➡️ Při migraci na ASP.NET Core nahradit za `dotnet publish` profile pro Forpsi (FTP/Web Deploy).

---

## 11. Doménový model (pro referenci)

```
River (Hydra.River)
  └─ Id, Name, RaftLink

Station (Hydra.Station)        ← 1:N s River
  └─ Id, Spot, Spa_val, Spa0, Spa1, Spa2, Spa3, Spa3e,
     Type, Link, Id_River, DownLoadType (1=CHMI, 2=Pvl, 3=PvlNadrze, 4=PlaNadrze, 5=PmoNadrze, 6=PmoToky)

Sample-{NNN} (Hydra.Sample-001 .. Sample-650)   ← 1 tabulka per stanice
  └─ TimeStamp, Level, Flow, Temperature

Config (Hydra.Config)
  └─ Id, Key, Value   ← drží counter aktuálně zpracovávané stanice (0..650)
```

---

## 12. Otevřené body pro Fázi 1

Před spuštěním migrace na .NET 8 potřebuji potvrdit / získat:

| # | Co | Proč | Kdo |
|---|---|---|---|
| 1 | **Forpsi connection string** k Hydra2 DB | Nahradí Smarterasp.net v `appsettings.Production.json` (nebo env var) | Uživatel |
| 2 | Zda `Hydra.DownLoad` (console exe) běží na Forpsi nebo jen lokálně | Rozhodne, jestli ho migrovat nebo zrušit | Uživatel |
| 3 | Vzor produkčního logu (≥ 10 MB) | Identifikovat skutečný viník 100 MB/den | Uživatel — slíbeno |
| 4 | Zda externí ping má jen 1 endpoint nebo víc | Zachovat funkčnost migrace | Uživatel |
| 5 | URL staging subdomény | Cíl pro deploy testů | Uživatel — připraveno |

---

## Souhrn — TOP 7 zjištění

1. **🔴 Quartz je fake-scheduler** — `StartNow()` jednou + `while(true)` v jobu. Migrace musí přepsat celý update mechanismus, ne jen bumpnout knihovnu.
2. **🔴 NLog má `*` rule bez minlevel** — loguje úplně vše. Ihned snížit level vyřeší 80 % problému 100 MB/den.
3. **🔴 `secretToken = "myToken"`** v gitu + používá se pro autorizaci ping endpointu. Při migraci přegenerovat a do env.
4. **🟡 `/Adm/*` a `/Api/HandUpdate` bez autorizace** — nutno přidat alespoň token check, ideálně skutečnou auth.
5. **🟡 SQLi anti-pattern** v 4 místech — vstup je sanitizovaný (`int.ToString("000")`), ale styl je špatný a fragile.
6. **🟢 Dapper migrace už proběhla** — Service vrstva je čistá, jen async/await chybí.
7. **🟢 Existující trigger endpoint** (`/Api/UpdateNext`) je solidní základ pro budoucí smart heartbeat — stačí rozšířit o status reporting.

---

## Doporučený první krok Fáze 1

Začít vytvořením **paralelního stromu projektů** v `src/` (SDK-style, .NET 8) — neporušit současný build, mít možnost srovnat side-by-side. Sekvence:

1. `src/Hydra2.Service` — port (nejmenší změny, čistý Dapper)
2. `src/Hydra2.Downloaders` — port (nahradit `WebClient` za `HttpClient`, async)
3. `src/Hydra2.Web` — port MVC controllerů + Razor views, `Program.cs`, `appsettings.json`
4. `src/Hydra2.AppHost` (volitelně) — pokud chceme oddělit Quartz Worker

Stávající `Hydra2.Web` neporušit dokud `src/` build neprojde a smoke test na localhost neběží.
