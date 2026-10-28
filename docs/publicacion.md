# Publicar versiones y actualizaciones

Cada cliente tiene **su propia línea de versiones**: publicar la 1.3.0 para `museo-sidra` no cambia nada a
`ejemplo`. Lo hace GitHub Actions; los equipos del cliente ven un botón «Actualizar a la versión X» en la barra
superior y se actualizan solos con [Velopack](https://velopack.io).

## Cómo funciona

```
git tag museo-sidra/v1.3.0 ─► publish.yml
                               ├─ tests (ci.yml)
                               ├─ paquete (Windows): dotnet publish -p:Client -p:Version -p:UpdateUrl → vpk pack
                               └─ desplegar (environment "museo-sidra"):
                                    ├─ migraciones a la base del cliente (secreto DB_CONNECTION)
                                    └─ sube los archivos a la release "updates-museo-sidra" de este repo
                                                        ▲
equipos del museo ── cada 2 h y al arrancar ────────────┘  releases/download/updates-museo-sidra/releases.win.json
```

- **Una release fija por cliente** en las releases de este mismo repo, con el tag `updates-<cliente>`
  (`updates-museo-sidra`). No puede ser solo `museo-sidra`: git no deja tener a la vez el tag `museo-sidra` y
  los de versión `museo-sidra/v1.3.0`. Se crea sola en la primera publicación, sin marcarse como «Latest». Guarda todas sus versiones: el instalador (`QuickInventory-museo-sidra-win-Setup.exe`), el paquete completo de
  cada versión, las actualizaciones _delta_ (solo lo que cambia) y el índice `releases.win.json`.
- La dirección de esa release va **dentro del .exe** (`-p:UpdateUrl`, ver `VelopackUpdater`). Una compilación
  local no la lleva, así que no busca actualizaciones.
- El **instalador es público** (cualquiera con el enlace lo descarga), así que **nunca lleva la conexión a la
  base de datos**: el pipeline falla si aparece un `appsettings.json` en el paquete. La conexión se configura una
  vez en cada equipo (`%LOCALAPPDATA%\QuickInventory\appsettings.json`, ver README).
- **Migraciones**: se aplican justo antes de subir la versión. Desde ese momento los equipos con la versión
  anterior no arrancan («la base de datos es de una versión más nueva»), pero en lugar de solo avisar ofrecen
  actualizar (`App.OfferUpdate`). Los que ya están abiertos siguen funcionando mientras las migraciones solo
  añadan cosas, y muestran el botón en un máximo de 2 horas. Por eso las migraciones tienen que ser
  **compatibles con la versión anterior** durante ese rato: añadir columnas o tablas sí; renombrar o borrar,
  en dos versiones.

## Puesta en marcha (una vez)

1. El repo tiene que ser **público**: los equipos descargan las actualizaciones sin iniciar sesión. (Si algún día
   pasa a privado, las releases tendrán que ir a otro repo público.) No hace falta ningún token: el workflow sube
   los archivos con su propio `GITHUB_TOKEN`.
2. Por cada cliente, _Settings → Environments → New environment_ con el **mismo nombre que su carpeta** de
   `clientes/` (`museo-sidra`):
   - Secreto `DB_CONNECTION`: cadena de conexión del **usuario propietario** de su base (el que puede cambiar el
     esquema; no `museo_app`).
   - Recomendado: _Required reviewers_ contigo, para aprobar cada publicación antes de tocar su base.
3. La base tiene que aceptar conexiones desde los servidores de GitHub. Si el firewall solo deja entrar a IPs
   conocidas, hará falta abrirlo a los [rangos de GitHub Actions](https://api.github.com/meta) o usar un
   _self-hosted runner_.

## Publicar una versión

```bash
git tag museo-sidra/v1.3.0
git push origin museo-sidra/v1.3.0
```

O desde _Actions → Publicar versión → Run workflow_, indicando cliente y versión. La versión tiene que ser mayor
que la última de ese cliente. Al terminar, el resumen del workflow muestra el enlace al instalador.

## Instalar en un equipo nuevo

1. Descarga y ejecuta `https://github.com/Mario-Miguel/quick-inventory/releases/download/updates-<cliente>/QuickInventory-<cliente>-win-Setup.exe`.
   Se instala en `%LOCALAPPDATA%\QuickInventory-<cliente>` sin pedir permisos de administrador y crea el acceso
   directo.
2. Crea `%LOCALAPPDATA%\QuickInventory\appsettings.json` con la conexión (ver README, «Conexión»).

El instalador no está firmado: Windows SmartScreen avisará la primera vez («Más información → Ejecutar de todas
formas»). Para quitar el aviso hace falta un certificado de firma de código (`vpk pack --signTemplate`/`--azureTrustedSignFile`).

## Pendiente

- Icono propio por cliente (`vpk pack --icon`, y `ApplicationIcon` en el .csproj).
- Las versiones antiguas se quedan en la release. Velopack solo necesita el último paquete completo y los deltas
  recientes; si ocupa demasiado, se pueden borrar los `-full.nupkg` viejos.
