# Museo de la Sidra — Gestión

Aplicación de escritorio para Windows que lleva el inventario, las ventas, los ingresos y los pagos del museo.

Está hecha con **C# / .NET 10**, **Blazor Hybrid** dentro de una ventana **WPF**, **MudBlazor** para los componentes visuales y **SQLite** con **Entity Framework Core** para los datos.

## Qué hay hecho

- **Pantalla de inicio** con cuatro botones grandes: Sales, Inventario, Ingresos y Pagos.
- **Inventario** completo:
  - búsqueda instantánea que no distingue mayúsculas ni tildes (también sirve el lector de códigos de barras);
  - filtro de "stock bajo";
  - alta y edición de productos;
  - entradas y salidas de unidades con description, que quedan registradas en un historial;
  - baja de productos sin perder su historial.
- **Products sin stock** (entradas, visitas guiadas...): se marcan con "Llevar control de unidades" desactivado.
- **Modelos de Sales, Ingresos y Pagos** ya creados en la base de datos; sus pantallas muestran "en construcción".
- Products de ejemplo la primera vez que se abre (puedes darlos de baja).

## Requisitos para programar

- Windows 10 u 11.
- **Visual Studio 2026** (o Visual Studio 2022 17.14 o posterior) con estas cargas de trabajo:
  - _Desarrollo de escritorio de .NET_
  - _Desarrollo de ASP.NET y web_
- SDK de **.NET 10** (se instala con Visual Studio).
- **WebView2 Runtime**: ya viene con Windows 10/11 actualizados.

## Cómo arrancarlo

1. Abre `QuickInventory.slnx` con Visual Studio.
2. Clic derecho en **QuickInventory.Desktop** -> _Establecer como proyecto de inicio_.
3. Pulsa **F5**. La primera vez descargará los paquetes NuGet.

Desde la terminal también funciona:

```powershell
dotnet run --project src/QuickInventory.Desktop
```

> Si tu versión de Visual Studio no abre archivos `.slnx`, crea un `.sln` clásico:
>
> ```powershell
> dotnet new sln -n QuickInventory
> dotnet sln add (Get-ChildItem -Recurse src/*.csproj)
> ```

Con la app en modo Debug puedes pulsar **F12** dentro de la ventana para abrir las herramientas de desarrollo (como en el navegador).

## Estructura

```
QuickInventory.slnx
src/
├── QuickInventory.Core      Modelos y contratos de servicio. Sin dependencias.
├── QuickInventory.Data      EF Core + SQLite e implementación de los servicios.
├── QuickInventory.UI        Pantallas Blazor (Razor Class Library).
└── QuickInventory.Desktop   Ventana WPF que aloja las pantallas.
```

Las dependencias van siempre hacia dentro: `Desktop → UI → Core` y `Desktop → Data → Core`.
**UI no conoce Data**: las pantallas solo hablan con interfaces como `IInventoryService`. Gracias a eso, la futura web podrá usar las mismas pantallas con otra implementación de los servicios.

## Dónde se guardan los datos

En un único archivo: `%LOCALAPPDATA%\QuickInventory\museo.db`

Para hacer una copia de seguridad basta con copiar ese archivo con la app cerrada. Para empezar de cero, bórralo.

Los importes se guardan en **céntimos enteros** para evitar errores de redondeo. Lo hace un conversor en `DbContext` y es transparente para el resto del código, que trabaja con `decimal`.

## Antes de instalarlo en el museo: pasar a migraciones

Ahora mismo la base de datos se crea con `EnsureCreated()`, que es cómodo para empezar pero **no puede modificar el esquema** de una base que ya existe. Cuando el model esté más estable:

```powershell
dotnet tool install --global dotnet-ef
dotnet add src/QuickInventory.Data package Microsoft.EntityFrameworkCore.Design
dotnet ef migrations add Inicial --project src/QuickInventory.Data --startup-project src/QuickInventory.Desktop
```

Después, en `DataConfiguration.InitDatabase`, cambia `db.Database.EnsureCreated()` por `db.Database.Migrate()`.

Para que `dotnet ef` pueda crear el contexto en tiempo de diseño, añade en `QuickInventory.Data` una clase que implemente `IDesignTimeDbContextFactory<DbContext>`.

## Siguientes pasos sugeridos

1. **Sales (TPV)**: rejilla de botones con los productos más vendidos, carrito, cobro en efectivo o tarjeta, cálculo del cambio y descuento automático del stock (`StockMovementType.Sale`).
2. **Cierre de caja diario**: totales por método de pago y exportación a PDF/Excel.
3. **Ingresos y Pagos**: formularios simples sobre `CashMovement`, con categorías.
4. **Historial de movimientos** de cada product en el inventario.
5. **Copia de seguridad automática** del archivo `museo.db` al cerrar la app.
6. **Instalador**: `dotnet publish -c Release -r win-x64 --self-contained` e Inno Setup o MSIX.

## Camino a la versión web

1. Crear un proyecto **Blazor Web App** que referencie `QuickInventory.UI` y use `Main` como componente raíz.
2. Cambiar el proveedor de EF Core de SQLite a PostgreSQL o SQL Server en `AddData`.
3. Añadir inicio de sesión (ASP.NET Core Identity), que en escritorio no hace falta.

Las pantallas, los modelos y las reglas de negocio se reutilizan sin cambios.
