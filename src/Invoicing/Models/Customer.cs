namespace Invoicing.Models;

/// <summary>
/// Cliente habitual (colegios, asociaciones, agencias...): a quien el museo emite facturas completas
/// (<see cref="CustomerInvoice"/>). La dirección es obligatoria porque tiene que salir en la factura.
/// </summary>
public class Customer : Contact
{
    protected override string OfWhom => "del cliente";

    public Customer Clone() => (Customer)MemberwiseClone();

    public override void Validate()
    {
        base.Validate();
        if (Address is null)
            throw new BusinessRuleException("Escribe la dirección del cliente: tiene que salir en sus facturas.");
    }
}
