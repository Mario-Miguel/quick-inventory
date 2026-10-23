# Base de datos

Esquema de la base PostgreSQL que comparten todos los equipos, tal como lo crean las migraciones
de EF Core (`src/QuickInventory.Data/Migrations`) a partir de `BaseDbContext`.
Si cambias un modelo, añade una migración y actualiza también este documento.

Para ver el esquema real en cualquier momento: `dotnet ef migrations script --project src/QuickInventory.Data --startup-project src/QuickInventory.Data`,
o conectarse con `psql` o cualquier cliente de PostgreSQL (pgAdmin, DBeaver...).

## Diagrama

```mermaid
erDiagram
    Products ||--o{ SaleLines : "se vende en"
    Sales ||--|{ SaleLines : "tiene"
    Products ||--o{ StockMovements : "cambios de stock"
    Sales |o--o{ StockMovements : "origina"
    CashMovements ||--o| InvoiceFiles : "factura adjunta"

    Products {
        integer Id PK
        varchar Name "obligatorio, con índice"
        varchar Type "Ticket | Shop (discriminador)"
        varchar BarCode "solo Shop; opcional, con índice"
        bigint SalePrice "céntimos"
        bigint CostPrice "solo Shop; céntimos"
        integer Stock "solo Shop; solo cambia con StockMovements"
        integer MinStock "solo Shop"
        integer CountsAs "solo Ticket; unidades en el recuento"
        boolean Active "nunca se borra"
        timestamp RegistrationDate
    }

    Sales {
        integer Id PK
        timestamp Date "con índice"
        text SalesPoint "TicketOffice | Shop (discriminador)"
        text PaymentMethod "Cash | Card | BankTransfer"
        bigint Total "céntimos"
        boolean Canceled
        boolean GuidedVisit "solo TicketOffice"
        boolean GroupVisit "solo TicketOffice"
    }

    SaleLines {
        integer Id PK
        integer SaleId FK
        integer ProductId FK
        text Description "copia del nombre"
        integer Amount "unidades vendidas"
        bigint BasePrice "céntimos, copia sin descuento"
        bigint UnitPrice "céntimos, precio cobrado"
        text DiscountName "copia, opcional"
        bigint DiscountPercent "en céntimos: 3000 = 30 %"
        integer CountsAs "copia de Products.CountsAs"
        text Category "copia: Ticket | Shop"
    }

    StockMovements {
        integer Id PK
        integer ProductId FK
        integer SaleId FK "opcional"
        integer Amount "+ entra, - sale"
        text Type "Input | Output | Sale | Cancelation"
        text Description "opcional"
        timestamp Date "con índice"
    }

    CashMovements {
        integer Id PK
        timestamp Date "con índice"
        text Type "Income | Pay"
        text Category "Reembolso, ..."
        varchar Description "concepto"
        bigint Amount "céntimos, siempre positivo"
        text PaymentMethod
        text SalesPoint "caja de la que sale; null = sin caja"
        varchar Person "opcional"
        varchar InvoiceNumber "opcional"
        varchar InvoiceFileName "opcional; el archivo está en InvoiceFiles"
        text Notes "opcional"
        boolean Canceled
    }

    InvoiceFiles {
        integer Id PK
        integer CashMovementId FK "único: una factura por movimiento"
        varchar FileName
        varchar ContentType
        bytea Content "PDF o imagen, máx. 10 MB"
    }

    Issuers {
        integer Id PK
        varchar Nif "9 caracteres, normalizado"
        varchar LegalName "nombre o razón social"
        varchar Address "opcional"
    }
```

## Tablas

| Tabla            | Modelo (`QuickInventory.Core/Models`) | Para qué sirve                                                                                           |
| ---------------- | ------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `Products`       | `Product`                             | Todo lo que se vende: entradas (Taquilla) y artículos (Tienda).                                          |
| `Sales`          | `Sale`                                | Una venta: fecha, punto de venta, método de pago y total.                                                |
| `SaleLines`      | `SalesLine`                           | Cada producto de una venta, con nombre y precios **copiados** del momento de la venta.                   |
| `StockMovements` | `StockMovement`                       | Historial de cada cambio de stock (ajustes, ventas, anulaciones).                                        |
| `CashMovements`  | `CashMovement`                        | Dinero que entra o sale y no es una venta. Hoy solo reembolsos (`Category = "Reembolso"`, `Type = Pay`). |
| `InvoiceFiles`   | `InvoiceFile`                         | Factura escaneada de un `CashMovement`. Tabla aparte para no cargar el archivo al listar.                |
| `Issuers`        | `Issuer`                              | Titular del museo (quien emite las facturas). Una sola fila, creada por el asistente de primera instalación. |
| `__EFMigrationsHistory` | —                              | Migraciones aplicadas. La gestiona EF; la app solo la lee para comprobar la versión.                     |

## Relaciones

- **Sale → SaleLines (1 a muchos).** Una venta tiene al menos una línea (lo exige `SalesService`).
- **Product → SaleLines (1 a muchos).** La línea apunta al producto, pero no depende de él para
  mostrarse: nombre, precios, descuento, categoría y `CountsAs` están copiados, así que editar
  el producto no cambia el histórico.
- **Product → StockMovements (1 a muchos).** `Products.Stock` debe ser siempre la suma de sus movimientos.
- **Sale → StockMovements (0/1 a muchos).** Los movimientos de tipo `Sale` y `Cancelation` apuntan a
  la venta que los originó; los ajustes manuales no tienen venta.
- **CashMovement → InvoiceFile (1 a 0/1).** Índice único en `CashMovementId`.
- `CashMovements.SalesPoint` **no** es una relación: es el enum de la caja (Taquilla/Tienda). Los
  reembolsos en efectivo de una caja se restan al cerrarla.

## Convenciones de almacenamiento

- **Dinero:** todos los `decimal` se guardan como **céntimos enteros** (`AmountInCentsConverter`).
  `12,50 €` → `1250`. Ojo: `DiscountPercent` también es `decimal`, así que `30 %` se guarda como `3000`.
- **Enums:** como texto (`"Cash"`, `"TicketOffice"`...). `ProductCategory` se guarda por su `Value`
  (`"Ticket"`, `"Shop"`).
- **Fechas:** `timestamp without time zone`, con la hora local del museo (`DateTime.Now`).
  Es una convención de `BaseDbContext.ConfigureConventions`.
- **Herencia:** una tabla por jerarquía. `Products.Type` y `Sales.SalesPoint` dicen la clase; las
  columnas de una sola clase (`Stock`, `CountsAs`, `GuidedVisit`...) son null en las filas de la otra.
- **Ids:** columnas `identity` de PostgreSQL.
- **Nada se borra:** productos (`Active`), ventas (`Canceled`) y reembolsos (`Canceled`) se marcan,
  para conservar el historial.
