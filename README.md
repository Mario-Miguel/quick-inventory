# Museo de la Sidra — Gestión

Aplicación de escritorio para Windows que lleva el inventario, las ventas, los ingresos y los pagos del museo.

Está hecha con **C# / .NET 10**, **Blazor Hybrid** dentro de una ventana **WPF**, **MudBlazor** para los componentes visuales y **PostgreSQL** con **Entity Framework Core** para los datos. Todos los equipos del museo comparten la misma base de datos, alojada en la nube.

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
- **Asistente de primera instalación**: pide los datos del titular (nombre o razón social, NIF y dirección). Luego se cambian desde Admin → «Datos del titular».
- **Bloqueo sin conexión**: si no se llega a la base de datos, la app se tapa con un aviso y no deja hacer nada hasta que vuelve la conexión.
- Productos de ejemplo la primera vez que se abre en Debug (puedes darlos de baja).

## Requisitos para programar

- Windows 10 u 11.
- **Visual Studio 2026** (o Visual Studio 2022 17.14 o posterior) con estas cargas de trabajo:
  - _Desarrollo de escritorio de .NET_
  - _Desarrollo de ASP.NET y web_
- SDK de **.NET 10** (se instala con Visual Studio).
- **WebView2 Runtime**: ya viene con Windows 10/11 actualizados.
- **PostgreSQL** para desarrollo. Lo más cómodo es Docker:

  ```powershell
  docker run -d --name museo-postgres -p 5432:5432 -e POSTGRES_PASSWORD=postgres -v museo-pgdata:/var/lib/postgresql/data postgres:17
  ```

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
├── QuickInventory.Data      EF Core + PostgreSQL, migraciones e implementación de los servicios.
├── QuickInventory.UI        Pantallas Blazor (Razor Class Library).
└── QuickInventory.Desktop   Ventana WPF que aloja las pantallas.
tests/
└── QuickInventory.Core.Tests  Tests (xUnit) de las reglas de Core.
deploy/postgres/             Scripts para preparar la base de datos del museo.
```

Las dependencias van siempre hacia dentro: `Desktop → UI → Core` y `Desktop → Data → Core`.
**UI no conoce Data**: las pantallas solo hablan con interfaces como `IInventoryService`. Gracias a eso, la futura web podrá usar las mismas pantallas con otra implementación de los servicios.

## Base de datos

### Conexión

La app lee la cadena de conexión `ConnectionStrings:Museo` de estos sitios, y cada uno sustituye al anterior:

1. `appsettings.json` junto al `.exe`: el PostgreSQL local de desarrollo.
2. `%LOCALAPPDATA%\QuickInventory\appsettings.json`: la conexión de cada equipo. Para apuntar a otra base basta con crear este archivo:

   ```json
   {
     "ConnectionStrings": {
       "Museo": "Host=servidor.ejemplo.com;Port=5432;Database=museo;Username=museo_app;Password=...;SSL Mode=VerifyFull;Timeout=5"
     }
   }
   ```

3. La variable de entorno `QUICKINVENTORY_ConnectionStrings__Museo`. También la usa `dotnet ef`.

Si al arrancar no puede conectar, la app avisa con la ruta del archivo de configuración y se cierra. Si la conexión se pierde con la app abierta, se bloquea hasta que vuelve.

### Migraciones

El esquema sale de las migraciones de EF Core (`src/QuickInventory.Data/Migrations`). **La base no se borra nunca**: guardará registros de facturación que hay que conservar años.

```bash
dotnet tool restore   # una vez: instala dotnet-ef (herramienta local, ver dotnet-tools.json)

# Después de cambiar un modelo:
dotnet ef migrations add <Nombre> --project src/QuickInventory.Data --startup-project src/QuickInventory.Data --output-dir Migrations

# Aplicarlas a una base (la de la variable de entorno o, si no está, la local):
dotnet ef database update --project src/QuickInventory.Data --startup-project src/QuickInventory.Data
```

- En **Debug** la app aplica sola las migraciones pendientes al arrancar.
- En **Release** no toca el esquema: solo comprueba que la base y la app van por la misma migración y, si no, avisa y se cierra. En el museo las migraciones se aplican con `dotnet ef database update`, con el usuario **propietario** de la base, al publicar cada versión.
- Los equipos se conectan con un usuario sin permisos para cambiar el esquema. Se crea con `deploy/postgres/usuario-app.sql`.

### Almacenamiento

Los importes se guardan en **céntimos enteros** para evitar errores de redondeo. Lo hace un conversor en `DbContext` y es transparente para el resto del código, que trabaja con `decimal`. Las fechas son la hora local del museo, sin huso horario. Más detalles en `docs/base-de-datos.md`.

## Siguientes pasos sugeridos

1. **Sales (TPV)**: rejilla de botones con los productos más vendidos, carrito, cobro en efectivo o tarjeta, cálculo del cambio y descuento automático del stock (`StockMovementType.Sale`).
2. **Cierre de caja diario**: totales por método de pago y exportación a PDF/Excel.
3. **Ingresos y Pagos**: formularios simples sobre `CashMovement`, con categorías.
4. **Historial de movimientos** de cada product en el inventario.
5. **Copias de seguridad** de la base en la nube con retención larga (ver `docs/verifactu.md`).
6. **Instalador**: `dotnet publish -c Release -r win-x64 --self-contained` e Inno Setup o MSIX.

## Camino a la versión web

1. Crear un proyecto **Blazor Web App** que referencie `QuickInventory.UI` y use `Main` como componente raíz.
2. Registrar `AddData` con la cadena de conexión de la configuración del servidor (ya usa PostgreSQL).
3. Añadir inicio de sesión (ASP.NET Core Identity), que en escritorio no hace falta.

Las pantallas, los modelos y las reglas de negocio se reutilizan sin cambios.
