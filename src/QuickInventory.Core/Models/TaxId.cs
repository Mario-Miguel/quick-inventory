namespace QuickInventory.Core.Models;

/// <summary>
/// Comprobación del NIF español: de persona física (DNI, o NIE para extranjeros)
/// y de persona jurídica o entidad (el antiguo CIF). Solo mira el formato y el dígito
/// de control; no comprueba que exista en el censo de Hacienda.
/// </summary>
public static class TaxId
{
    private const string DniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string EntityLetters = "ABCDEFGHJNPQRSUVW";
    private const string EntityControlLetters = "JABCDEFGHI";

    /// <summary>Quita espacios, guiones y puntos, y pasa a mayúsculas.</summary>
    public static string Normalize(string? value) =>
        new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>Indica si el valor (ya normalizado) es un NIF, NIE o NIF de entidad válido.</summary>
    public static bool IsValid(string value)
    {
        if (value.Length != 9) return false;

        var first = value[0];
        if (char.IsDigit(first)) return IsValidDni(value);
        if (first is 'X' or 'Y' or 'Z') return IsValidDni("XYZ".IndexOf(first) + value[1..]);
        // K, L y M: NIF de personas físicas sin DNI (menores, extranjeros sin NIE...).
        if (first is 'K' or 'L' or 'M') return IsValidDni("0" + value[1..]);
        if (EntityLetters.Contains(first)) return IsValidEntity(value);
        return false;
    }

    /// <summary>8 dígitos y la letra que les corresponde.</summary>
    private static bool IsValidDni(string value) =>
        value[..8].All(char.IsDigit) && DniLetters[int.Parse(value[..8]) % 23] == value[8];

    /// <summary>Letra del tipo de entidad, 7 dígitos y un control que puede ser número o letra.</summary>
    private static bool IsValidEntity(string value)
    {
        var digits = value[1..8];
        if (!digits.All(char.IsDigit)) return false;

        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[i] - '0';
            // Las posiciones impares (1.ª, 3.ª...) se multiplican por 2 y se suman sus cifras.
            if (i % 2 == 0)
            {
                digit *= 2;
                digit = digit / 10 + digit % 10;
            }
            sum += digit;
        }
        var control = (10 - sum % 10) % 10;

        var type = value[0];
        var actual = value[8];
        var asDigit = (char)('0' + control);
        var asLetter = EntityControlLetters[control];

        // Algunas entidades llevan siempre letra de control y otras siempre número.
        return type switch
        {
            'N' or 'P' or 'Q' or 'R' or 'S' or 'W' => actual == asLetter,
            'A' or 'B' or 'E' or 'H' => actual == asDigit,
            _ => actual == asDigit || actual == asLetter,
        };
    }
}
