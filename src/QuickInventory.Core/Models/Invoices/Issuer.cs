namespace QuickInventory.Core.Models;

/// <summary>
/// Titular del museo: quien emite las facturas. Hay una sola fila, que se rellena
/// con el asistente de la primera instalación.
/// </summary>
public class Issuer
{
    public int Id { get; set; }
    /// <summary>NIF del titular, en mayúsculas y sin espacios ni guiones (p. ej. "G33123456").</summary>
    public string Nif { get; set; } = "";
    /// <summary>Nombre o razón social, tal como figura en Hacienda.</summary>
    public string LegalName { get; set; } = "";
    /// <summary>Dirección para el ticket (opcional).</summary>
    public string? Address { get; set; }

    public Issuer Clone() => (Issuer)MemberwiseClone();

    public void Normalize()
    {
        Nif = TaxId.Normalize(Nif);
        LegalName = LegalName?.Trim() ?? "";
        Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim();
    }

    public void Validate()
    {
        if (Nif.Length == 0)
            throw new BusinessRuleException("Escribe el NIF del titular.");
        if (!TaxId.IsValid(Nif))
            throw new BusinessRuleException("El NIF no es correcto. Revisa que no falte ni sobre ningún número o letra.");
        if (LegalName.Length == 0)
            throw new BusinessRuleException("Escribe el nombre o la razón social del titular.");
    }

    public void CopyFrom(Issuer other)
    {
        Nif = other.Nif;
        LegalName = other.LegalName;
        Address = other.Address;
    }
}
