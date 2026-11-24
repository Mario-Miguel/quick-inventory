# Plantillas de factura a clientes

Cómo se generan, se ven, se imprimen y se pasan a PDF las facturas a clientes (colegios y organizaciones), y
cómo se podría vender como módulo aparte.

Introducido en el commit `1e6f7bd` («added invoices for clients, added invoice personalization»). Desde entonces vive en la
librería reutilizable **Invoicing** (`src/Invoicing`, `src/Invoicing.EntityFrameworkCore`, `src/Invoicing.UI`; ver
`src/Invoicing/README.md`), con los nombres del código en inglés.

---

## 1. Resumen en una frase

La factura es **una plantilla HTML con marcadores `{{…}}`** que un pequeño motor propio de la librería
(`Invoicing.Templates.InvoiceTemplate`) rellena con los datos de la factura. El HTML resultante se **pinta dentro de la propia
pantalla Blazor** (`InvoiceDocument.razor`) y, para imprimir o sacar el PDF, se **imprime la página de
WebView2** con una hoja de estilos `@media print` que oculta todo menos la factura. No se usa ninguna librería
de PDF ni se genera nada en un servidor: el PDF lo genera el motor de Chromium de WebView2.

```
 BD (PostgreSQL)                  Invoicing                     Invoicing.UI (Blazor)        Desktop (WPF)
┌───────────────────┐   ┌──────────────────────────┐   ┌──────────────────────────┐   ┌──────────────────────────┐
│ CustomerInvoice   │──▶│ InvoiceTemplate.Render() │──▶│ InvoiceDocument.razor    │   │ DesktopPagePrinter       │
│ InvoiceDesign     │   │  plantilla + datos → HTML│   │  (MarkupString) en       │──▶│  ShowPrintUI   (imprimir)│
│ (logo, colores,   │   │  escapado + saneado      │   │  .invoice-document       │   │  PrintToPdfStreamAsync   │
│  pie, plantilla)  │   └──────────────────────────┘   │  + @media print (A4)     │   │  (PDF → IFileSaver)      │
└───────────────────┘            ▲                     └──────────────────────────┘   └──────────────────────────┘
                                 │ recurso incrustado
                      Templates/LogoLeft.html, LogoCentered.html, Compact.html
```

---

## 2. Piezas y dónde están

| Pieza | Archivo | Proyecto | Qué hace |
|---|---|---|---|
| Diseño | `Models/InvoiceDesign.cs` | Invoicing | Una sola fila en la BD: disposición, colores, pie, logotipo, plantilla importada. Validación. |
| Motor de plantillas | `Templates/InvoiceTemplate.cs` | Invoicing | Sustituye marcadores, repite bloques, escapa, sanea. |
| Plantillas de serie | `Templates/*.html` | Invoicing | `LogoLeft`, `LogoCentered`, `Compact`. Recursos incrustados (`EmbeddedResource` en `Invoicing.csproj`). |
| Servicio | `Services/IInvoiceDesignService.cs` / `Services/InvoiceDesignService.cs` | Invoicing / Invoicing.EntityFrameworkCore | Leer y guardar el diseño. |
| Componente | `Components/InvoiceDocument.razor` | Invoicing.UI | Pinta el HTML de la factura dentro de `<div class="invoice-document">`. |
| Pantalla de la factura | `Pages/CustomerInvoiceView.razor` (`/customer-invoices/{id}`) | Invoicing.UI | Muestra la factura; botones «Imprimir» y «Guardar en PDF». |
| Pantalla de diseño | `Pages/InvoiceDesignSettings.razor` (`/invoice-design`) | Invoicing.UI | Logo, colores, disposición, pie, importar/descargar plantilla, vista previa en vivo. |
| Estilos de impresión | `wwwroot/invoicing.css` (`.invoice-document`, `@media print`, `.invoice-preview`) | Invoicing.UI | Hoja A4, imprimir solo la factura, vista previa al 60 %. |
| Impresión / PDF (abstracción) | `IPagePrinter.cs` | Invoicing.UI | `PrintAsync()` y `ToPdfAsync()`. |
| Impresión / PDF (escritorio) | `DesktopPagePrinter.cs` | Desktop | Implementación con WebView2. Registrada en `App.xaml.cs`. |
| Tests | `tests/Invoicing.Tests/InvoiceTemplateTests.cs` | Tests | Marcadores, alias en español, bloques, secciones, colores, saneado, plantillas de serie completas. |

La librería no conoce QuickInventory, y todo lo específico de Windows (WebView2) está detrás de `IPagePrinter`,
implementado en Desktop. La futura versión web (u otra aplicación) podría reutilizar `Invoicing.UI` tal cual con
otra implementación de `IPagePrinter`.

---

## 3. Modelo de datos: `InvoiceDesign`

Tabla `InvoiceDesigns`, **una sola fila** común a todos los equipos (si no existe, `GetDesignAsync` devuelve
uno por defecto; `SaveDesignAsync` la crea la primera vez).

| Propiedad | Tipo | Reglas |
|---|---|---|
| `Layout` | `InvoiceLayout` (enum como texto) | `LogoLeft` (defecto), `LogoCentered`, `Compact`. |
| `BackgroundColor` | `string` | Obligatorio, exactamente `#RRGGBB`. Defecto `#FFFFFF`. |
| `AccentColor` | `string?` | `#RRGGBB` o `null` = `InvoicingUiOptions.DefaultAccentColor` (en QuickInventory, el color `Primary` del `client.json`). |
| `FooterText` | `string?` | Máx. 500 caracteres (p. ej. el IBAN). |
| `Logo` / `LogoContentType` / `LogoFileName` | `byte[]?` / `string?` / `string?` | PNG o JPG, máx. 1 MB. Se guarda en la BD (`bytea`). |
| `CustomTemplate` / `CustomTemplateFileName` | `string?` | HTML importado, máx. ~200 KB. Si existe, **sustituye** a `Layout`. |

Importante: el diseño **no se copia en cada factura**. Al abrir una factura ya emitida se pinta con el diseño
actual; los *datos* (emisor, cliente, líneas, importes) sí son una instantánea guardada en `CustomerInvoice`,
así que cambiar el diseño solo cambia el aspecto. La pantalla de diseño lo avisa.

---

## 4. El motor de plantillas (`InvoiceTemplate`)

Motor propio, sin dependencias, de sintaxis parecida a Mustache. Todo con expresiones regulares generadas
(`[GeneratedRegex]`).

### 4.1 Qué plantilla se usa

```csharp
InvoiceTemplate.For(design) => design.CustomTemplate ?? Builtin(design.Layout);
```

`Builtin` lee el recurso incrustado `Invoicing.Templates.<Layout>.html`. Las disposiciones de
serie **son plantillas como cualquier otra**: una plantilla importada funciona exactamente igual, y lo normal
es descargar la actual y modificarla.

### 4.2 Sintaxis

| Sintaxis | Significado |
|---|---|
| `{{nombre}}` | Se cambia por el dato (escapado). Si no existe o está vacío, queda en blanco. |
| `{{#lines}}…{{/lines}}` | Se repite por cada línea de la factura. Dentro valen `{{description}}`, `{{quantity}}`, `{{price}}`, `{{tax_rate}}`, `{{amount}}` y también los datos generales. |
| `{{#taxes}}…{{/taxes}}` | Se repite por cada tipo de IVA (`CustomerInvoice.TaxBreakdown()`). Dentro: `{{rate}}`, `{{base}}`, `{{tax_amount}}`, `{{exempt}}`. |
| `{{#dato}}…{{/dato}}` | Solo sale si el dato no está vacío (p. ej. `{{#logo}}<img src="{{logo}}">{{/logo}}`). |
| `{{^dato}}…{{/dato}}` | Solo sale si el dato está vacío (p. ej. `{{^exempt}}` para no pintar la cuota en exentas). |

Las secciones se resuelven en bucle, así que se pueden anidar.

### 4.3 Marcadores disponibles

Lista única en `InvoiceTemplate.Placeholders` (la pantalla de diseño la enseña):

`number`, `date`, `issuer.name`, `issuer.nif`, `issuer.address`, `customer.name`, `customer.nif`,
`customer.address`, `base`, `tax`, `total`, `notes`, `footer`, `logo`, `color.background`, `color.accent`,
`#lines`, `#taxes`.

**Nombres antiguos en español.** Las primeras versiones usaban nombres en español, y puede haber plantillas
importadas con ellos en la base de datos. Siguen valiendo como alias (`InvoiceTemplate.Aliases`), también en
`{{#…}}`, `{{^…}}` y `{{/…}}`. Antes de rellenar la plantilla se cambian por el nombre nuevo:

| Antiguo | Nuevo | Antiguo | Nuevo |
|---|---|---|---|
| `numero` | `number` | `lineas` | `lines` |
| `fecha` | `date` | `concepto` | `description` |
| `emisor.nombre` / `.nif` / `.direccion` | `issuer.name` / `.nif` / `.address` | `cantidad` | `quantity` |
| `cliente.nombre` / `.nif` / `.direccion` | `customer.name` / `.nif` / `.address` | `precio` | `price` |
| `iva` | `tax` | `tipo_iva` | `tax_rate` |
| `observaciones` | `notes` | `importe` | `amount` |
| `pie` | `footer` | `impuestos` | `taxes` |
| `color.fondo` | `color.background` | `tipo` | `rate` |
| `color.acento` | `color.accent` | `cuota` | `tax_amount` |
| | | `exenta` | `exempt` |

`base`, `total` y `logo` se llaman igual en los dos.

Formatos: importes con `ToString("C")` en cultura `es-ES` (`1.234,56 €`), fecha `dd/MM/yyyy`. El logotipo va
como *data URI* (`data:image/png;base64,…`), así que la plantilla no depende de archivos externos.

### 4.4 Orden del renderizado (`Render`)

1. Se calculan los valores generales (todos pasan por `Encode`).
2. `ApplyAliases(template)` (nombres antiguos → nuevos) y `Sanitize`.
3. Se expande `{{#lines}}` y luego `{{#taxes}}` (cada repetición con sus valores + los generales).
4. `Fill`: secciones condicionales y después marcadores sueltos.

### 4.5 Seguridad

- **Escapado**: todo texto pasa por `Encode` (`& < > " '`) y los saltos de línea se convierten en `<br>`. Un
  cliente llamado `<b>Colegio</b>` no rompe la factura.
- **Colores**: solo se aceptan `#RRGGBB` (validado al guardar y otra vez al pintar), porque van dentro del
  `<style>`; no hay forma de inyectar CSS.
- **Saneado de la plantilla**: se quitan `<script>`, `iframe/frame/frameset/object/embed/applet/link/meta/base/form`,
  atributos `on…=` y `javascript:`. Es una red de seguridad (solo Admin puede importar), no un saneador HTML
  completo.
- **Marcadores desconocidos**: `UnknownPlaceholders` avisa al importar de los que están mal escritos.
- Si la plantilla falla al renderizar, `InvoiceDocument` muestra un mensaje en lugar de tumbar la pantalla.

### 4.6 Convenciones de las plantillas de serie

- Todos los estilos dentro de `<style>` y **prefijados con `.invoice`** (antes `.factura`; las plantillas importadas llevan su propio CSS, así que siguen funcionando), para no afectar al resto de la app (la
  factura se pinta dentro de la misma página que MudBlazor).
- `.invoice` mide `min-height: 296mm` con `display:flex; flex-direction:column`, y el pie usa
  `margin-top:auto`, así queda abajo de la hoja.
- Los márgenes los pone la propia plantilla (`padding: 16mm 14mm`); la impresión va con márgenes 0.

---

## 5. Visualización en el front

`InvoiceDocument.razor`:

```razor
<div class="invoice-document">@((MarkupString)html)</div>
```

En `OnParametersSet` llama a `InvoiceTemplate.Render(Template ?? For(Design), Invoice, Design, Options.DefaultAccentColor)`.
Se usa en dos sitios:

- **`/customer-invoices/{id}`**: la factura real, a tamaño A4 (`.invoice-document { width: 210mm }`).
- **`/invoice-design`**: vista previa **en vivo** con una factura de ejemplo (datos reales del titular,
  un colegio ficticio, una línea con IVA 21 % y otra exenta), escalada con `.invoice-preview { zoom: 0.6 }`.
  Cualquier cambio (color, logo, plantilla importada) se ve antes de pulsar «Guardar diseño».

Como el HTML se inserta en el DOM de Blazor (no en un `iframe`), lo que hay en la plantilla convive con la app:
de ahí el prefijo `.invoice` en los estilos y el saneado.

---

## 6. Impresión y PDF

### 6.1 CSS de impresión (`invoicing.css`)

```css
@media print {
  @page { size: A4; margin: 0; }
  body * { visibility: hidden; }
  .invoice-document, .invoice-document * { visibility: visible; }
  html, body, .mud-layout, .mud-main-content { height: auto !important; overflow: visible !important; }
  .invoice-document { position: absolute; left: 0; top: 0; width: 210mm; box-shadow: none; overflow: visible; }
}
```

Además `.invoice-document` lleva `print-color-adjust: exact` para que se impriman fondos y colores.

### 6.2 `IPagePrinter` → `DesktopPagePrinter` (WebView2)

| Botón | Método | Implementación |
|---|---|---|
| Imprimir | `PrintAsync()` | `CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System)`: diálogo de impresión de Windows (también permite «Microsoft Print to PDF»). No se usa `window.print()` porque abre la vista previa de Edge dentro de la ventana y la app puede quedarse en negro al cerrarla. |
| Guardar en PDF | `ToPdfAsync()` | `CoreWebView2.PrintToPdfStreamAsync(settings)` con márgenes 0, fondos activados y sin cabecera/pie del navegador. Los bytes se guardan con `IFileSaver` como `Factura <número>.pdf`. |

Las llamadas a WebView2 se encolan en el `Dispatcher` con prioridad `Background`, porque llegan desde un
evento de WebView2 y anidar otra operación de WebView2 ahí puede cerrar la aplicación.

### 6.3 Limitaciones de este enfoque

- **El PDF es «de la pantalla actual»**: solo se puede generar desde `/customer-invoices/{id}` con la factura
  pintada. No hay generación en segundo plano: no se pueden sacar 50 PDF de golpe, ni adjuntar el PDF a un
  correo automático, ni generarlo desde un servicio, sin abrir cada factura.
- **Depende de WebView2** (Desktop). La futura versión web tendría que implementar `IPagePrinter` con
  `window.print()` (sin `ToPdfAsync` directo) o generar el PDF en el servidor (p. ej. Chromium sin cabeza /
  Playwright, o una librería tipo QuestPDF con otro motor de maquetación).
- **Paginación**: una factura con muchas líneas pasa a una segunda hoja con el corte que decida Chromium; no hay
  cabecera repetida ni «página X de Y».
- **El diseño no se congela por factura** (ver sección 3).
- **Sin QR VERI\*FACTU todavía**: cuando se integre (`docs/verifactu.md`) habrá que añadir un marcador (p. ej.
  `{{qr}}` como *data URI*, igual que el logo) y la leyenda obligatoria, y comprobar que las plantillas
  importadas lo incluyen.

---

## 7. ¿Se puede vender como módulo aparte?

**Sí, y encaja con el mecanismo que ya existe** para `PrestaShop` y `Verifactu`: `ClientFeatures` en el
`client.json`, que va **incrustado en el ejecutable** de cada cliente. El cliente no puede activarlo sin que
le compiles una versión nueva, así que sirve como «licencia» por cliente.

### 7.1 Qué se podría cobrar

Hay dos cortes razonables (se pueden combinar):

| Opción | Qué incluye | Sin el módulo |
|---|---|---|
| **A. «Facturas a clientes» completo** | Área entera: Facturas a clientes, Clientes, Diseño de factura. | El apartado no sale en la pantalla principal y sus rutas están bloqueadas. |
| **B. «Diseño de factura personalizado»** (complemento premium) | Pantalla `/invoice-design`: logo, colores, pie y plantilla HTML propia. | Se factura igual, con la disposición `LogoLeft` y los colores del `client.json`. |

Mi recomendación: **A como módulo de pago** (es funcionalidad de negocio clara) y, si quieres un escalón
más, **B como extra** (o solo la parte de «Plantilla propia (avanzado)», que es la que implica trabajo de
soporte).

### 7.2 Cómo está hecho

**La opción A está implementada.** Hay que activarla en cada cliente con `"CustomerInvoices": true` en `Features`
de `clients/<id>/client.json`. Por defecto (sin la clave) está desactivada, igual que `PrestaShop` y `Verifactu`.

- `ClientSettings.cs`: `ClientFeatures.CustomerInvoices` y `ClientFeature.CustomerInvoices`.
- `Modules.cs`: los tres módulos del área «Facturas a clientes» llevan `Requires = ClientFeature.CustomerInvoices`.
  `Home` deja de mostrar el área, porque las áreas vacías no salen.
- `Modules.Allows` comprueba también las subrutas (`/customer-invoices/12` sigue la regla de
  `/customer-invoices`), y `MainLayout` lo comprueba en cada navegación, no solo al cambiar de perfil. Si la
  pantalla no está permitida, se vuelve al inicio.
- `InvoiceSeriesSettings.razor`: sin el módulo, no se muestra `CustomerInvoiceSeriesPanel` y el texto solo habla de tickets.

Para dejar de cobrarlo a un cliente se pone a `false` y se publica una versión nueva (el `client.json` va dentro
del exe). Las facturas ya emitidas siguen en la base de datos.

Opción B (pendiente), además:

1. Otro `ClientFeature` (p. ej. `InvoiceDesign`) como `Requires` solo del módulo «Diseño de factura».
2. La librería no conoce `ClientSettings`: añadir a `InvoicingUiOptions` una opción (p. ej.
   `AllowCustomDesign`) que `App.xaml.cs` rellena desde `ClientFeatures` en `AddInvoicingUi`. Si está apagada,
   `InvoiceDocument.razor` renderiza con `new InvoiceDesign()` (disposición y colores por defecto) en vez del
   diseño guardado. Así, si un cliente deja de pagarlo, sus facturas vuelven al diseño estándar aunque la fila
   siga en la BD.
3. Opcional: que `InvoiceDesignService.SaveDesignAsync` rechace guardar con la opción apagada
   (`BusinessRuleException`), con una opción equivalente en `Invoicing.EntityFrameworkCore`.

No hace falta migración: las tablas existen en todas las bases, aunque el módulo esté apagado.

### 7.3 Lo que hay que tener claro

- **Es ocultar, no proteger**: el código va dentro de todos los ejecutables y las tablas existen en todas las
  bases. Para clientes a los que compilas tú el exe es suficiente; no es una protección frente a alguien que
  descompile el programa. Si algún día se necesitara de verdad, ahora es más fácil: el módulo ya está en sus
  propios ensamblados (`Invoicing*`), así que se podrían dejar fuera al compilar según `-p:Client`. Habría que
  hacer opcionales la referencia, el registro (`AddInvoicingData`/`AddInvoicingUi`) y el `AdditionalAssemblies`.
- **Activar/desactivar = nueva versión**: cambiar el `client.json` implica publicar un tag
  `<cliente>/v<versión>` (ver `docs/publicacion.md`); Velopack lo distribuye solo.
- **Al desactivar**, las facturas ya emitidas siguen en la BD (no se borra nada, por obligación legal) pero no
  se podrían ver ni reimprimir desde la app. Conviene decidir si en ese caso se deja al menos la consulta e
  impresión en solo lectura.
- **VERI\*FACTU**: si el cliente emite facturas desde la app, esas facturas también tendrán que ir a la AEAT
  cuando se integre VERI\*FACTU. Si el módulo de facturas se vende aparte, el de VERI\*FACTU debe ir activado
  con él (o incluido en el precio).
