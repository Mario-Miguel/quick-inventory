using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Diseño de las facturas a clientes, común a todos los equipos.</summary>
public interface IInvoiceDesignService
{
    /// <summary>El diseño guardado, o el de por defecto si todavía no se ha cambiado nunca.</summary>
    Task<InvoiceDesign> GetDesignAsync();

    /// <summary>Guarda el diseño (lo crea la primera vez). Afecta también a las facturas ya emitidas al imprimirlas.</summary>
    Task SaveDesignAsync(InvoiceDesign design);
}
