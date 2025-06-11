using System.Text.Json.Serialization;

namespace WSB_project.Models;

public class SupplierModel
{
    public int IdSupplier { get; private set; }
    public string Name { get; private set; }
    public string Address { get; private set; }
    public string Phone { get; private set; }
    
    [JsonConstructor]
    public SupplierModel(string name, string address, string phone)
    {
        Name = name;
        Address = address;
        Phone = phone;
    }
    
    public SupplierModel(int idsupplier, string name, string address, string phone)
    {
        IdSupplier = idsupplier;
        Name = name;
        Address = address;
        Phone = phone;
    }
}