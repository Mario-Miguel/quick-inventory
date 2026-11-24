namespace Invoicing.Services;

/// <summary>
/// Códigos de serie que la aplicación anfitriona ya usa para otros documentos (p. ej. tickets). Una serie
/// de facturas a clientes no puede repetirlos: habría dos facturas con el mismo número. Si no se registra
/// ninguno, no hay códigos reservados.
/// </summary>
public interface IReservedSeriesCodes
{
    Task<IReadOnlySet<string>> GetAsync();
}
