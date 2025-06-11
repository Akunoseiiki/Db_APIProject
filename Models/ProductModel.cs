using System.Text.Json.Serialization;

namespace WSB_project.Models;

public class ProductModel
{
    public int IdProduct { get; private set; }
    public string Name { get; private set; }
    public float Cost { get; private set; }
    public string Description { get; private set; }
    public string Category { get; private set; }
    public string Supplier { get; private set; }
    
    [JsonConstructor]
    public ProductModel( string name, float cost, string description, string category, string supplier)
    {
        Name = name;
        Cost = cost;
        Description = description;
        Category = category;
        Supplier = supplier;
    }
    
    public ProductModel(int idproduct, string name, float cost, string description, string category, string supplier)
    {
        IdProduct = idproduct;
        Name = name;
        Cost = cost;
        Description = description;
        Category = category;
        Supplier = supplier;
    }
}