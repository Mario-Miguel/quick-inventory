namespace QuickInventory.Core.Models;


public class ShopProduct : Product
{
    public override ProductCategory Category => ProductCategory.Shop;

    public override bool StockControl => true;

    public string? BarCode { get; set; }
    public decimal CostPrice { get; set; }
    public int Stock { get; set; }
    public int MinStock { get; set; }
    
    public bool LowStock => Stock <= MinStock;

    public override void Normalize()
    {
        base.Normalize();
        BarCode = string.IsNullOrWhiteSpace(BarCode) ? null : BarCode.Trim();
    }

    public override void Validate()
    {
        base.Validate();
        if (CostPrice < 0)
            throw new BusinessRuleException("Los precios no pueden ser negativos.");
        if (Stock < 0 || MinStock < 0)
            throw new BusinessRuleException("El stock no puede ser negativo.");
    }

    public override void CopyFrom(Product other)
      {
          base.CopyFrom(other);
          var o = (ShopProduct)other;
          BarCode = o.BarCode;
          CostPrice = o.CostPrice;
          MinStock = o.MinStock;
      }

      /// <summary>Suma o resta unidades comprobando que no quede negativo.</summary>
      public void ChangeStock(int amount)
      {
          if (Stock + amount < 0)
              throw new BusinessRuleException(
                  $"No hay stock suficiente. Quedan {Stock} unidades de «{Name}».");
          Stock += amount;
      }
}