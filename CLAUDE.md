# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Windows desktop app for the Museo de la Sidra (inventory, sales, income, payments). C# / .NET 10, Blazor Hybrid hosted in a WPF window, MudBlazor components, SQLite via EF Core. Only the Inventario module is implemented; Sales, Ingresos and Pagos have models in the DB but their routes point to `EnConstruccion.razor`.

**Everything is in Spanish**: identifiers (`GetProductsAsync`, `factory`, `Avisos`), comments, UI text and commit-facing docs. Keep new code in Spanish to match. User-facing text is aimed at non-technical museum staff (plain wording, large buttons).

## Commands

There is no test project yet.

```bash
# Linux/WSL (development happens here): compiles everything, including the WPF project
dotnet build QuickInventory.slnx -p:EnableWindowsTargeting=true

# Windows only
dotnet build QuickInventory.slnx
dotnet run --project src/QuickInventory.Desktop
```

`QuickInventory.Desktop` targets `net10.0-windows10.0.17763.0` (WPF + WebView2). The explicit Windows version is required: the WebView2 package's WPF assembly for .NET 5+ references `Microsoft.Windows.SDK.NET`, which is only copied to the output with a versioned Windows TFM (otherwise it fails at runtime with "Could not load file or assembly 'Microsoft.Windows.SDK.NET'"). On Linux a plain `dotnet build` of the solution fails with `NETSDK1100`; the flag above lets it compile, but the app can only be run on Windows.

To run the app from WSL, build a `win-x64` exe onto the Windows drive and launch it through WSL interop (the Windows side has the .NET 10 Desktop runtime; output goes to `C:` because WebView2 misbehaves on `\\wsl.localhost` paths):

```bash
taskkill.exe /IM QuickInventory.exe /F 2>/dev/null; \
dotnet build src/QuickInventory.Desktop -r win-x64 --no-self-contained -p:EnableWindowsTargeting=true -o /mnt/c/Users/Mario/QuickInventory-dev \
  && (/mnt/c/Users/Mario/QuickInventory-dev/QuickInventory.exe &)
```

The `taskkill` is needed because a running instance locks the output files (`MSB3021: Access ... denied`); `&&` avoids launching a stale exe when the build fails.

In a Debug build, F12 inside the app window opens the WebView dev tools.

Shared build settings (nullable, implicit usings, version) live in `Directory.Build.props`.

## Architecture

Four projects; dependencies point inward:

- `QuickInventory.Core` — models (`Modelos/`), service interfaces (`Servicios/I*Service.cs`), `BusinessRuleException`. No dependencies.
- `QuickInventory.Data` — `DbContext`, service implementations (`Servicios/`), and `DataConfiguration` (DI registration via `AddData` + DB init/seed).
- `QuickInventory.UI` — Razor Class Library with all screens. Root component is `Main.razor`; theme in `Theme.cs`; layout registers the MudBlazor providers.
- `QuickInventory.Desktop` — WPF host. `App.xaml.cs` sets `es-ES` culture, builds the `ServiceProvider`, initializes the DB and exposes the provider as the `services` resource that `MainWindow.xaml`'s `BlazorWebView` consumes.

**UI must not reference Data.** Pages inject only Core interfaces (e.g. `IInventoryService`). This is deliberate: a future Blazor Web App will reuse `QuickInventory.UI` unchanged with a different provider (PostgreSQL/SQL Server) swapped in `AddData`. Don't put anything Windows-specific in UI/Core/Data.

Adding a new service: interface in Core → implementation in Data → register in `DataConfiguration.AddData`.

### Conventions that span multiple files

- **DbContext lifetime**: services take `IDbContextFactory<DbContext>` and create a short-lived context per method (`await using var db = await factory.CreateDbContextAsync()`). Don't inject `DbContext` directly — Blazor Hybrid scopes live for the whole window.
- **Business errors**: services throw `BusinessRuleException` with a message meant to be shown verbatim to the user. UI catches it and shows it in a snackbar; other exceptions get a generic message. Unhandled WPF exceptions are caught in `App.UnhandledErrorHappens`.
- **Stock is only changed through movements**: `Product.Stock` must never be edited directly. Every change goes through `AdjustStockAsync` (or future sales logic) and writes a `StockMovement` row. `SaveProductAsync` deliberately does not copy `Stock` on edit. Products with `StockControl = false` (tickets, services) have no stock.
- **Soft delete**: products are deactivated (`Active = false`), never removed, so history survives. Queries must filter on `Active`.
- **Money**: all `decimal` properties are stored as integer cents via `AmountInCentsConverter` (convention in `DbContext.ConfigureConventions`); enums are stored as strings. New `decimal`/enum properties pick this up automatically — new enums need their own `HaveConversion<string>()` line.
- **Sale lines snapshot** name and price (`SalesLine.Description`, `UnitPrice`) so later product edits don't alter history.
- UI edits a `Product.Clone()` copy in dialogs, so cancelling doesn't mutate the list.
- Search is case- and accent-insensitive via `CompareInfo.IndexOf(..., IgnoreCase | IgnoreNonSpace)` (see `Inventario.razor`); barcode scanners type into the same search box.

## Database

Single file at `%LOCALAPPDATA%\QuickInventory\museo.db`. Schema is currently created with `EnsureCreated()` in `DataConfiguration.InitDatabase`, which also seeds sample products on first run. `EnsureCreated` cannot alter an existing schema: after changing models during development, delete `museo.db` to recreate it. Before deploying to the museum, the plan (see README) is to switch to EF migrations (`dotnet ef migrations add Inicial --project src/QuickInventory.Data --startup-project src/QuickInventory.Desktop`, add an `IDesignTimeDbContextFactory`, and replace `EnsureCreated()` with `Migrate()`).
