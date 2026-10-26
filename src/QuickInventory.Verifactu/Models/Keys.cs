namespace QuickInventory.Verifactu;

/// <summary>Entorno de la AEAT: pruebas (no tiene efectos fiscales) o producción.</summary>
public enum AeatEnvironment { Test, Production }

/// <summary>Tipo de factura (TipoFactura). La librería solo genera F2 y R5; el resto se rechaza al validar.</summary>
public enum InvoiceType
{
    /// <summary>Factura completa.</summary>
    F1,
    /// <summary>Factura simplificada.</summary>
    F2,
    /// <summary>Factura completa en sustitución de simplificadas.</summary>
    F3,
    R1, R2, R3, R4,
    /// <summary>Rectificativa de una factura simplificada (devoluciones).</summary>
    R5,
}

/// <summary>Tipo de rectificativa (TipoRectificativa).</summary>
public enum CorrectionType
{
    /// <summary>"S": la rectificativa sustituye a la original y lleva los importes rectificados.</summary>
    Substitution,
    /// <summary>"I": la rectificativa solo lleva la diferencia (en una devolución, en negativo).</summary>
    Differences,
}

/// <summary>Causa de exención del IVA (OperacionExenta). E1 es la del art. 20 de la Ley del IVA.</summary>
public enum TaxExemption { E1, E2, E3, E4, E5, E6, E7, E8 }

/// <summary>RechazoPrevio de un registro de alta.</summary>
public enum PreviousRejection
{
    /// <summary>No hubo rechazo. No se envía el campo (equivale a "N").</summary>
    None,
    /// <summary>"S": se subsana un registro que la AEAT rechazó.</summary>
    Rejected,
    /// <summary>"X": el registro no existe en la AEAT (se rechazó o no se llegó a enviar).</summary>
    NotInAeat,
}
