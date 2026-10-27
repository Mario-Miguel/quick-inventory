# Integración con VERI\*FACTU — análisis y plan de implementación

Documento de trabajo para añadir a la aplicación del Museo de la Sidra el cumplimiento del
Reglamento de Sistemas Informáticos de Facturación (RD 1007/2023, Orden HAC/1177/2024) en
modalidad **VERI\*FACTU** (remisión voluntaria e inmediata de los registros a la AEAT).

Basado en la documentación técnica de la AEAT guardada en `.internalDocs/Verifactu/`:

| Documento | Para qué sirve aquí |
| --- | --- |
| `SistemaFacturacion.wsdl.xml` | Operaciones SOAP y URL de los servicios (pruebas y producción). |
| `SuministroLR.xsd.xml`, `SuministroInformacion.xsd.xml` | Formato del envío: `RegistroAlta` y `RegistroAnulacion`. |
| `RespuestaSuministro.xsd.xml` | Respuesta al envío: CSV, estado global, estado por registro, `TiempoEsperaEnvio`. |
| `ConsultaLR.xsd.xml`, `RespuestaConsultaLR.xsd.xml` | Consulta de los registros ya presentados. |
| `Veri-Factu_Descripcion_SWeb.pdf` (v1.0.3) | Funcionamiento del servicio web, operativas de alta, subsanación y anulación, control de flujo. |
| `Veri-Factu_especificaciones_huella_hash_registros.pdf` (v0.1.2) | Cálculo de la huella (SHA-256) y encadenamiento. Incluye 3 ejemplos con el resultado esperado. |
| `DetalleEspecificacTecnCodigoQRfactura.pdf` (v0.5.0) | URL y presentación del código QR en la factura. |
| `Validaciones_Errores_Veri-Factu.pdf` (v1.2.2) y `errores.properties.txt` | Reglas de negocio que comprueba la AEAT y códigos de error. |
| `EspecTecGenerFirmaElectRfact.pdf`, `EventosSIF.xsd.xml`, `RespuestaValRegistNoVeriFactu.xsd.xml` | Firma XAdES y registro de eventos. **Solo para sistemas «No VERI\*FACTU»**; con la propuesta de este documento no hacen falta. |
| `EjemplosDeclaracionResponsable(V0.5.1).pdf` | Modelo de la declaración responsable que debe acompañar al software. |

---

## 1. Resumen

- **Sí se puede hacer como un módulo independiente**: un proyecto nuevo `QuickInventory.Verifactu` que no conoce
  la base de datos ni las pantallas. Recibe datos de una factura y devuelve el registro (XML, huella, URL del QR),
  y sabe enviarlo a la AEAT e interpretar la respuesta. La aplicación lo usa desde `QuickInventory.Data`.
- **La parte de comunicación con la AEAT es la más pequeña.** Lo que de verdad falta es que la aplicación
  **emita facturas**: hoy una venta no tiene número de factura, ni serie, ni IVA desglosado, ni se imprime ningún
  ticket. VERI\*FACTU da por hecho que todo eso existe.
- **Recomendación: funcionar solo como VERI\*FACTU** (`TipoUsoPosibleSoloVerifactu = S`). Así no hace falta la
  firma electrónica XAdES de cada registro, ni el registro de eventos, ni la detección de anomalías, que son
  obligatorios en la modalidad «No VERI\*FACTU» y suponen bastante más trabajo.
- **Antes de programar nada hay que resolver con la asesoría fiscal las preguntas del apartado 2.** En concreto,
  es posible que las **entradas** del museo no necesiten factura (y, por tanto, no generen registros), y que el
  museo ni siquiera esté obligado, según quién sea su titular.

---

## 2. Decisiones previas (asesoría fiscal / dirección del museo)

Estas respuestas cambian el alcance del trabajo. Ninguna se puede deducir del código.

> **Estado (21-10-2026)**: se sigue adelante sin esperar a la asesoría, con estas decisiones de diseño:
>
> - **D1**: los datos del titular (NIF, nombre o razón social, dirección) se piden en un **asistente de primera
>   instalación** y se guardan en la tabla `Issuers`. Hecho en la fase 0. Si el museo resulta no estar obligado,
>   la facturación VERI\*FACTU se podrá desactivar (`VerifactuSettings.Enabled`).
> - **D3 y D4**: el IVA (tipo o exención) se indica **en cada producto** al darlo de alta en el inventario,
>   **también en las entradas**. Así no hace falta decidir de antemano si las entradas están exentas: lo decide quien
>   da de alta el producto. Se hace en la fase 2.
> - **D6**: base de datos compartida en la nube (2.1).
> - D2, D5, D7, D8 y D9 quedan pendientes; no bloquean las fases 0 y 1.

| # | Pregunta | Por qué importa |
| --- | --- | --- |
| D1 | ¿Quién es el titular del museo (ayuntamiento, fundación, empresa, asociación) y cuál es su NIF? | El reglamento se aplica a contribuyentes del IS, del IRPF por actividades económicas y del IRNR con establecimiento permanente. Las entidades **totalmente exentas del IS** (art. 9.1 LIS: Estado, CCAA, entidades locales…) podrían no estar obligadas. Tampoco lo están las que llevan los libros por el SII. |
| D2 | ¿Desde qué fecha es obligatorio para ese titular? | A fecha de este documento, el RDL 15/2025 fijó el **1 de enero de 2027** para contribuyentes del IS y el **1 de julio de 2027** para el resto. **Comprobarlo**, porque las fechas ya se han aplazado más de una vez. |
| D3 | ¿Qué IVA llevan las **entradas**? ¿Están exentas por el art. 20.Uno.14.º LIVA (servicios culturales de entidades públicas o culturales privadas de carácter social)? | Si están exentas por ese artículo, el art. 3 del RD 1619/2012 dispensa de expedir factura, y **las entradas no generarían registros VERI\*FACTU**. Solo la tienda los generaría. Si no están exentas, llevan IVA (normalmente 21 %) y sí hay que facturar. |
| D4 | ¿Qué tipo de IVA lleva cada producto de la **tienda**? (sidra y alimentación, libros, merchandising…) | Hace falta guardar el tipo en cada producto y desglosarlo en la factura (bloque `Desglose`, hasta 12 líneas por tipo). |
| D5 | ¿Alguna vez se cobra más de **400 €** (o 3.000 € en ventas al por menor) en una sola venta, o un cliente pide factura con su NIF (colegios, grupos, agencias)? | Por encima de esos límites, o si se pide, hay que emitir **factura completa** (`F1`, con los datos del cliente) o una factura que sustituye a la simplificada (`F3`). El primer alcance puede limitarse a simplificadas (`F2`). |
| D6 | ¿Taquilla y Tienda usan **el mismo ordenador**? ¿Habrá más de un equipo con la aplicación? | Hoy cada equipo tiene su propia base de datos (`%LOCALAPPDATA%`). Cada equipo sería una **instalación distinta** del sistema, con su propia cadena de registros, su propio `NumeroInstalacion` y una **serie de factura distinta** (si no, dos equipos podrían emitir el mismo número). **Respondida: base de datos compartida en la nube (ver 2.1).** |
| D7 | ¿Con qué **certificado electrónico** se enviarán los registros? | Hace falta un certificado cualificado: de representante de persona jurídica, de sello de entidad, o de un apoderado o colaborador social. Con certificado de sello se usan las URL `www10`/`prewww10`. |
| D8 | ¿Quién es el **productor** del software (nombre/razón social y NIF)? | Va en el bloque `SistemaInformatico` de cada registro y en la **declaración responsable**. Si lo desarrolla el propio museo, el productor es el museo; si es un desarrollador externo, es esa persona o empresa. |
| D9 | Las entradas vendidas en la **web (PrestaShop)** las factura PrestaShop. | Es otro sistema de facturación: tiene que cumplir por su cuenta (módulo VERI\*FACTU para PrestaShop). Queda fuera de este proyecto, pero hay que tenerlo en cuenta. |

### 2.1 Respuesta a D6: una base de datos compartida en la nube

**Decisión**: todos los equipos (Taquilla, Tienda y los que se añadan) se conectan a **una sola base de datos
alojada en un servicio gestionado** (AWS, Azure o GCP), en lugar de tener cada uno su `museo.db`. Así comparten
inventario, ventas y caja.

**Consecuencias para VERI\*FACTU**:

- **Una sola instalación del sistema**: un `NumeroInstalacion`, una cadena de registros y una fila de
  `VerifactuSettings`, compartidos por todos los equipos. No hace falta una serie por equipo ni por punto de venta:
  basta con **una serie por año, común a Taquilla y Tienda** (fase 2). Las rectificativas irán en su propia serie.
- **Generar el registro hay que hacerlo de uno en uno entre equipos.** Con SQLite bastaba la transacción; con dos
  equipos cobrando a la vez, los dos podrían leer el mismo «último registro» y romper la cadena, o coger el mismo
  número de factura. Dentro de la transacción de la venta:
  1. Tomar un bloqueo exclusivo que dure hasta el `COMMIT`. En PostgreSQL: `SELECT pg_advisory_xact_lock(<id fijo>)`,
     o `SELECT … FROM "VerifactuSettings" FOR UPDATE`.
  2. Con el bloqueo tomado: leer el último registro, incrementar `InvoiceSeries.LastNumber`, tomar la hora
     (`FechaHoraHusoGenRegistro`) y escribir el registro nuevo. Así el orden de la cadena coincide con el de la hora.
  3. Como red de seguridad, se mantienen los índices únicos en `PreviousRecordId` y en (serie, número). Si alguno
     salta, se reintenta la venta entera.

  El bloqueo dura milisegundos, así que no se nota aunque haya varios equipos.
- **El envío a la AEAT lo hace un solo equipo a la vez.** Si `VerifactuSender` corre en todos los equipos, dos podrían
  enviar los mismos registros, y además cada uno llevaría su propia cuenta del `TiempoEsperaEnvio`, aunque es la
  misma instalación. (**Comprobar** en la Orden y en `Veri-Factu_Descripcion_SWeb.pdf` si la AEAT controla la espera
  por obligado tributario, por sistema o por instalación.) Opciones:
  - **(Recomendada al principio)** El sender corre en todos los equipos, pero antes de enviar toma un bloqueo
    (`pg_try_advisory_lock`) o un «turno» en `VerifactuSettings` (`SenderId` + `SenderLeaseUntil`, renovado en cada
    ciclo). Solo envía el que lo tenga; si ese equipo se apaga, el turno caduca y lo coge otro. `NextSendAt` ya está en
    la base, así que la espera se respeta entre todos.
  - Más adelante, sacar el envío a un proceso en la nube (la futura versión web como `IHostedService`, o una función
    programada). Así el certificado está en un único sitio (Key Vault / Secrets Manager / Secret Manager) en vez de en
    cada equipo. Ver D7.
- **La hora de cada equipo cuenta.** La AEAT exige que `FechaHoraHusoGenRegistro` sea la hora actual con un margen
  (error `2004`). Con varios equipos, todos deben tener la hora sincronizada (la sincronización automática de Windows
  basta). Mostrar aviso si un equipo se desvía (ver riesgos).

**Consecuencias para la aplicación en general**:

- **Sin internet no se puede cobrar.** Esto cambia el punto 1 del diseño (apartado 5): la venta sigue sin esperar a
  la AEAT, pero ahora sí depende de llegar a la base de datos.

  **Decisión**: se asume que el museo **siempre tiene conexión**. Si se pierde, la app **se bloquea** y no permite
  hacer operaciones hasta que vuelva (solución a corto plazo). El funcionamiento sin conexión queda fuera de alcance
  (apartado 9). En concreto:
  - Un servicio de Data comprueba la conexión con la base cada pocos segundos (una consulta mínima, `SELECT 1`).
    Además, cualquier error de conexión en una operación marca el estado como «sin conexión» al momento, sin esperar
    a la siguiente comprobación. La UI lo ve a través de una interfaz de Core (p. ej. `IConnectionStatus`), sin
    referenciar Data.
  - Mientras no hay conexión, el layout muestra una pantalla encima de todo: «Sin conexión con el servidor. No se
    pueden hacer ventas ni cambios. La aplicación se desbloqueará sola cuando vuelva la conexión». Se reintenta solo,
    y además hay un botón «Reintentar».
  - **Si la conexión se corta a mitad de un cobro**, la transacción no llega al `COMMIT`: no hay venta, ni número, ni
    registro. La pantalla tiene que decir claramente «La venta **no** se ha registrado. No entregue ticket» y no
    imprimir nada. Si el error llega después del `COMMIT` (se registró, pero se perdió la respuesta), al volver la
    conexión hay que poder ver la venta en el resumen y reimprimir su ticket.
  - **Preguntar a la asesoría** qué hacer mientras el sistema no está disponible (¿se puede cobrar con factura manual
    y registrarla después?).
- **Proveedor de base de datos**: **PostgreSQL gestionado** (Azure Database for PostgreSQL Flexible Server, Amazon
  RDS for PostgreSQL o Cloud SQL for PostgreSQL). Encaja con lo previsto en `CLAUDE.md` (cambiar el proveedor en
  `AddData`, la UI no cambia). Se elige PostgreSQL frente a SQL Server por precio, por los bloqueos *advisory* y
  porque Npgsql funciona muy bien con EF Core. Las tres nubes sirven igual; lo más práctico es usar la que ya tenga
  el titular del museo (D1). En cualquier caso:
  - **Región en España o en la UE**: Azure *Spain Central*, AWS *eu-south-2* (Madrid) o GCP *europe-southwest1*
    (Madrid).
  - La instancia más pequeña sobra para el volumen del museo. Comprobar precios: la factura mensual depende del
    tamaño, del almacenamiento y de las copias que se guarden.
- **Seguridad**: con una app de escritorio que conecta directamente a la base, la cadena de conexión está en cada
  equipo. Para reducir el riesgo:
  - **Firewall** del servicio limitado a la IP pública del museo (mejor si es fija), y TLS obligatorio
    (`SSL Mode=VerifyFull`).
  - **Usuario de la app con permisos mínimos**: sin `DELETE`/`UPDATE` sobre `VerifactuRecords` (solo `INSERT` y
    `SELECT`). Así la base garantiza la regla «los registros no se modifican ni se borran», y no solo el código.
  - Las migraciones se aplican con **otro usuario** (propietario del esquema), que no se instala en los equipos.
  - Cadena de conexión fuera del código, protegida con DPAPI en Desktop (`ProtectedData`, ámbito del usuario).
  - Más adelante, con la versión web, se puede poner una API delante y quitar el acceso directo a la base desde los
    equipos.
- **Migraciones**: se aplican **una vez, desde desarrollo** (`dotnet ef database update` o un *migration bundle*) al
  publicar una versión, no con `Migrate()` al arrancar en cada equipo. Si varios equipos arrancan a la vez con una
  versión nueva, podrían intentar migrar a la vez, y además el usuario de la app no tiene permisos para cambiar el
  esquema. Al arrancar, la app solo comprueba que la base está en la versión que espera y, si no, avisa.
- **Copias de seguridad**: las hace el servicio (copias automáticas y restauración a un momento concreto). La
  retención por defecto es de días o semanas, y los registros hay que conservarlos años. Hace falta además una
  **exportación periódica** (por ejemplo, mensual) a almacenamiento de objetos con retención larga e inmutable (S3
  Object Lock, Azure Blob con política de inmutabilidad, GCS Bucket Lock).
- **Datos actuales**: los equipos que ya tengan un `museo.db` con datos reales necesitan una migración única de
  SQLite a PostgreSQL (productos y stock; las ventas solo si se quieren conservar). Como todavía no hay registros
  VERI\*FACTU, esto no afecta a la cadena.
- **SQLite se puede mantener para desarrollo**, pero las migraciones EF dependen del proveedor. Lo más sencillo es
  usar PostgreSQL también en desarrollo (un contenedor Docker en WSL) y tener un solo juego de migraciones.

---

## 3. Conceptos clave (resumen de la documentación)

### 3.1 Registro de facturación

Cada vez que se **expide una factura** se genera un `RegistroAlta`. Si una factura se expidió por error (no hubo
venta real), se genera un `RegistroAnulacion`. Los registros **nunca se modifican ni se borran**: para corregir
datos se genera otro registro con la misma identificación de factura (subsanación).

La identificación única de una factura es **NIF del emisor + `NumSerieFactura` + `FechaExpedicionFactura`**.

Campos principales de un `RegistroAlta` para una factura simplificada típica del museo:

| Campo | Valor previsto |
| --- | --- |
| `IDVersion` | `1.0` |
| `IDFactura/IDEmisorFactura` | NIF del museo (igual que `Cabecera/ObligadoEmision/NIF`). |
| `IDFactura/NumSerieFactura` | Serie + número, máx. 60 caracteres ASCII 32-126, **sin** `" ' < > =`. Ej.: `F2027-000123`. |
| `IDFactura/FechaExpedicionFactura` | `dd-MM-yyyy`. No puede ser futura. |
| `NombreRazonEmisor` | Razón social del museo. |
| `TipoFactura` | `F2` (simplificada). `R5` para rectificativas de simplificadas. `F1`/`F3` si se hace la fase 2. |
| `DescripcionOperacion` | Texto libre (máx. 500), p. ej. «Venta en tienda» o «Entradas». |
| `Desglose/DetalleDesglose` (1..12) | Por cada tipo de IVA: `Impuesto=01`, `ClaveRegimen=01`, `CalificacionOperacion=S1` + `TipoImpositivo` + `BaseImponibleOimporteNoSujeto` + `CuotaRepercutida`; o `OperacionExenta=E1` + base, sin tipo ni cuota. |
| `CuotaTotal`, `ImporteTotal` | Suma de cuotas; total con IVA. Formato `\d{1,12}(\.\d{0,2})?`, punto decimal, sin ceros a la izquierda. |
| `Encadenamiento` | `PrimerRegistro=S` solo la primera vez en toda la vida de la instalación; después `RegistroAnterior` (NIF, número, fecha y **huella del registro inmediatamente anterior**, sea de alta o de anulación). |
| `SistemaInformatico` | Productor, `NombreSistemaInformatico` (≤30), `IdSistemaInformatico` (2 caracteres, mayúsculas o dígitos, sin Ñ), `Version`, `NumeroInstalacion`, `TipoUsoPosibleSoloVerifactu=S`, `TipoUsoPosibleMultiOT=N`, `IndicadorMultiplesOT=N`. |
| `FechaHoraHusoGenRegistro` | Fecha y hora **con huso horario**: `2027-01-15T11:20:30+01:00`. |
| `TipoHuella` / `Huella` | `01` (SHA-256) / huella de este registro. |

Reglas de la AEAT relevantes para el museo (de `Validaciones_Errores_Veri-Factu.pdf`):

- `F2` **no lleva** `Destinatarios`. `F1`/`F3`/`R1-R4` **los exigen**.
- `CuotaRepercutida = Base × Tipo / 100` (margen ±10 €) y con el mismo signo que la base.
- Tipos de IVA admitidos con `S1`: 0, 2, 4, 5, 7,5, 10, 21 (con fechas para algunos).
- Si hay `OperacionExenta`, **no** se informan `TipoImpositivo` ni `CuotaRepercutida`.
- `F2`: la suma de bases y cuotas no puede pasar de 3.000 € (+10 €).
- `ImporteTotal` y `CuotaTotal` deben cuadrar con el desglose (±10 €); si no, el registro queda «aceptado con errores».
- Una huella mal calculada **no rechaza** el registro, pero lo deja «aceptado con errores» y hay que subsanarlo.
- Marcar `PrimerRegistro=S` cuando ya hay registros de ese sistema y NIF también da «aceptado con errores».

### 3.2 Huella (hash) y encadenamiento

Fuente: `Veri-Factu_especificaciones_huella_hash_registros.pdf`.

```
Alta:      IDEmisorFactura=…&NumSerieFactura=…&FechaExpedicionFactura=…&TipoFactura=…&CuotaTotal=…&ImporteTotal=…&Huella=<huella anterior>&FechaHoraHusoGenRegistro=…
Anulación: IDEmisorFacturaAnulada=…&NumSerieFacturaAnulada=…&FechaExpedicionFacturaAnulada=…&Huella=<huella anterior>&FechaHoraHusoGenRegistro=…
```

- Valores **sin espacios al principio ni al final**, con el mismo contenido que en el XML. En el primer registro, `Huella=` va vacío.
- Cadena en UTF-8 → SHA-256 → **hexadecimal en mayúsculas** (64 caracteres).
- La cadena es **una sola para toda la instalación** (altas y anulaciones mezcladas, en orden cronológico de generación).
- Vectores de prueba de la AEAT (deben salir exactos en los tests):

| Caso | Huella esperada |
| --- | --- |
| Alta, primer registro (`89890001K`, `12345678/G33`, `01-01-2024`, `F1`, `12.35`, `123.45`, `2024-01-01T19:20:30+01:00`) | `3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60` |
| Alta encadenada (`12345679/G34`, `…19:20:35+01:00`) | `F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97` |
| Anulación encadenada (`12345679/G34`, `…19:20:40+01:00`) | `177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68` |

**Importante**: los importes y fechas de la cadena tienen que formatearse con **el mismo formateador** que el XML,
para que no haya diferencias (`123.1` y `123.10` valen igual para la AEAT, pero conviene ser coherentes).

### 3.3 Servicio web

- SOAP 1.1, estilo *document/literal*, UTF-8, HTTPS con **certificado de cliente** (TLS mutuo).
- Operaciones: `RegFactuSistemaFacturacion` (alta/anulación, de 1 a 1.000 registros por envío) y
  `ConsultaFactuSistemaFacturacion` (consulta, hasta 10.000 por página).
- URL (de `SistemaFacturacion.wsdl.xml`):

| Entorno | Certificado personal / representante | Certificado de sello |
| --- | --- | --- |
| Pruebas | `https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP` | `https://prewww10.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP` |
| Producción | `https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP` | `https://www10.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP` |

- **Respuesta**:
  - `EstadoEnvio`: `Correcto` / `ParcialmenteCorrecto` / `Incorrecto`.
  - Por registro, `EstadoRegistro`: `Correcto` / `AceptadoConErrores` / `Incorrecto`, con `CodigoErrorRegistro` y `DescripcionErrorRegistro`.
  - **`CSV`**: hay que guardarlo, porque no se puede recuperar después.
  - **`TiempoEsperaEnvio`**: segundos que hay que esperar antes del siguiente envío.
- **Errores SOAP (`Fault`)**:
  - `soapenv:Server` o sin respuesta → reintentar más tarde.
  - `soapenv:Client` → el XML o la cabecera están mal; se rechaza el envío entero y no tiene sentido reintentar sin corregir.
- **Duplicados**: si se reenvía un registro que la AEAT ya tiene (por ejemplo, porque se cortó la conexión tras
  enviarlo), lo rechaza y devuelve `RegistroDuplicado` con su estado. Ese caso hay que tratarlo como «ya enviado», no como un error.
- **Control de flujo** (art. 16.2 de la Orden): tras un envío, el siguiente no se hace hasta que pasen
  `TiempoEsperaEnvio` segundos (inicialmente 60) **o** haya 1.000 registros pendientes, lo que ocurra antes.
  En un museo esto significa: enviar en lotes cada minuto como mucho.

### 3.4 Operativas de corrección

| Situación | Qué se genera | Marcas |
| --- | --- | --- |
| Venta normal | Alta | — |
| Alta **rechazada** por la AEAT (no existe allí) | Nueva alta con datos corregidos | `Subsanacion=S`, `RechazoPrevio=X` |
| Alta aceptada (o con errores) con un dato mal, **sin** necesidad de rectificativa | Nueva alta con datos corregidos | `Subsanacion=S` |
| Factura expedida **por error** (no hubo venta) | Anulación | — |
| Anulación rechazada | Nueva anulación | `RechazoPrevio=S` |
| Devolución o cambio de importe (hubo venta real) | **Factura rectificativa** `R5` (de una simplificada) | `TipoRectificativa=I` (por diferencias, con importes negativos) o `S` (sustitución) |

Todas generan un registro nuevo en la cadena, con su propia huella.

### 3.5 Código QR en la factura

Fuente: `DetalleEspecificacTecnCodigoQRfactura.pdf`.

- URL (VERI\*FACTU):
  - Producción: `https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR?nif=…&numserie=…&fecha=dd-MM-yyyy&importe=123.45`
  - Pruebas: `https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?…`
- Cada valor va con *URL encoding* UTF-8 (`&` → `%26`…). Solo esos 4 parámetros: `formato=json` nunca va en el QR.
- QR ISO/IEC 18004, **nivel de corrección M**, entre **30×30 y 40×40 mm**, con margen blanco de al menos 2 mm (6 recomendados).
- **Al principio de la factura**, centrado arriba si es vertical. Encima, el texto «**QR tributario:**». Debajo,
  «**Factura verificable en la sede electrónica de la AEAT**» o «**VERI\*FACTU**», en letra igual o mayor que el resto.

### 3.6 Lo que **no** hace falta en modo «solo VERI\*FACTU»

- Firma electrónica XAdES de los registros (`EspecTecGenerFirmaElectRfact.pdf`, alcance: «solo exigible para sistemas no VERI\*FACTU»).
- Registro de eventos (`EventosSIF.xsd`), detección de anomalías, exportación firmada.
- Remisión bajo requerimiento (`RequerimientoSOAP`) y `RespuestaValRegistNoVeriFactu.xsd`.

Si en el futuro se quisiera admitir el modo «No VERI\*FACTU», habría que añadir todo eso (ver apartado 9).

---

## 4. Situación actual de la aplicación y huecos

| Aspecto | Hoy | Necesario |
| --- | --- | --- |
| Número de factura | No existe; solo `Sale.Id`. | Serie + número **correlativo y sin huecos**, asignado al cobrar. |
| IVA | No se guarda. `SalesLine` tiene precio con IVA incluido. | Tipo de IVA (o exención) por producto, **copiado** en cada línea al vender, y desglose por tipo. |
| Ticket / factura | No se imprime nada. | Factura simplificada impresa (o en pantalla/PDF) con el QR y los datos obligatorios del art. 7 del RD 1619/2012. |
| Anular venta | `CancelSaleAsync` pone `Canceled = true` y devuelve stock. | Distinguir **anulación** (venta por error → `RegistroAnulacion`) de **devolución** (venta real → rectificativa `R5`). |
| Datos del museo | No existen. | Configuración: NIF, razón social, certificado, entorno, series, `NumeroInstalacion`. |
| Esquema de BD | `EnsureCreated()` sobre un `museo.db` por equipo. | **PostgreSQL compartido en la nube** (D6) con **migraciones EF**: la base no se puede borrar ni recrear, porque los registros de facturación hay que conservarlos años. |
| Tests | No hay proyecto de tests. | Imprescindibles para la huella, el XML y los cálculos de IVA. |
| Fecha y hora | `DateTime.Now` sin huso. | `DateTimeOffset` con huso de Europe/Madrid para `FechaHoraHusoGenRegistro`. |

Cosas que **no** cambian: cierre de caja, reembolsos (son compras, no facturas emitidas), inventario y stock.

---

## 5. Arquitectura propuesta

```
QuickInventory.Core          Modelos y contratos (+ IVA en productos, número de factura en ventas,
                             IVerifactuService, VerifactuRecord…)
QuickInventory.Verifactu     NUEVO. Librería independiente: no conoce EF ni la UI ni los modelos de la app.
   ├─ Modelos/               InvoiceData, CancellationData, TaxLine, SystemInfo, Issuer…
   ├─ Huella/                FingerprintCalculator (SHA-256 según la especificación)
   ├─ Xml/                   RecordXmlBuilder (RegistroAlta / RegistroAnulacion), SoapEnvelope, validación con los XSD
   ├─ Cliente/               AeatClient (HttpClient + certificado), ResponseParser, entornos y URL
   ├─ Qr/                    QrUrlBuilder (URL de cotejo)
   └─ Esquemas/              XSD y WSDL de la AEAT como recursos incrustados (+ xmldsig-core-schema.xsd local)
QuickInventory.Data          Usa Verifactu: genera el registro en la misma transacción que la venta,
                             guarda la cadena, envía la cola y procesa respuestas.
QuickInventory.UI            Pantalla Admin «VERI*FACTU» (configuración, estado de envíos, errores),
                             ticket imprimible con QR. No referencia Verifactu ni Data.
QuickInventory.Desktop       Selección del certificado del almacén de Windows, arranque del envío en segundo plano.
QuickInventory.Verifactu.Tests  NUEVO. xUnit: huella (vectores AEAT), XML contra XSD, cálculos, parser de respuestas.
```

Dependencias: `Verifactu` no depende de nada del proyecto. `Data` → `Core` + `Verifactu`. `UI` → `Core`.
Se mantiene la regla de `CLAUDE.md`: **UI no referencia Data** (ni Verifactu). La UI solo ve `IVerifactuService`
y los modelos de Core.

### Flujo de una venta

```mermaid
sequenceDiagram
    participant UI as Pantalla de cobro
    participant S as SalesService (Data)
    participant V as QuickInventory.Verifactu
    participant DB as Base de datos (nube)
    participant Q as VerifactuSender (segundo plano)
    participant A as AEAT

    UI->>S: RegisterShopSaleAsync(...)
    S->>DB: BEGIN TRANSACTION + bloqueo de la cadena
    S->>DB: siguiente número de la serie + último registro de la cadena
    S->>V: BuildAlta(datos factura, registro anterior, ahora con huso)
    V-->>S: XML del RegistroAlta + huella + URL QR
    S->>DB: Sale (con nº factura) + VerifactuRecord (Pendiente)
    S->>DB: COMMIT
    S-->>UI: venta + datos para imprimir el ticket con QR
    Q->>DB: registros Pendientes (máx. 1000)
    Q->>A: RegFactuSistemaFacturacion (SOAP + certificado)
    A-->>Q: CSV, EstadoEnvio, estados por registro, TiempoEsperaEnvio
    Q->>DB: guarda estado, CSV, errores; programa el siguiente envío
```

Puntos clave del diseño:

1. **Generar y enviar son pasos separados.** La venta **nunca** espera a la AEAT: si la AEAT no responde, se cobra
   igual, se imprime el ticket con su QR y el registro queda pendiente. Con la base en la nube (D6), **sin internet
   la app se bloquea** y no se cobra, porque la venta no llega a la base (ver 2.1).
2. **Factura y registro en la misma transacción** (lo recomienda el ejemplo de declaración responsable). No puede
   existir una venta sin registro, ni un registro sin venta.
3. **La cadena se genera en serie.** Dentro de la transacción se toma un bloqueo exclusivo
   (`pg_advisory_xact_lock`), se lee el último registro y se escribe el nuevo. La transacción sola no basta, porque
   varios equipos comparten la base (ver 2.1).
4. **El XML guardado es el que se envía.** Se genera una vez, se guarda tal cual (`Xml`) y en los reintentos se
   envía el mismo. Nunca se regenera, para que la huella y la fecha no cambien.

---

## 6. Modelo de datos

Nuevas tablas y columnas. Todo con **migraciones EF** (fase 0). Actualizar `docs/base-de-datos.md`.

### Cambios en tablas existentes

| Tabla | Columna | Notas |
| --- | --- | --- |
| `Products` | `TaxRate` (decimal, %) | Tipo de IVA: 21, 10, 4… |
| `Products` | `TaxExemption` (texto, null) | `E1`…`E6` si está exento (p. ej. entradas por art. 20.Uno.14.º). Excluyente con `TaxRate`. |
| `SaleLines` | `TaxRate`, `TaxExemption` | **Copia** del producto al vender, como ya se hace con el nombre y el precio. |
| `Sales` | `InvoiceSeries`, `InvoiceNumber`, `InvoiceDate` | Asignados al cobrar. Índice único por (serie, número). |
| `Sales` | `InvoiceType` | `F2`, `R5`… |
| `Sales` | `RectifiedSaleId` (null) | Para rectificativas: venta original. |

### Tablas nuevas

**`VerifactuSettings`** (una fila):

- Los datos del emisor (NIF y razón social) **no** van aquí: están en `Issuers`, que se crea en la fase 0.
- `Environment` (`Test`/`Production`), `CertificateThumbprint` (o ruta y contraseña de un .pfx; ver seguridad).
- Datos del sistema: `ProducerName`, `ProducerNif`, `SystemName`, `SystemId` (2 caracteres), `InstallationNumber`.
- `Enabled`, `LastWaitSeconds`, `NextSendAt`.

**`InvoiceSeries`**: `Code` (p. ej. `F2027`), `Year` (único: una serie por año, común a Taquilla y Tienda), `LastNumber`.
Al crearla, `LastNumber` puede no ser 0 si ese año ya se emitieron facturas con otro programa. Admin la gestiona en
«Series de facturas»; una serie con facturas emitidas desde la aplicación ya no se puede cambiar.
El número se asigna incrementando `LastNumber` dentro de la transacción de la venta. Así es correlativo y sin huecos:
si la transacción falla, el número no se consume.

**`VerifactuRecords`** (la cadena; **solo se inserta, nunca se modifica el contenido**):

| Columna | Notas |
| --- | --- |
| `Id` | Orden de la cadena. |
| `Kind` | `Alta` / `Anulacion`. |
| `SaleId` | Venta a la que corresponde. |
| `IssuerNif`, `InvoiceNumber`, `InvoiceDate` | Identificación de la factura (`IDFactura`). |
| `InvoiceType`, `TotalTax`, `TotalAmount` | Datos que entran en la huella. |
| `GeneratedAt` | `DateTimeOffset`, exactamente el valor de `FechaHoraHusoGenRegistro`. |
| `PreviousRecordId`, `PreviousFingerprint` | Encadenamiento. |
| `Fingerprint` | Huella de este registro. |
| `IsCorrection`, `PreviousRejection` | `Subsanacion` / `RechazoPrevio` (`N`, `S`, `X`). |
| `Xml` | El `RegistroAlta`/`RegistroAnulacion` tal como se envía. |
| `QrUrl` | URL del QR, para reimprimir el ticket igual. |

**`VerifactuSubmissions`** (estado del envío; esta sí se actualiza):

| Columna | Notas |
| --- | --- |
| `RecordId` | Registro enviado. |
| `Status` | `Pendiente`, `Enviando`, `Correcto`, `AceptadoConErrores`, `Rechazado`, `ErrorTecnico`. |
| `Attempts`, `LastAttemptAt` | Reintentos. |
| `Csv`, `SubmittedAt` | Del `DatosPresentacion` de la respuesta. |
| `ErrorCode`, `ErrorDescription` | De `CodigoErrorRegistro` / `DescripcionErrorRegistro`. |
| `ResponseXml` | Respuesta completa, para auditoría. |

> Separar el registro (inmutable) del estado de envío (cambiante) deja claro qué no se puede tocar.

---

## 7. Plan de implementación por fases

Cada fase termina con algo que se puede probar.

### Fase 0 — Requisitos previos

> **Estado (21-10-2026)**: hechos los puntos 1 a 3. Queda pendiente el punto 4 (certificado) y, de cara a producción,
> crear la base en la nube y cifrar la cadena de conexión con DPAPI. No hay que pasar datos de `museo.db`: la app
> todavía no estaba en uso en el museo.

1. Resolver las decisiones del apartado 2 (sobre todo D1, D3, D4 y D6) y anotarlas en este documento. **Hecho**
   (ver la nota del apartado 2). Asistente de primera instalación con los datos del titular (`FirstRunWizard`,
   tabla `Issuers`, validación del NIF en `TaxId`) y pantalla Admin «Datos del titular» para cambiarlos después.
2. **Pasar a PostgreSQL y a migraciones EF** (D6, apartado 2.1):
   - Crear la base gestionada en la nube (región España/UE, firewall con la IP del museo, TLS) y una base PostgreSQL
     local en Docker para desarrollo.
   - Cambiar el proveedor en `AddData` (`UseNpgsql`). La cadena de conexión viene de la configuración de Desktop.
   - Añadir `IDesignTimeDbContextFactory`.
   - Ejecutar `dotnet ef migrations add Inicial --project src/QuickInventory.Data --startup-project src/QuickInventory.Data`.
     (El proyecto de inicio es Data porque Desktop no se puede usar desde Linux.)
   - Quitar `EnsureCreated()`. Las migraciones se aplican desde desarrollo, con el usuario propietario del esquema, y
     la app solo comprueba la versión al arrancar.
   - Crear el usuario de la app con permisos mínimos (`deploy/postgres/usuario-app.sql`).
   - ~~Pasar los datos de inventario de los `museo.db` actuales.~~ No hace falta: no había datos reales.
   - Bloqueo de la app sin conexión (`IConnectionStatus` y pantalla de bloqueo, ver 2.1).

   A partir de aquí **no se vuelve a borrar ni recrear la base**.
3. Crear un proyecto de tests (xUnit) y añadirlo a `QuickInventory.slnx`. **Hecho**: `tests/QuickInventory.Core.Tests`,
   con los tests del NIF. `tests/QuickInventory.Verifactu.Tests` se crea en la fase 1, junto con la librería.
4. Pedir el certificado (D7). Instalarlo en el equipo de desarrollo para el **entorno de pruebas**: el de pruebas de
   la AEAT admite certificados reales y también los de prueba que publica la AEAT.

**Hecho cuando**: dos equipos con la app ven los mismos datos de la base en la nube, el esquema sale de las
migraciones y `dotnet test` se ejecuta.

### Fase 1 — Librería `QuickInventory.Verifactu` (sin tocar la app)

> **Estado (23-10-2026)**: hechos los puntos 1 a 10, con sus tests (`tests/QuickInventory.Verifactu.Tests`, 96 tests).
> Falta el punto 11: la prueba real está escrita (`AeatTestEnvironmentTests`), pero necesita el certificado (D7).
> Para ejecutarla, definir estas variables de entorno y lanzar `dotnet test tests/QuickInventory.Verifactu.Tests`:
>
> | Variable | Valor |
> | --- | --- |
> | `VERIFACTU_PRUEBAS_CERTIFICADO` | Ruta del certificado `.pfx`, con su clave privada. |
> | `VERIFACTU_PRUEBAS_CLAVE` | Contraseña del `.pfx`. |
> | `VERIFACTU_PRUEBAS_NIF`, `VERIFACTU_PRUEBAS_NOMBRE` | Obligado tributario: el titular del certificado o alguien a quien representa. |
> | `VERIFACTU_PRUEBAS_SELLO` | `1` si es un certificado de sello de entidad (usa otros servidores). |
>
> Cómo ha quedado:
>
> - Carpetas: `Modelos/`, `Formato/` (`VerifactuFormat`, `SpainTime`), `Huella/`, `Xml/` (`RecordXmlBuilder`,
>   `SoapMessages`, `AeatSchemas`), `Validacion/`, `Qr/`, `Cliente/` (`AeatClient`, `ResponseParser`,
>   `ErrorCatalog`) y `Esquemas/`.
> - El XML se genera con `XElement` a mano. Cada registro se valida contra el XSD antes de devolverlo.
> - Solo genera `F2` y `R5` (por diferencias o por sustitución), con IVA en régimen general o exento; el resto de
>   tipos se rechaza al validar. No hay destinatarios ni `Incidencia` (el modo sin conexión está fuera de alcance).
> - Diferencia admitida entre la cuota y base × tipo: 1 céntimo. La AEAT admite hasta 10 €, pero un descuadre
>   mayor indicaría un fallo de cálculo. En la fase 2 el desglose se calcula por tipo de IVA sobre el total.
> - La consulta es por mes (`QueryAsync` por páginas o `QueryMonthAsync` con todas).
> - `AeatClient` no reintenta ni espera: lo hará el envío en segundo plano de la fase 3.
> - Es genérica: no lleva nada propio del museo. La hora puede ser la peninsular o la de Canarias
>   (`SpainTime.Now(SpanishTimeZone...)`) y `SystemInfo` indica si el sistema factura por varios obligados
>   tributarios (`TipoUsoPosibleMultiOT`, `IndicadorMultiplesOT`; por defecto no).
> - `xmldsig-core-schema.xsd` es una copia local sin la declaración de DTD del original (no usa sus entidades), para
>   no tener que procesar DTD.

1. Crear el proyecto (`net10.0`, sin dependencias de la solución) con los XSD/WSDL como recursos incrustados.
   - Descargar también `xmldsig-core-schema.xsd`, porque `SuministroInformacion.xsd` lo importa desde una URL del W3C.
2. **Modelos propios** de entrada: emisor, sistema informático, datos de factura (número, fecha, tipo, descripción,
   líneas de desglose, totales), datos de anulación, registro anterior (o «primer registro»), marcas de subsanación.
3. **Formateo** centralizado:
   - Importes: `InvariantCulture`, punto decimal, sin ceros a la izquierda, 2 decimales.
   - Fechas: `dd-MM-yyyy`.
   - Fecha y hora con huso: `yyyy-MM-ddTHH:mm:sszzz`.
   - Recorte de espacios en los textos.
   - Validación de `NumSerieFactura` (ASCII 32-126, sin `" ' < > =`).
4. **`FingerprintCalculator`**: cadena `campo=valor&…` → UTF-8 → SHA-256 → hex en mayúsculas.
   **Tests con los 3 vectores del apartado 3.2.**
5. **`RecordXmlBuilder`**: genera `RegistroAlta` y `RegistroAnulacion` con los *namespaces* exactos
   (`…/tike/cont/ws/SuministroInformacion.xsd`, `…/SuministroLR.xsd`). Se escribe con `XmlWriter` o `XDocument` a
   mano, o se generan las clases desde los XSD con `XmlSchemaClassGenerator` (`dotnet xscgen`); lo segundo cubre
   el esquema completo pero da clases muy verbosas. Para el alcance del museo (F2/R5, sin destinatarios), el
   `XmlWriter` a mano es más legible.
   **Tests**: el XML generado valida contra los XSD (`XmlSchemaSet`), y la huella del XML coincide con la calculada.
6. **Validaciones locales** antes de generar, para que la AEAT no rechace (apartado 3.1):
   - `F2` sin destinatarios y con límite de 3.000 €.
   - Cuota = base × tipo.
   - Tipos de IVA permitidos.
   - Exención sin tipo ni cuota.
   - Máximo 12 líneas de desglose.
   - Fecha de expedición no futura.

   Lanzan una excepción propia de la librería; `Data` la traduce a `BusinessRuleException`.
7. **`QrUrlBuilder`**: URL de cotejo por entorno, con `Uri.EscapeDataString` en cada valor. Test con el ejemplo del
   documento (`12345678&G33` → `12345678%26G33`).
8. **`AeatClient`**:
   - `HttpClient` con `HttpClientHandler.ClientCertificates` (certificado X509 con clave privada).
   - Sobre SOAP 1.1 (`Content-Type: text/xml; charset=utf-8`, `SOAPAction: ""`) con `Cabecera` + hasta 1.000 `RegistroFactura`.
9. **`ResponseParser`**:
   - Lee `RespuestaRegFactuSistemaFacturacion` (CSV, `DatosPresentacion`, `TiempoEsperaEnvio`, `EstadoEnvio` y cada `RespuestaLinea` con `RegistroDuplicado`).
   - Distingue `Fault` `Client` de `Server`.
   - Mapea los códigos con `errores.properties` (incrustado). El original de la AEAT está en ISO-8859-1; la copia
     incrustada está pasada a UTF-8 y sus cabeceras renombradas como `AeatErrorKind`. Si la AEAT publica una versión
     nueva, hay que repetir ambas cosas.
   - **Tests** con respuestas de ejemplo: correcta, parcialmente correcta, duplicado y `Fault`.
10. **Consulta** (`ConsultaFactuSistemaFacturacion`) por ejercicio y periodo, con paginación por `ClavePaginacion`.
    Sirve para conciliar después de un corte y para la pantalla de comprobación.
11. **Prueba real** contra el entorno de pruebas de la AEAT con una pequeña consola o test de integración
    (desactivado por defecto): enviar 2 altas encadenadas y 1 anulación, y consultarlas.

**Hecho cuando**: los tests pasan y la AEAT de pruebas acepta como `Correcto` los registros generados por la librería.

### Fase 2 — Facturar en la aplicación (Core + Data, sin enviar aún)

1. **IVA en productos** (D3 y D4): `TaxRate`/`TaxExemption` en `Product`, para **todos** los productos, entradas
   incluidas. Campo obligatorio en `ProductDialog` (desplegable con los tipos permitidos y «Exento», con el motivo
   de la exención), copia en `SalesLine` al vender. Los productos que ya existan se quedan sin IVA hasta que Admin lo
   indique, y no se pueden vender hasta entonces.
2. **Series y numeración**: tabla `InvoiceSeries`, una serie por año, común a Taquilla y Tienda. Con la base
   compartida (D6) no hace falta una serie por equipo ni por punto de venta. Asignar número en `RegisterTicketSaleAsync`/`RegisterShopSaleAsync` dentro de
   la transacción, con el bloqueo de la cadena tomado.
3. **Cálculo del desglose**: agrupar las líneas por tipo. Base = total con IVA / (1 + tipo/100) y
   cuota = total − base, redondeando a céntimos **por tipo**, no por línea, y comprobando que base + cuota = total
   exacto. Tests con casos de redondeo.
4. **Generación del registro** en la misma transacción que la venta (transacción explícita, como en `RefundService`):
   - Tomar el bloqueo de la cadena (`pg_advisory_xact_lock`).
   - Leer el último `VerifactuRecord`.
   - Construir el alta con la librería.
   - Guardar `VerifactuRecord` + `VerifactuSubmission(Pendiente)`.
   - Si las entradas no se facturan (D3), las ventas de Taquilla no generan registro.
5. **Anular y devolver**:
   - `CancelSaleAsync` pasa a ser «Anular factura (emitida por error)»: genera `RegistroAnulacion` y mantiene el
     comportamiento actual de stock.
   - Nueva operación «Devolución»: genera una venta rectificativa `R5` por diferencias (importes negativos) que
     referencia la original y devuelve el stock.
   - En la UI, dos botones con explicación clara para el personal.
6. `ChangePaymentMethodAsync` puede seguir igual: el medio de pago no forma parte del registro.
7. **Datos del sistema**: tabla `VerifactuSettings` (el emisor ya está en `Issuers`). `NumeroInstalacion` se genera una vez (un GUID
   corto) al crear la base y no cambia. Es el mismo para todos los equipos, porque comparten la base (D6).
8. Actualizar `docs/base-de-datos.md` y `CLAUDE.md`, con una convención nueva: *«Los registros de facturación no se modifican ni se borran nunca»*.

**Hecho cuando**: cada venta tiene número de factura y registro encadenado, y un test recorre la cadena de la base
recalculando las huellas sin encontrar diferencias. Otro test lanza ventas en paralelo desde varias conexiones y
comprueba que la cadena y la numeración siguen sin huecos ni repetidos.

### Fase 3 — Envío a la AEAT

1. **`VerifactuSender`** (en Data): bucle en segundo plano que:
   - Solo envía si tiene el turno de envío (bloqueo o *lease* en `VerifactuSettings`, ver 2.1). Los demás equipos esperan.
   - Espera hasta `NextSendAt`, o hasta que haya 1.000 pendientes.
   - Toma los pendientes **en orden de cadena** (máx. 1.000) y los marca `Enviando`.
   - Envía. Por cada `RespuestaLinea`:
     - `Correcto` → guardar CSV.
     - `AceptadoConErrores` → guardar y avisar.
     - `Incorrecto` → `Rechazado`.
     - **Duplicado** → tomar el estado de `RegistroDuplicado`.
   - `Fault Server`, *timeout* o sin red → volver a `Pendiente` con espera creciente (1, 2, 5, 10 min… máx. 1 h).
   - `Fault Client` → `ErrorTecnico`, no reintentar solo, avisar a Admin.
   - Guarda `TiempoEsperaEnvio` → `NextSendAt`.
   - Si al coger el turno hay registros en `Enviando` (el equipo anterior se cerró a mitad de un envío), vuelven a
     `Pendiente`; si ya estaban en la AEAT, la respuesta los marcará como duplicados.
2. Arranque desde `App.OnStartup` (Desktop) con `CancellationToken` que se cancela al cerrar. Interfaz pensada para
   que la futura versión web lo registre como `IHostedService`.
3. **Certificado** (Desktop): elegirlo del almacén de Windows (`X509Store`, CurrentUser\My) y guardar solo la
   huella digital (*thumbprint*). La clave privada no sale del almacén. No es código de UI: va detrás de una
   interfaz en Core (p. ej. `ICertificateProvider`) implementada en Desktop.
4. **Cabecera `RemisionVoluntaria/Incidencia=S`** cuando se envían registros generados durante una caída
   prolongada (sin conexión). Confirmar en la Orden cuándo es exigible.

**Hecho cuando**: en el entorno de pruebas, las ventas hechas desde varios equipos llegan a la AEAT como `Correcto`
sin intervención y sin duplicados; si la AEAT no responde, las ventas siguen funcionando y se envían al volver; y
si se apaga el equipo que envía, otro toma el relevo.

### Fase 4 — Ticket / factura simplificada con QR

1. **Contenido mínimo** de la factura simplificada (art. 7 RD 1619/2012):
   - Número y serie, fecha de expedición.
   - NIF y nombre del museo.
   - Descripción de lo vendido.
   - Tipo de IVA (y «IVA incluido») o mención de la exención.
   - Importe total.

   Además, el **QR** al principio con «QR tributario:» encima y «VERI\*FACTU» debajo.
2. **Generar el QR**: librería `QRCoder` (MIT, .NET puro) en la UI, nivel M, a SVG para que se imprima nítido.
   La URL viene ya hecha del registro (`QrUrl`).
3. **Imprimir**: componente Blazor `InvoiceTicket.razor` con CSS de impresión (`@page { size: 80mm auto; }` para
   impresora térmica, o A4) y `window.print()`. Funciona en WebView2 y en la futura web.
   - Comprobar en la impresora real que el QR mide entre 30 y 40 mm.
   - Si se quiere imprimir sin diálogo, hace falta una implementación Windows en Desktop detrás de una interfaz (como `IFileSaver`).
4. Botón «Reimprimir ticket» en el resumen de ventas (mismo número, mismo QR).

**Hecho cuando**: el QR impreso, escaneado con el móvil, abre la página de la AEAT de pruebas y dice que la
factura consta como recibida.

### Fase 5 — Pantalla de administración «VERI\*FACTU» (perfil Admin)

- **Configuración**: datos del museo, certificado (seleccionar y probar), entorno (pruebas/producción, con
  confirmación), activar el envío.
- **Estado** (con aviso también en la pantalla de inicio de Admin si hay problemas):
  - Pendientes de enviar.
  - Último envío correcto.
  - Rechazados y aceptados con errores, con el texto de la AEAT.
- **Subsanar** un registro rechazado o aceptado con errores: genera una nueva alta (`Subsanacion=S`,
  `RechazoPrevio=X` o `N`, según el caso del apartado 3.4) con los datos corregidos. El registro original no se toca.
- **Comprobar con la AEAT**: consulta por mes y comparación con los registros locales (lo que falta en la AEAT, lo que sobra, los estados).
- **Verificar la cadena**: recalcular todas las huellas locales y comprobar el encadenamiento.
- Exportación a Excel de los registros del mes, aprovechando `SalesExport`.

### Fase 6 — Puesta en producción

1. **Declaración responsable** del sistema (modelo en `EjemplosDeclaracionResponsable(V0.5.1).pdf`): nombre,
   código (`IdSistemaInformatico`), versión, componentes, «solo VERI\*FACTU = S», «multi OT = N», productor (D8).
   Accesible desde la propia aplicación (pantalla «Acerca de»).
2. Pruebas completas en el entorno de pruebas de la AEAT durante un periodo de uso real (por ejemplo, una semana).
3. Cambiar a producción con la base de datos limpia de registros de prueba: la cadena de producción empieza con
   `PrimerRegistro=S`. Los registros de pruebas **no** se pueden mezclar con los de producción.
4. **Copias de seguridad**: las automáticas del servicio en la nube y, además, una exportación periódica a
   almacenamiento inmutable con retención larga (ver 2.1). La base pasa a contener registros que hay que conservar
   durante el plazo de prescripción. Probar una restauración antes de pasar a producción.
5. Formación del personal: anular frente a devolver, qué hacer si sale el aviso de envíos pendientes.

---

## 8. Riesgos y puntos delicados

| Riesgo | Mitigación |
| --- | --- |
| Romper la cadena (dos registros con el mismo «anterior»). | Generación dentro de la transacción de la venta; índice único en `PreviousRecordId`; test de verificación de cadena. |
| Huella distinta de la de la AEAT por formatos (decimales, huso, espacios). | Un único formateador para XML y huella; tests con los vectores oficiales; comparar con el XML guardado. |
| Hora del PC mal puesta. | La AEAT avisa si `FechaHoraHusoGenRegistro` es futura. Mostrar aviso si el reloj difiere mucho de la hora del servidor (cabecera `Date` de la respuesta). |
| Cambio de horario verano/invierno. | Usar siempre `DateTimeOffset.Now` (lleva el huso correcto en cada momento); nunca construir el huso a mano. |
| Pérdida o corrupción de la base. | Copias del servicio + exportación inmutable de retención larga; la consulta a la AEAT permite reconstruir qué se envió, pero **no** el CSV. |
| Sin internet en el museo no se puede cobrar (base en la nube). | Se asume conexión permanente; si se pierde, la app se bloquea hasta que vuelva (2.1). Preguntar a la asesoría el procedimiento mientras tanto. Si los cortes resultan frecuentes: router 4G/5G de respaldo o funcionamiento sin conexión (apartado 9). |
| Dos equipos generan registros a la vez. | Bloqueo `pg_advisory_xact_lock` en la transacción de la venta + índices únicos; test de ventas en paralelo. |
| Dos equipos envían a la AEAT a la vez. | Turno de envío (bloqueo o *lease*) compartido en la base; `NextSendAt` común. |
| Credenciales de la base en cada equipo. | Firewall por IP del museo, TLS, usuario con permisos mínimos (sin `UPDATE`/`DELETE` en `VerifactuRecords`), cadena de conexión cifrada con DPAPI. |
| Certificado caducado. | Avisar en la pantalla Admin 30 días antes de la caducidad. |
| Borrar o recrear la base como en desarrollo. | Fase 0 (migraciones) obligatoria antes de producción; el usuario de la app no puede cambiar el esquema; documentarlo en `CLAUDE.md`. |
| Cambios de la AEAT en esquemas o validaciones (el documento de validaciones va por la v1.2.2). | XSD incrustados con su versión; revisar el portal de desarrolladores antes de cada versión de la app. |

---

## 9. Fuera de alcance (posibles ampliaciones)

- **Facturas completas `F1`** con datos del cliente y **`F3`** (sustitución de simplificadas): diálogo para pedir
  NIF/nombre, validación de NIF y bloque `Destinatarios`. Probablemente necesario para colegios y agencias (D5).
- **Modo «No VERI\*FACTU»**: firma XAdES-EPES con política AGE (`urn:oid:2.16.724.1.3.1.1.2.1.9`), registro de
  eventos (`EventosSIF.xsd`), detección de anomalías, exportación y remisión bajo requerimiento. Bastante más
  trabajo y sin ventaja clara para el museo.
- **Facturas de la tienda online**: dependen de PrestaShop (D9).
- **Funcionamiento sin conexión** (si los cortes de internet resultan frecuentes). Cada equipo guarda sus ventas en
  una base local y las sube a la nube en lotes cuando hay conexión. Condiciones:
  - **La numeración y la cadena no pueden asignarse en la nube**. El ticket necesita número y QR en el momento de la
    venta, y el registro se genera al expedir la factura. Por eso cada equipo tiene que ser una **instalación
    distinta**, con su propia serie (`T1-2027`, `S1-2027`…), su `NumeroInstalacion` y su cadena en la base local. Se
    elimina el bloqueo entre equipos.
  - Ventas, líneas, movimientos de stock y registros solo se añaden, así que la sincronización no tiene conflictos.
    Basta con que reenviar un lote no duplique nada (clave única por venta). Hacen falta GUID, o una pareja
    (equipo, id local), en las tablas que se sincronizan.
  - Catálogo, precios y usuarios bajan de la nube como copia de solo lectura. Editarlos, y anular, devolver o cambiar
    el medio de pago de ventas de otro equipo, solo con conexión.
  - Con varios equipos sin conexión puede quedar stock negativo al sincronizar.
  - Cada equipo envía su propia cadena a la AEAT, con `Incidencia=S` si envía tras un corte (error `2004`).
  - **Comprobar** que la AEAT admite varias instalaciones con el mismo NIF e `IdSistemaInformatico`, cada una con su
    `PrimerRegistro=S` (error `2007`).
  - Esfuerzo orientativo: 1-2 semanas más (dos esquemas, SQLite y PostgreSQL, y la sincronización).

---

## 10. Estimación orientativa

| Fase | Contenido | Esfuerzo |
| --- | --- | --- |
| 0 | PostgreSQL en la nube, migraciones, paso de datos, proyecto de tests, certificado | 3-5 días |
| 1 | Librería Verifactu + pruebas reales con la AEAT | 5-8 días |
| 2 | IVA, numeración, registro en la venta, anular/devolver | 4-6 días |
| 3 | Envío en segundo plano, reintentos, certificado | 3-4 días |
| 4 | Ticket imprimible con QR | 2-3 días |
| 5 | Pantalla Admin (estado, subsanación, consulta, verificación) | 3-5 días |
| 6 | Declaración responsable, pruebas en uso real, paso a producción | 2-3 días + periodo de prueba |

Total aproximado: **4-6 semanas** de una persona, sin contar el tiempo de respuesta de la asesoría fiscal ni la
obtención del certificado.
