using System.Text.Json.Serialization;

namespace WSB_project.Models;

public class ProductInOrderModel
{
    public int IdProduct { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }

    public ProductInOrderModel(int idProduct, string productName, int quantity)
    {
        IdProduct = idProduct;
        ProductName = productName;
        Quantity = quantity;
    }
    
    [JsonConstructor]  
    public ProductInOrderModel(int idProduct, int quantity)
    {
        IdProduct = idProduct;
        Quantity = quantity;
    }
}
