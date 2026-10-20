namespace QuickInventory.Core.Models;


public class TicketProduct : Product
{
    public override ProductCategory Category => ProductCategory.Ticket;

    public int CountsAs { get; set; } = 1;


    public override void Validate()
    {
        base.Validate();
        if (CountsAs < 1)
            throw new BusinessRuleException("Cada producto tiene que contar como 1 unidad o más.");
    }

    public override void CopyFrom(Product other)
      {
          base.CopyFrom(other);
          var o = (TicketProduct)other;
          CountsAs = o.CountsAs;
      }
}