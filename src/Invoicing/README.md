# Invoicing

Librería de **facturas a clientes**: clientes, series de numeración por año, emisión de facturas completas
(F1) con desglose de IVA, diseño de la factura con plantillas HTML, impresión y PDF. Nació dentro de
QuickInventory, pero no depende de ella: se puede usar en cualquier proyecto .NET 10.

Los nombres del código están en inglés; los textos que ve el usuario (botones, avisos, errores de
validación) están en español.

## Proyectos

| Proyecto | Depende de | Qué tiene |
|---|---|---|
| `Invoicing` | nada | Modelos (`Customer`, `CustomerInvoice`, `CustomerInvoiceLine`, `CustomerInvoiceSeries`, `InvoiceDesign`), tipos fiscales (`TaxRate`, `TaxExemption`, `InvoiceType`, `TaxId`, `Contact`, `NumberingSeries`), `BusinessRuleException`, el motor de plantillas (`Templates/InvoiceTemplate`) y las interfaces de los servicios. |
| `Invoicing.EntityFrameworkCore` | `Invoicing`, EF Core (sin proveedor) | Implementación de los servicios sobre el `DbContext` de la aplicación, configuración de las tablas y conversores. Vale para cualquier base de datos relacional con EF Core. |
| `Invoicing.UI` | `Invoicing`, MudBlazor | Pantallas Blazor: `/customers`, `/customer-invoices`, `/customer-invoices/{id}`, `/invoice-design`, y el componente `CustomerInvoiceSeriesPanel`. |

Se puede usar solo `Invoicing` (con tu propia persistencia y tus pantallas), `Invoicing` + `Invoicing.EntityFrameworkCore`
(con tu propia UI) o las tres.

## Cómo usarla

### 1. Base de datos (`Invoicing.EntityFrameworkCore`)

Tu `DbContext` implementa `IInvoicingDbContext` y llama a la configuración de la librería. Así sus tablas van
en tu base y en tus migraciones:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IInvoicingDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerInvoiceSeries> CustomerInvoiceSeries => Set<CustomerInvoiceSeries>();
    public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
    public DbSet<CustomerInvoiceLine> CustomerInvoiceLines => Set<CustomerInvoiceLine>();
    public DbSet<InvoiceDesign> InvoiceDesigns => Set<InvoiceDesign>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration) =>
        configuration.AddInvoicingConventions();   // IVA, exenciones, tipo de factura… como texto

    protected override void OnModelCreating(ModelBuilder model) =>
        model.ApplyInvoicingModel();               // longitudes, índices, relaciones
}
```

Los importes (`decimal`) y las fechas se guardan como diga tu aplicación: la librería no impone
convención (QuickInventory, por ejemplo, guarda los importes como céntimos enteros).

### 2. Servicios

```csharp
services.AddDbContextFactory<AppDbContext>(o => o.UseSqlServer(...)); // o UseNpgsql, UseSqlite…
services.AddInvoicingData<AppDbContext>();

// Obligatorio: de dónde salen los datos del emisor de las facturas.
services.AddScoped<IInvoiceIssuerProvider, MyIssuerProvider>();

// Opcionales:
services.AddSingleton<IInvoiceNumberingLock, MyLock>();         // bloqueo al numerar (p. ej. pg_advisory_xact_lock)
services.AddScoped<IReservedSeriesCodes, MyReservedCodes>();    // códigos de serie que ya usas para otros documentos
```

| Punto de extensión | Para qué | Si no se registra |
|---|---|---|
| `IInvoiceIssuerProvider` | Nombre, NIF y dirección del emisor, que se copian en cada factura. | Falla al emitir (es obligatorio). |
| `IInvoiceNumberingLock` | Que dos equipos no numeren a la vez. Se llama dentro de la transacción. | No bloquea: el índice único (serie, número) impide los repetidos y el segundo en guardar recibe un error. |
| `IReservedSeriesCodes` | Que una serie de facturas no repita un código de otra serie tuya (tickets, albaranes…). | No hay códigos reservados. |

Servicios que quedan disponibles: `ICustomerService`, `ICustomerInvoiceService`,
`ICustomerInvoiceSeriesService`, `IInvoiceDesignService`. Los errores de negocio son
`Invoicing.BusinessRuleException`, con un mensaje pensado para enseñarlo tal cual al usuario.

### 3. Pantallas (`Invoicing.UI`)

```csharp
services.AddMudServices();
services.AddInvoicingUi(o => o.DefaultAccentColor = "#2F5D2A"); // color de la factura si el diseño no tiene

// Lo que depende del anfitrión (escritorio, web…):
services.AddSingleton<IFilePicker, MyFilePicker>();   // elegir el logotipo o una plantilla
services.AddSingleton<IFileSaver, MyFileSaver>();     // guardar el PDF o la plantilla
services.AddSingleton<IPagePrinter, MyPagePrinter>(); // imprimir / generar el PDF de la pantalla actual
```

```razor
<Router AppAssembly="typeof(App).Assembly" AdditionalAssemblies="[typeof(Invoicing.UI.InvoicingUi).Assembly]">
```

```html
<link href="_content/Invoicing.UI/invoicing.css" rel="stylesheet" />
```

Las series se muestran donde quieras con `<CustomerInvoiceSeriesPanel />`. `NumberingSeriesDialog` sirve
también para tus propias series (pásale tus textos con `NumberingSeriesTexts`). Los títulos de página usan la
clase `page-title`, que puedes definir en tu CSS.

En QuickInventory, `IPagePrinter` está hecho con WebView2 (`DesktopPagePrinter`). En una web se puede hacer
con `window.print()`, y el PDF en el servidor (p. ej. con Chromium sin cabeza).

## Plantillas de factura

Ver `docs/plantillas-factura.md` en QuickInventory. En resumen: HTML con marcadores `{{number}}`,
`{{customer.name}}`, `{{#lines}}…{{/lines}}`, `{{#taxes}}…{{/taxes}}`, secciones `{{#dato}}`/`{{^dato}}`. La
lista completa está en `InvoiceTemplate.Placeholders`. Los nombres en español de las primeras versiones
(`{{numero}}`, `{{#lineas}}`…) siguen valiendo (`InvoiceTemplate.Aliases`).

## Limitaciones conocidas

- Algunos mensajes de error nombran pantallas de QuickInventory («Datos del titular», «Series de facturas»,
  «Clientes»). Si otra aplicación las llama de otra forma, habrá que hacerlos configurables.
- Fiscalidad española: tipos de IVA y exenciones fijos (`TaxRate.All`, `TaxExemption.All`) y NIF validado
  con las reglas españolas (`TaxId`). Los importes se escriben con el formato de moneda de la cultura actual
  (en QuickInventory, `es-ES`).
- El diseño no se guarda por factura: al reimprimir una factura antigua se usa el diseño actual (los datos
  sí son los del momento de emitirla).
