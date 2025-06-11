namespace WSB_project.Models;

public class ProductSupplierModel
{
    public int IdProduct { get; private set; }
    public int IdSupplier { get; private set; }
    
    public ProductSupplierModel(int idproduct, int idsupplier)
    {
        IdProduct = idproduct;
        IdSupplier = idsupplier;
    }
}