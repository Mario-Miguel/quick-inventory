# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Windows desktop app for the Museo de la Sidra (inventory, sales, income, payments). C# / .NET 10, Blazor Hybrid hosted in a WPF window, MudBlazor components, PostgreSQL via EF Core (one database shared by every computer; in the cloud for the museum, a local Docker container in development). VERI\*FACTU integration is planned in `docs/verifactu.md`. Only the Inventario module is implemented; Sales, Ingresos and Pagos have models in the DB but their routes point to `EnConstruccion.razor`.

**Everything is in Spanish**: identifiers (`GetProductsAsync`, `factory`, `Avisos`), comments, UI text and commit-facing docs. Keep new code in Spanish to match. User-facing text is aimed at non-technical museum staff (plain wording, large buttons).

## Commands

```bash
dotnet build QuickInventory.slnx                 # also on Linux/WSL, including the WPF project
dotnet run --project src/QuickInventory.Desktop  # Windows only

# Tests (xUnit, tests/)
dotnet test tests/QuickInventory.Core.Tests
dotnet test tests/QuickInventory.Verifactu.Tests   # the real AEAT test is skipped unless VERIFACTU_PRUEBAS_* are set

# Migrations (dotnet-ef is a local tool: run `dotnet tool restore` once).
# Data is its own startup project (Desktop can't be used from Linux); see DesignTimeDbContextFactory.
dotnet ef migrations add <Nombre> --project src/QuickInventory.Data --startup-project src/QuickInventory.Data --output-dir Migrations
dotnet ef database update --project src/QuickInventory.Data --startup-project src/QuickInventory.Data
```

`QuickInventory.Desktop` targets `net10.0-windows10.0.17763.0` (WPF + WebView2). The explicit Windows version is required: the WebView2 package's WPF assembly for .NET 5+ references `Microsoft.Windows.SDK.NET`, which is only copied to the output with a versioned Windows TFM (otherwise it fails at runtime with "Could not load file or assembly 'Microsoft.Windows.SDK.NET'"). The csproj sets `EnableWindowsTargeting=true` so the solution compiles on Linux (without it, it fails with `NETSDK1100` and so does VS Code's Testing tab, which builds the whole solution); the app can only be run on Windows.

To run the app from WSL, build a `win-x64` exe onto the Windows drive and launch it through WSL interop (the Windows side has the .NET 10 Desktop runtime; output goes to `C:` because WebView2 misbehaves on `\\wsl.localhost` paths):

```bash
taskkill.exe /IM QuickInventory.exe /F 2>/dev/null; \
dotnet build src/QuickInventory.Desktop -r win-x64 --no-self-contained -p:EnableWindowsTargeting=true -o /mnt/c/Users/Mario/QuickInventory-dev \
  && (/mnt/c/Users/Mario/QuickInventory-dev/QuickInventory.exe &)
```

The `taskkill` is needed because a running instance locks the output files (`MSB3021: Access ... denied`); `&&` avoids launching a stale exe when the build fails.

Per-client executable (see README, "Clients"): `dotnet publish src/QuickInventory.Desktop -c Release -r win-x64 --self-contained -p:Client=<id> -o publish/<id>`. Without `-p:Client` it builds `museo-sidra`. In Debug all clients are embedded and env var `QUICKINVENTORY_CLIENT=<id>` picks one at startup (set by the client picker in `.vscode/launch.json`; ignored in Release).

In a Debug build, F12 inside the app window opens the WebView dev tools.

Shared build settings (nullable, implicit usings, version) live in `Directory.Build.props`.

## Architecture

Five projects; dependencies point inward:

- `QuickInventory.Core` — models (`Modelos/`), service interfaces (`Servicios/I*Service.cs`), `BusinessRuleException`. No dependencies.
- `QuickInventory.Data` — `DbContext`, service implementations (`Servicios/`), and `DataConfiguration` (DI registration via `AddData` + DB init/seed).
- `QuickInventory.Verifactu` — standalone VERI\*FACTU library (no project references): builds `RegistroAlta`/`RegistroAnulacion` XML with the chained SHA-256 fingerprint, validates against the AEAT XSDs embedded in `Esquemas/`, builds the QR URL, and sends/queries via SOAP (`AeatClient`). It has its own exceptions (`VerifactuException`…); Data will translate them to `BusinessRuleException` (phase 2). All values that go into the fingerprint must be formatted with `VerifactuFormat`. The AEAT source documents are in the git-ignored `.internalDocs/Verifactu/`.
- `QuickInventory.UI` — Razor Class Library with all screens. Root component is `Main.razor`; theme in `Theme.cs`; layout registers the MudBlazor providers.
- `QuickInventory.Desktop` — WPF host. `App.xaml.cs` sets `es-ES` culture, loads the configuration (connection string), builds the `ServiceProvider`, initializes the DB and exposes the provider as the `services` resource that `MainWindow.xaml`'s `BlazorWebView` consumes.

**UI must not reference Data.** Pages inject only Core interfaces (e.g. `IInventoryService`). This is deliberate: a future Blazor Web App will reuse `QuickInventory.UI` unchanged with a different provider (PostgreSQL/SQL Server) swapped in `AddData`. Don't put anything Windows-specific in UI/Core/Data.

Adding a new service: interface in Core → implementation in Data → register in `DataConfiguration.AddData`.

### Conventions that span multiple files

- **DbContext lifetime**: services take `IDbContextFactory<DbContext>` and create a short-lived context per method (`await using var db = await factory.CreateDbContextAsync()`). Don't inject `DbContext` directly — Blazor Hybrid scopes live for the whole window.
- **Business errors**: services throw `BusinessRuleException` with a message meant to be shown verbatim to the user. UI catches it and shows it in a snackbar; other exceptions get a generic message. Unhandled WPF exceptions are caught in `App.UnhandledErrorHappens`.
- **Stock is only changed through movements**: `Product.Stock` must never be edited directly. Every change goes through `AdjustStockAsync` (or future sales logic) and writes a `StockMovement` row. `SaveProductAsync` deliberately does not copy `Stock` on edit. Only `ShopProduct` has stock; `TicketProduct` has none.
- **Soft delete**: products are deactivated (`Active = false`), never removed, so history survives. Queries must filter on `Active`.
- **Money**: all `decimal` properties are stored as integer cents via `AmountInCentsConverter` (convention in `DbContext.ConfigureConventions`); enums are stored as strings. New `decimal`/enum properties pick this up automatically — new enums need their own `HaveConversion<string>()` line.
- **Sale lines snapshot** name and price (`SalesLine.Description`, `UnitPrice`) so later product edits don't alter history.
- **Product types**: `Product` is abstract; `TicketProduct` (Taquilla) and `ShopProduct` (Tienda) share the `Products` table (TPH, discriminator `Type`). The type is chosen when creating and can't change afterwards (`SaveProductAsync` rejects it). `ProductDialog` swaps the instance (`Product.New(category)`) when the type changes, so callers must save the product returned in `DialogResult.Data`, not the one they passed in.
- UI edits a `Product.Clone()` copy in dialogs, so cancelling doesn't mutate the list.
- **Online tickets (PrestaShop)**: `PrestaShopTicketService` reads orders read-only through the PrestaShop Webservice (`/api/orders`, JSON, `ws_key`). Nothing is stored locally except `OnlineShopSettings` (configured from Admin → "Tienda online"). Visit day/time and ticket type are parsed from each order line's `product_name` ("Entrada - Fecha: 25/10/2026, Hora: 11:00") by `PrestaShopProductName`; if the shop stores the date elsewhere (customization fields, a booking module), that's the piece to change. With the shop URL set to `demo` (the default seeded on first run), `PrestaShopDemoHandler` fakes the Webservice with deterministic sample orders, so the whole pipeline runs without a real shop.
- **Per-client customization**: `clientes/<id>/cliente.json` (name, colors, optional modules, optional fiscal data) is embedded in the Desktop exe at build time (`-p:Client=<id>`, `EmbeddedResource` with `LogicalName="cliente.json"`), read by `ClientSettingsLoader` and registered as a Core `ClientSettings` singleton. UI injects it: `Theme.For(colors)` in `MainLayout`, `Module.Requires` hides modules whose `ClientFeature` is off (`Modules.For`/`Allows`), and `FirstRunWizard` prefills `ClientSettings.Issuer`. Never use the client name or colors as literals; never put secrets in `cliente.json` (the connection goes in the git-ignored `clientes/<id>/appsettings.json`, copied next to the exe). A new optional module = a `ClientFeatures` property + `ClientFeature` value + `Requires` on its `Module`s.
- **Releases and updates** (`docs/publicacion.md`): tag `<cliente>/v<version>` → `.github/workflows/publish.yml` builds that client's exe, packs it with Velopack (`vpk`, local tool, same version as the `Velopack` package), applies migrations with the client environment's `DB_CONNECTION` secret and uploads with `GITHUB_TOKEN` to a fixed release per client in this (public) repo, tag `updates-<client>` (not `<client>`: it would clash with the `<client>/v*` tags). The feed URL is baked in via `-p:UpdateUrl` (AssemblyMetadata read by `VelopackUpdater`, which implements UI's `IAppUpdater`). `Program.Main` must call `VelopackApp.Build().Run()` first. Packages are public: never ship `appsettings.json` (the workflow fails if it's there). Migrations run before clients update, so they must stay compatible with the previous version.
- Search is case- and accent-insensitive via `CompareInfo.IndexOf(..., IgnoreCase | IgnoreNonSpace)` (see `Inventario.razor`); barcode scanners type into the same search box.

## Database

PostgreSQL, shared by every computer running the app. The schema diagram and storage conventions are in `docs/base-de-datos.md`; update it whenever a model or `BaseDbContext` changes.

- **Connection string** `ConnectionStrings:Museo`, read in layers by `App.LoadConfiguration`: `appsettings.json` next to the exe (optional, git-ignored: it holds the password) → `%LOCALAPPDATA%\QuickInventory\appsettings.json` (per computer; the usual place, also in development) → env var `QUICKINVENTORY_ConnectionStrings__Museo` (also used by `dotnet ef`).
- **Schema comes from EF migrations** (`src/QuickInventory.Data/Migrations`). After changing a model, add a migration (command above); never edit an applied one. `DataConfiguration.InitDatabase` applies pending migrations and seeds sample products only in **Debug**; in Release it only checks that the DB and the app are on the same migration and refuses to start otherwise (migrations are applied from development with the schema owner; the app user from `deploy/postgres/usuario-app.sql` can't alter the schema). The database is never dropped or recreated: it will hold invoice records that must be kept for years.
- **Dates** are local museum time (`DateTime.Now`) stored as `timestamp without time zone` (convention in `ConfigureConventions`). Without it Npgsql would require UTC `DateTime`s.
- **Connection loss locks the app**: `ConnectionMonitor` (Data, exposed as Core's `IConnectionStatus`) polls the DB and is also notified by `ConnectionFailureInterceptor` when an EF operation fails for connection reasons; `MainLayout` covers everything with an overlay while disconnected. There is no offline mode by design (see `docs/verifactu.md`, 2.1).
- **First run**: while there is no `Issuer` row, `MainLayout` shows `FirstRunWizard` instead of the page. Admin can edit it later in `/issuer`.
