# Integración con Groupon

Plan para ver las reservas hechas en Groupon, marcarlas como consumidas y obtener las entradas vendidas, como módulo independiente al estilo de `QuickInventory.Verifactu`.

Fuente: `.internalDocs/groupon/swagger.json` (Groupon Connect APIs, equipo 3PIP).

## 1. Qué permite la API

La API funciona **al revés** de lo que cabría esperar: no sirve para consultar Groupon. Casi todos los endpoints (`/groupon/v1/...`) **los implementa el partner** (nosotros) en un servidor propio (`https://{your domain}`) y es Groupon quien los llama. Groupon solo ofrece tres endpoints que podamos llamar nosotros (`https://offer-api.groupon.com`, staging en `https://offer-api-staging.groupon.com`):

| Endpoint de Groupon | Uso |
|---|---|
| `PATCH /partners/{partner}/v1/units` | Marcar una entrada como `redeemed` (consumida) o `cancelled` |
| `PATCH /partners/{partner}/v1/reservations` | Marcar una reserva como `fulfilled` o `cancelled` |
| `PATCH /partners/{partner}/v1/bookings` | Flujo de citas: `check-in`, `no-show`, `cancelled` |

### Requisitos pedidos

| Requisito | ¿Se puede? | Cómo |
|---|---|---|
| Ver todas las reservas | Sí, pero solo las que nos lleguen | No existe un endpoint para listarlas en Groupon. Groupon nos llama al reservar, cobrar y cancelar (`POST /groupon/v1/reservations`, `.../fulfillments`, `.../cancellations`) y las guardamos en la BD. Las ventas anteriores a la integración no se recuperan. |
| Marcarlas como consumidas | Sí | `PATCH .../units` con `status: redeemed`, usando los ids de entrada que generamos nosotros al reservar. `redeemed` es definitivo. |
| Facturas | No | No hay nada sobre facturas, liquidaciones ni pagos en la API. Siguen en el Merchant Center de Groupon (descarga manual). |
| Entradas vendidas | Sí | Cada reserva trae productos, cantidades, precio y estado. Ojo: es el precio que paga el cliente, no lo que Groupon liquida al museo (descontada su comisión). |

## 2. Obstáculos

- **Servidor público 24/7.** La app de escritorio no puede recibir llamadas de Groupon. Hay que desplegar un servicio HTTPS en la nube, junto al PostgreSQL. Es infraestructura nueva para el proyecto.
- **Alta como partner 3PIP.** Esta API la suelen implementar plataformas de reservas (FareHarbor, Bókun…), no un comercio. Hay que pedir el alta (contacto del spec: `3pip@groupon.com`) y pasar una certificación en staging. Groupon da el `partner` y el `client_id`.
- **Autenticación de las llamadas entrantes sin definir.** El spec no tiene `securitySchemes`: hay que acordarla con Groupon (IP permitidas, mTLS, clave…).
- **Catálogo y disponibilidad.** Groupon consulta disponibilidad antes de vender (`/products/availability`, `/merchants/{id}/catalog`, `/feed/...`). Exige modelar aforo y horarios, que hoy la app no tiene.
- **Dos flujos posibles.** *Transactional*: Groupon vende la entrada contra nuestro inventario y se consume por unidad. *Booking*: el cliente ya tiene el bono y reserva fecha; se cierra con `check-in`. Para "entradas vendidas" encaja el Transactional, pero lo decide Groupon en el alta.
- **SLA exigentes** (TP99 de unos 35–80 ms por llamada): el servidor debe estar cerca de la BD.

**Alternativa si Groupon no admite al museo como partner directo:** canjear los bonos en el Merchant Center e importar sus exportaciones CSV a la app. Sin servidor ni certificación.

## 3. Plan

### Fase 0: validación con Groupon (bloqueante)

- [ ] Confirmar que aceptan al museo como partner 3PIP y con qué flujo (Transactional o Booking).
- [ ] Acordar la autenticación de las llamadas de Groupon hacia nuestro servidor.
- [ ] Obtener `partner`, `client_id` y acceso a staging.
- [ ] Preguntar si hay forma automatizada de obtener las liquidaciones o facturas.
- [ ] Consultar con la gestoría cómo se facturan fiscalmente las ventas por Groupon (afecta a Verifactu).

### Fase 1: librería `QuickInventory.Groupon`

Igual que `QuickInventory.Verifactu`: sin referencias a otros proyectos de la solución.

- [ ] `Models/`: DTO de entrada (reserva, cumplimiento, cancelación, disponibilidad, catálogo) y de salida (PATCH de units, reservations y bookings).
- [ ] `Format/GrouponFormat`: fechas ISO-8601 con zona y conversión desde/hacia la hora local del museo.
- [ ] Máquina de estados de las reservas según el spec: `reserved → fulfilling → fulfilled`, `→ cancelling → cancelled`. `cancelled` y `redeemed` son finales.
- [ ] `Errors.cs`: `GrouponException` y los códigos de error del spec.
- [ ] `Client/GrouponClient`: PATCH a staging o producción, con `client_id`.
- [ ] Tests en `tests/QuickInventory.Groupon.Tests`, con los ejemplos del swagger como fixtures.

### Fase 2: base de datos (Core y Data)

- [ ] `GrouponReservation`: id nuestro, id de Groupon, estado, fechas, comprador, importe.
- [ ] `GrouponUnit`: id de la entrada, código para escanear, estado, fecha de canje.
- [ ] `GrouponProductMapping`: producto de Groupon ↔ `TicketProduct`.
- [ ] `GrouponSettings`.
- [ ] Tabla de salida (*outbox*) para los avisos pendientes a Groupon (el spec obliga a reintentar).
- [ ] Migración compatible con la versión anterior.
- [ ] Actualizar `docs/base-de-datos.md`.

### Fase 3: servidor `QuickInventory.Groupon.Server`

ASP.NET Core minimal API que usa la librería y Data.

- [ ] Endpoints de partner del flujo elegido: disponibilidad, reservar, consultar/buscar, cumplir, cancelar, cancelar unidad, catálogo y heartbeat. Idempotentes y rápidos.
- [ ] Autenticación de las llamadas entrantes según lo acordado en la fase 0.
- [ ] `BackgroundService` que vacía el outbox llamando a `PATCH /units` y `PATCH /reservations`, con reintentos.
- [ ] Despliegue en la nube junto a la BD, con su propio workflow y secretos (`client_id`, conexión). El usuario de BD no puede alterar el esquema.

### Fase 4: app de escritorio (UI)

- [ ] `IGrouponService` en Core, implementación en Data:
  - listar reservas por fecha y estado;
  - buscar por código (vale el lector de códigos de barras);
  - `RedeemUnitAsync`: marca la entrada en local y deja el aviso en el outbox;
  - resumen de entradas vendidas por periodo.
- [ ] El escritorio **nunca llama a Groupon directamente**: los paquetes de Velopack son públicos y el `client_id` no puede viajar en el exe. Los avisos los envía el servidor.
- [ ] Pantalla "Groupon" en Taquilla: lista de reservas y botón grande "Marcar como usada".
- [ ] Visible solo con `ClientFeature.Groupon` (propiedad nueva en `ClientFeatures`, como PrestaShop).
- [ ] Exportar las entradas vendidas reutilizando `SalesExport`.

### Fase 5 (opcional): liquidaciones

- [ ] Importar el CSV de liquidaciones del Merchant Center como factura de proveedor (`PurchaseInvoiceService`) para cuadrar comisión e ingresos.

## 4. Por qué esta separación

- La librería, como Verifactu, solo sabe hablar con Groupon.
- La persistencia queda en Data.
- El servidor es la única pieza nueva que hay que desplegar.
- UI sigue sin referenciar Data.
