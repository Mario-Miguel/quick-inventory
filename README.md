# Quickinventory — Gestión

Aplicación de escritorio para Windows que lleva el inventario, las ventas, los ingresos y los pagos.

Está hecha con **C# / .NET 10**, **Blazor Hybrid** dentro de una ventana **WPF**, **MudBlazor** para los componentes visuales y **PostgreSQL** con **Entity Framework Core** para los datos. Todos los equipos comparten la misma base de datos, alojada en la nube.

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
- **Librería VERI\*FACTU** (`QuickInventory.Verifactu`): genera los registros de facturación con su huella y su QR, y los envía a la AEAT. Todavía no la usa la app (ver `docs/verifactu.md`).
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
  $env:POSTGRES_PASSWORD = '<elige una contraseña>'
  docker run -d --name quickinventory -p 5432:5432 -e POSTGRES_PASSWORD -v quickinventory-pgdata:/var/lib/postgresql/data postgres:17
  ```

  `-e POSTGRES_PASSWORD` sin valor pasa al contenedor la variable de entorno del mismo nombre, así la contraseña
  no queda escrita en el README ni en el comando de Docker. Usa un volumen propio para cada proyecto: si el volumen ya tiene datos, postgres ignora `POSTGRES_USER`,
  `POSTGRES_PASSWORD` y `POSTGRES_DB`. Después crea `%LOCALAPPDATA%\QuickInventory\appsettings.json` con la
  conexión a ese contenedor (ver [Conexión](#conexión)).

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

## Clientes

La misma aplicación se puede compilar para distintos clients. Cada uno tiene una carpeta en `clients/`
con su `client.json`:

```json
{
  "AppName": "Museo de la Sidra",
  "Colors": { "Primary": "#2F5D2A", "Secondary": "#C8922A", "Background": "#FAF7F2" },
  "Features": { "Verifactu": true, "PrestaShop": true },
  "Issuer": { "Nif": "G33123456", "LegalName": "Fundación ...", "Address": "..." },
  "Database": { "Port": 5432, "Name": "quickinventory", "Username": "museo_app" }
}
```

- `AppName`: nombre de la barra superior, del título de la ventana y de los avisos.
- `Colors`: colores en formato `#RRGGBB`. `Primary` es el de los botones y la barra superior.
- `Features`: módulos opcionales. Sin `PrestaShop` desaparecen «Entradas online» y «Tienda online».
  `Verifactu` todavía no cambia nada: la app aún no envía facturas (ver `docs/verifactu.md`).
- `Issuer` (opcional): datos fiscales del titular. Si están, el asistente de la primera instalación sale ya
  relleno y solo hay que confirmarlos. Si no, se escriben a mano en el asistente.
- `Database` (opcional): base del cliente, **sin servidor ni contraseña** (`Port` es 5432 si no se pone). El
  servidor lo mete el pipeline al compilar desde el secreto `DB_HOST` del environment del cliente (`-p:DbHost`), para que no esté en el
  código. Con él, en una versión publicada (Release), la primera vez que se abre la app en un equipo sale la pantalla
  «Conexión con la base de datos», que solo pide la contraseña, la prueba (con TLS verificando el certificado) y la
  guarda en `%LOCALAPPDATA%\QuickInventory\appsettings.json`. Vuelve a salir si la contraseña guardada deja de
  servir. **En Debug nunca sale**: la conexión viene siempre de `appsettings.json`.

El `client.json` va **dentro del ejecutable**: el client no puede cambiarlo, y para cambiar algo hay que volver a
compilar. Si tiene algún error, la app avisa al arrancar y se cierra.

La **contraseña** nunca va en `client.json`. Para desarrollar, la conexión completa puede ir en
`clients/<id>/appsettings.json` (mismo formato que en [Conexión](#conexión), está en `.gitignore`), que se copia
junto al `.exe` y tiene preferencia sobre la pantalla de contraseña.

Para generar el ejecutable de un client (sirve también desde Linux/WSL):

```bash
dotnet publish src/QuickInventory.Desktop -c Release -r win-x64 --self-contained -p:Client=<id> -o publish/<id>
```

Sale una carpeta `publish/<id>` con `QuickInventory.exe`, que se copia tal cual al equipo del client. Con
`--self-contained` no hace falta instalar .NET en ese equipo (WebView2 sí, pero ya viene con Windows 10/11).
Sin `-p:Client`, también al pulsar F5, se compila `museo-sidra`. `clients/ejemplo` es un client de muestra con
otros colores, sin módulos opcionales y con los datos fiscales rellenos.

**Probar otro client al depurar.** En Debug el ejecutable lleva dentro todos los clients, y la variable de
entorno `QUICKINVENTORY_CLIENT=<id>` elige con cuál arranca (sin ella, el compilado). No hace falta recompilar:

- VS Code: al pulsar F5 pregunta el client (lista `inputs` de `.vscode/launch.json`).
- Terminal: `QUICKINVENTORY_CLIENT=ejemplo dotnet run --project src/QuickInventory.Desktop`.

En Release la variable no hace nada: solo va dentro el client compilado.

Para un client nuevo: copia `clients/ejemplo` a `clients/<id>` y cambia su `client.json`. Para depurarlo, añade su id
a la lista de `.vscode/launch.json`.

## Publicar versiones

GitHub Actions pasa los tests en cada push (`.github/workflows/ci.yml`) y, al subir un tag `<client>/v<versión>`
(p. ej. `museo-sidra/v1.3.0`), compila el ejecutable de ese client, aplica las migraciones a su base y publica
la versión (`publish.yml`). Los equipos del client ven el botón «Actualizar a la versión X» y se actualizan
solos. Cada client tiene sus propias versiones. Cómo configurarlo y cómo instalar un equipo nuevo:
[docs/publicacion.md](docs/publicacion.md).

## Estructura

```
QuickInventory.slnx
src/
├── QuickInventory.Core      Modelos y contratos de servicio. Sin dependencias.
├── QuickInventory.Data      EF Core + PostgreSQL, migraciones e implementación de los servicios.
├── QuickInventory.UI        Pantallas Blazor (Razor Class Library).
├── QuickInventory.Desktop   Ventana WPF que aloja las pantallas.
└── QuickInventory.Verifactu Librería VERI*FACTU: registros, huella, QR y envío a la AEAT. Sin dependencias.
clients/                    Personalización de cada client (ver «Clientes»).
tests/
├── QuickInventory.Core.Tests       Tests (xUnit) de las reglas de Core.
└── QuickInventory.Verifactu.Tests  Tests de la librería VERI*FACTU.
deploy/postgres/             Scripts para preparar la base de datos.
```

Las dependencias van siempre hacia dentro: `Desktop → UI → Core` y `Desktop → Data → Core`.
**UI no conoce Data**: las pantallas solo hablan con interfaces como `IInventoryService`. Gracias a eso, la futura web podrá usar las mismas pantallas con otra implementación de los servicios.

## Base de datos

### Conexión

La app lee la cadena de conexión `ConnectionStrings:Quickinventory` de estos sitios, y cada uno sustituye al anterior:

1. `appsettings.json` junto al `.exe` (opcional).
2. `%LOCALAPPDATA%\QuickInventory\appsettings.json`: la conexión de cada equipo. Es el sitio habitual, también para desarrollar:

   ```json
   {
     "ConnectionStrings": {
       "Quickinventory": "Host=servidor.ejemplo.com;Port=5432;Database=quickinventory;Username=quickinventory_app;Password=...;SSL Mode=VerifyFull;Timeout=5"
     }
   }
   ```

3. La variable de entorno `QUICKINVENTORY_ConnectionStrings__Quickinventory`. También la usa `dotnet ef`.

Ninguno de estos archivos está en el repositorio, porque llevan la contraseña: cada uno crea el suyo
(`src/QuickInventory.Desktop/appsettings.json` está en `.gitignore`). Sin ninguno, la app avisa al arrancar de que
no hay base de datos configurada.

Si al arrancar no puede conectar, la app avisa con la ruta del archivo de configuración y se cierra. Si la conexión se pierde con la app abierta, se bloquea hasta que vuelve.

### Migraciones

El esquema sale de las migraciones de EF Core (`src/QuickInventory.Data/Migrations`). **La base no se borra nunca**: guardará registros de facturación que hay que conservar años.

```bash
dotnet tool restore   # una vez: instala dotnet-ef (herramienta local, ver dotnet-tools.json)

# Después de cambiar un modelo:
dotnet ef migrations add <Nombre> --project src/QuickInventory.Data --startup-project src/QuickInventory.Data --output-dir Migrations

# Aplicarlas a la base de la variable de entorno QUICKINVENTORY_ConnectionStrings__Quickinventory:
dotnet ef database update --project src/QuickInventory.Data --startup-project src/QuickInventory.Data
```

`dotnet ef` no lee los `appsettings.json`: solo la variable de entorno. Sin ella falla con
«The ConnectionString property has not been initialized» (también `migrations remove`, que consulta la base para
saber si la migración ya está aplicada).

#### Aplicar una migración paso a paso

1. Definir la variable con un usuario **propietario** de la base (no `Quickinventory_app`, que no puede cambiar el esquema):

   ```bash
   export QUICKINVENTORY_ConnectionStrings__Quickinventory='Host=...;Port=5432;Database=Quickinventory;Username=<propietario>;Password=...'
   ```

2. Ver cuáles faltan. Las pendientes salen marcadas con `(Pending)`:

   ```bash
   dotnet ef migrations list --project src/QuickInventory.Data --startup-project src/QuickInventory.Data
   ```

3. Aplicarlas con `dotnet ef database update …` (arriba). Para revisar antes el SQL que se ejecutará, sobre todo
   en la base del Quickinventory: `dotnet ef migrations script <ÚltimaAplicada> --idempotent --project src/QuickInventory.Data --startup-project src/QuickInventory.Data`.
4. Volver a ejecutar `migrations list` y comprobar que no queda ninguna `(Pending)`.

Las tablas nuevas no necesitan permisos aparte: `deploy/postgres/usuario-app.sql` ya da a `Quickinventory_app` acceso a las
que cree el propietario en el futuro.

Para deshacer la última migración **antes de subirla a git** (si ya está aplicada en otra base, no se toca: se
corrige con una migración nueva): `dotnet ef database update <Anterior> …` y después `dotnet ef migrations remove …`.

- En **Debug** la app aplica sola las migraciones pendientes al arrancar.
- En **Release** no toca el esquema: solo comprueba que la base y la app van por la misma migración y, si no, avisa y se cierra. En Quickinventory las migraciones se aplican con `dotnet ef database update`, con el usuario **propietario** de la base, al publicar cada versión.
- Los equipos se conectan con un usuario sin permisos para cambiar el esquema. Se crea con `deploy/postgres/usuario-app.sql`.

### Almacenamiento

Los importes se guardan en **céntimos enteros** para evitar errores de redondeo. Lo hace un conversor en `DbContext` y es transparente para el resto del código, que trabaja con `decimal`. Las fechas son la hora local de Quickinventory, sin huso horario. Más detalles en `docs/base-de-datos.md`.

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
