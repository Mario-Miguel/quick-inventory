# Base de datos

Esquema de `museo.db` (SQLite), tal como lo crea EF Core a partir de `BaseDbContext`.
Si cambias un modelo, actualiza también este documento.

Para ver el esquema real en cualquier momento: `db.Database.GenerateCreateScript()`,
o abrir `%LOCALAPPDATA%\QuickInventory\museo.db` con [DB Browser for SQLite](https://sqlitebrowser.org/).

## Diagrama

```mermaid
erDiagram
    Products ||--o{ SaleLines : "se vende en"
    Sales ||--|{ SaleLines : "tiene"
    Products ||--o{ StockMovements : "cambios de stock"
    Sales |o--o{ StockMovements : "origina"
    CashMovements ||--o| InvoiceFiles : "factura adjunta"

    Products {
        INTEGER Id PK
        TEXT Type "discriminador: Ticket | Shop"
        TEXT Name "obligatorio"
        INTEGER SalePrice "céntimos"
        INTEGER CountsAs "solo Ticket: unidades en el recuento (1 normalmente)"
        TEXT BarCode "solo Shop: opcional, con índice"
        INTEGER CostPrice "solo Shop: céntimos"
        INTEGER Stock "solo Shop: solo cambia con StockMovements"
        INTEGER MinStock "solo Shop"
        INTEGER Active "bool; nunca se borra"
        TEXT RegistrationDate
    }

    Sales {
        INTEGER Id PK
        TEXT Date "con índice"
        TEXT SalesPoint "TicketOffice | Shop"
        TEXT PaymentMethod "Cash | Card | BankTransfer"
        INTEGER Total "céntimos"
        INTEGER Canceled "bool"
        INTEGER GuidedVisit "bool"
        INTEGER GroupVisit "bool"
    }

    SaleLines {
        INTEGER Id PK
        INTEGER SaleId FK
        INTEGER ProductId FK
        TEXT Description "copia del nombre"
        INTEGER Amount "unidades vendidas"
        INTEGER BasePrice "céntimos, copia sin descuento"
        INTEGER UnitPrice "céntimos, precio cobrado"
        TEXT DiscountName "copia, opcional"
        INTEGER DiscountPercent "en céntimos: 3000 = 30 %"
        INTEGER CountsAs "copia de Products.CountsAs"
        TEXT Category "copia: Ticket | Shop"
    }

    StockMovements {
        INTEGER Id PK
        INTEGER ProductId FK
        INTEGER SaleId FK "opcional"
        INTEGER Amount "+ entra, - sale"
        TEXT Type "Input | Output | Sale | Cancelation"
        TEXT Description "opcional"
        TEXT Date "con índice"
    }

    CashMovements {
        INTEGER Id PK
        TEXT Date "con índice"
        TEXT Type "Income | Pay"
        TEXT Category "Reembolso, ..."
        TEXT Description "concepto"
        INTEGER Amount "céntimos, siempre positivo"
        TEXT PaymentMethod
        TEXT SalesPoint "caja de la que sale; null = sin caja"
        TEXT Person "opcional"
        TEXT InvoiceNumber "opcional"
        TEXT InvoiceFileName "opcional; el archivo está en InvoiceFiles"
        TEXT Notes "opcional"
        INTEGER Canceled "bool"
    }

    InvoiceFiles {
        INTEGER Id PK
        INTEGER CashMovementId FK "único: una factura por movimiento"
        TEXT FileName
        TEXT ContentType
        BLOB Content "PDF o imagen, máx. 10 MB"
    }

    OnlineShopSettings {
        INTEGER Id PK "una sola fila"
        TEXT Url "dirección de PrestaShop"
        TEXT ApiKey "clave del Webservice (solo GET en orders)"
        TEXT TicketProductIds "ids separados por comas; vacío = todos"
        INTEGER DaysInAdvance "antelación máxima de compra"
    }
```

## Tablas

| Tabla            | Modelo (`QuickInventory.Core/Models`) | Para qué sirve                                                                                           |
| ---------------- | ------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `Products`       | `Product` (`TicketProduct`, `ShopProduct`) | Todo lo que se vende: entradas (Taquilla) y artículos (Tienda). Una sola tabla; `Type` dice cuál es y las columnas del otro tipo quedan a null. |
| `Sales`          | `Sale`                                | Una venta: fecha, punto de venta, método de pago y total.                                                |
| `SaleLines`      | `SalesLine`                           | Cada producto de una venta, con nombre y precios **copiados** del momento de la venta.                   |
| `StockMovements` | `StockMovement`                       | Historial de cada cambio de stock (ajustes, ventas, anulaciones).                                        |
| `CashMovements`  | `CashMovement`                        | Dinero que entra o sale y no es una venta. Hoy solo reembolsos (`Category = "Reembolso"`, `Type = Pay`). |
| `InvoiceFiles`   | `InvoiceFile`                         | Factura escaneada de un `CashMovement`. Tabla aparte para no cargar el archivo al listar.                |
| `OnlineShopSettings` | `OnlineShopSettings`              | Conexión con la tienda online (PrestaShop). Las entradas online (`OnlineTicket`) no se guardan: se leen de la tienda cada vez. |

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
- **Fechas:** texto ISO (así las guarda SQLite).
- **bool:** `0` / `1`.
- **Nada se borra:** productos (`Active`), ventas (`Canceled`) y reembolsos (`Canceled`) se marcan,
  para conservar el historial.
