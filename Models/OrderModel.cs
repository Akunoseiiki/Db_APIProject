using System.Text.Json.Serialization;

namespace WSB_project.Models;

public class OrderModel
{
    public int IdOrder { get; private set; }
    public DateTime OrderDate { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }
    public string Address { get; private set; }
    public string PostalCode { get; private set; }
    public string Email { get; private set; }
    public string Phone { get; private set; }
    public List<ProductInOrderModel> Products { get; set; }

    [JsonConstructor]  //wskazuje, ktorego konstruktowa uzyc gdy admin chce utworzyc nowe zamówienie
    public OrderModel(string firstName, string lastName, string city, string country,
        string address, string postalCode, string email, string phone, List<ProductInOrderModel> products)
    {
        FirstName = firstName;
        LastName = lastName;
        City = city;
        Country = country;
        Address = address;
        PostalCode = postalCode;
        Email = email;
        Phone = phone;
        Products = products;
    }
    
    public OrderModel(int idOrder, DateTime orderDate, string firstName, string lastName, string city, string country,
        string address, string postalCode, string email, string phone, List<ProductInOrderModel> products)
    {
        IdOrder = idOrder;
        OrderDate = orderDate;
        FirstName = firstName;
        LastName = lastName;
        City = city;
        Country = country;
        Address = address;
        PostalCode = postalCode;
        Email = email;
        Phone = phone;
        Products = products;
    }
}