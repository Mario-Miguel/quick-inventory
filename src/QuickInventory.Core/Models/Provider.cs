namespace QuickInventory.Core.Models;

/// <summary>
/// Proveedor habitual del museo: quien nos emite las facturas de compra (<see cref="PurchaseInvoice"/>).
/// No se borra nunca (sus facturas lo referencian); se desactiva con <see cref="Active"/>.
/// </summary>
public class Provider
{
    public int Id { get; set; }
    /// <summary>Nombre o razón social, tal como aparece en sus facturas.</summary>
    public string LegalName { get; set; } = "";
    /// <summary>NIF en mayúsculas y sin espacios ni guiones. No se puede repetir.</summary>
    public string Nif { get; set; } = "";
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    /// <summary>Teléfono tal como se escribe (con espacios, prefijo...), para que se lea bien.</summary>
    public string? PhoneNumber { get; set; }
    public bool Active { get; set; } = true;

    public const int MaxLegalNameLength = 120;
    public const int MaxAddressLength = 300;
    public const int MaxEmailLength = 254;
    public const int MaxNotesLength = 300;
    public const int MaxPhoneNumberLength = 30;

    public Provider Clone() => (Provider)MemberwiseClone();

    public void Normalize()
    {
        Nif = TaxId.Normalize(Nif);
        LegalName = LegalName?.Trim() ?? "";
        Address = Clean(Address);
        Email = Clean(Email);
        // Espacios repetidos fuera: "985  12 34 56" → "985 12 34 56".
        PhoneNumber = Clean(PhoneNumber) is { } phone ? string.Join(' ', phone.Split(' ', StringSplitOptions.RemoveEmptyEntries)) : null;
        Notes = Clean(Notes);
    }

    public void Validate()
    {
        if (LegalName.Length == 0)
            throw new BusinessRuleException("Escribe el nombre del proveedor.");
        if (LegalName.Length > MaxLegalNameLength)
            throw new BusinessRuleException($"El nombre del proveedor no puede tener más de {MaxLegalNameLength} caracteres.");
        if (Nif.Length == 0)
            throw new BusinessRuleException("Escribe el NIF del proveedor.");
        if (!TaxId.IsValid(Nif))
            throw new BusinessRuleException("El NIF no es correcto. Revisa que no falte ni sobre ningún número o letra.");
        if (Address?.Length > MaxAddressLength)
            throw new BusinessRuleException($"La dirección no puede tener más de {MaxAddressLength} caracteres.");
        if (Email?.Length > MaxEmailLength)
            throw new BusinessRuleException($"El email no puede tener más de {MaxEmailLength} caracteres.");
        if (PhoneNumberError(PhoneNumber) is { } phoneError)
            throw new BusinessRuleException(phoneError);
        if (Notes?.Length > MaxNotesLength)
            throw new BusinessRuleException($"Las notas no pueden tener más de {MaxNotesLength} caracteres.");
    }

    /// <summary>
    /// Por qué el teléfono no es válido, o null si lo es (o está vacío: es opcional). Admite números
    /// españoles y extranjeros: cifras, espacios, «-», «.», paréntesis y un «+» al principio, con
    /// entre 6 y 15 cifras (el máximo internacional). Lo usan <see cref="Validate"/> y el formulario.
    /// </summary>
    public static string? PhoneNumberError(string? phone)
    {
        phone = phone?.Trim();
        if (string.IsNullOrEmpty(phone)) return null;

        if (phone.Length > MaxPhoneNumberLength)
            return $"El teléfono no puede tener más de {MaxPhoneNumberLength} caracteres.";
        // El "+" solo vale delante del prefijo: "+34 ..." o "(+34) ...".
        var plus = phone.LastIndexOf('+');
        var plusOk = plus < 0 || (phone.IndexOf('+') == plus && (plus == 0 || phone[..plus] == "("));
        if (!phone.All(c => char.IsAsciiDigit(c) || c is ' ' or '-' or '.' or '(' or ')' or '+') || !plusOk)
            return "El teléfono solo puede llevar números, espacios, guiones y un «+» delante del prefijo.";
        var digits = phone.Count(char.IsAsciiDigit);
        if (digits < 6 || digits > 15)
            return "El teléfono no es correcto. Revisa que no falte ni sobre ningún número.";
        return null;
    }

    /// <summary>Copia los datos editables. No toca Id ni Active.</summary>
    public void CopyFrom(Provider other)
    {
        Nif = other.Nif;
        LegalName = other.LegalName;
        Address = other.Address;
        Email = other.Email;
        PhoneNumber = other.PhoneNumber;
        Notes = other.Notes;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
