# Fáze 13 — Upgrade na .NET 10 (LTS)

> Datum: 2026-10-05
> Větev: `net10-migration` (z `net8-migration`)
> Build status: ✅ `dotnet test Hydra2.net8.sln` — 82/82, 0 warning, 0 error

---

## Proč
- **.NET 8 končí podpora 10. 11. 2026.** .NET 9 (STS) končí ve stejný den, nemá smysl jako mezikrok.
- **.NET 10 je LTS** s podporou do listopadu 2028.
- Forpsi hosting .NET 10 podporuje (ASP.NET Core 10 Hosting Bundle) → framework-dependent deploy jako dosud.

## Co bylo uděláno

### 1. Target framework
`src/Directory.Build.props` a `tests/Directory.Build.props`: `net8.0` → `net10.0`.

### 2. NuGet balíčky

| Balíček | Před | Po |
|---|---|---|
| `Microsoft.Extensions.*` (Hosting, Http, Logging, Options, Configuration, DI) | 8.0.x | 10.0.12 |
| `Microsoft.Extensions.Http.Resilience` | 8.10.0 | 10.10.0 |
| `Microsoft.Extensions.TimeProvider.Testing` | 8.10.0 | 10.10.0 |
| `Microsoft.AspNetCore.Mvc.Testing` | 8.0.10 | 10.0.12 |
| `Serilog.AspNetCore` | 8.0.3 | 10.0.0 |
| `Serilog.Sinks.File` | 6.0.0 | 7.0.0 (vyžaduje Serilog.AspNetCore 10) |
| `Microsoft.Data.SqlClient` | 5.2.2 | 6.1.4 |

Beze změny: Quartz 3.18.1, HtmlAgilityPack, Dapper, LigerShark.WebOptimizer, xUnit/NSubstitute/FluentAssertions.

### 3. Kód
Žádné změny v kódu nebyly potřeba.

## Poznámky / gotchas
- **`Mvc.Testing` musí odpovídat verzi runtime.** S 8.0.10 na net10 padalo 11 integračních testů
  (`PipeWriter 'ResponseBodyPipeWriter' does not implement PipeWriter.UnflushedBytes` —
  TestHost 8 neimplementuje API, které System.Text.Json v .NET 10 používá). Produkční aplikace tím postižena nebyla.
- **SqlClient záměrně jen na 6.1**, ne 7.x — major verze 7 je samostatný krok s vlastním ověřením.
  6.x je kompatibilní s connection stringem (`Encrypt=True;TrustServerCertificate=True`).
- Název `Hydra2.net8.sln` ponechán — přejmenování na `Hydra2.sln` patří do Fáze 12 (cleanup).

## Ověření na stagingu (manuálně)
1. Ve Forpsi administraci přepnout staging na **.NET 10**
2. `dotnet publish src/Hydra2.Web -c Release -o ./publish` → FTP upload
3. Zkontrolovat:
   - `/api/heartbeat` → 200, DB healthy
   - Quartz joby běží (`/api/jobs/status`), stahování ze všech zdrojů zapisuje vzorky
   - Grafy a seznam stanic (připojení k SQL Serveru přes SqlClient 6.1)
   - Serilog zapisuje do souboru (Sinks.File 7)
4. Po 2–3 dnech bez regresí → produkce
