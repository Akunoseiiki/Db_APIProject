using Microsoft.OpenApi.Models;
using Npgsql;
using WSB_project.Models;

namespace WSB_project.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/", GetAllProducts)
            .WithName("GetProducts")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca listę produktów"
            })
            .Produces<List<ProductModel>>();
        
        routes.MapGet("/{columnName}/{orderBy}", GetAllProducts)
            .WithName("GetProductsWithParams")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca posortowaną listę produktów"
            })
            .Produces<List<ProductModel>>();

        routes.MapPost("/", CreateProduct)
            .WithName("NewProduct")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Tworzy nowy produkt"
            });

        routes.MapDelete("/{productId:int}", DeleteProduct)
            .WithName("DeleteProduct")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Usuwa produkt"
            });

        routes.MapPut("/{productId:int}", UpdateProduct)
        .WithName("UpdateProduct")
        .WithOpenApi(operation => new OpenApiOperation(operation)
        {
            Summary = "Aktualizuje produkt"
        });
    }

    private static async Task<IResult> GetAllProducts(ILogger<Program> logger, IConfiguration configuration, 
        string? columnName = "id_product", string? orderBy = "ASC")
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Pobrano listę wszystkich produktów.");

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = @"SELECT p.id_product, p.name, p.cost, p.description, c.name, s.name AS supplier_name FROM products p
                        JOIN products_categories pc ON p.id_product = pc.id_product
                        JOIN categories c ON pc.id_category = c.id_category
                        JOIN products_suppliers ps ON p.id_product = ps.id_product
                        JOIN suppliers s ON ps.id_supplier = s.id_supplier 
                        ORDER BY p." + columnName + " " + orderBy;
            await using var command = new NpgsqlCommand(query, connection);
            var productsQuery = await command.ExecuteReaderAsync();
            var products = new List<ProductModel>();
            
            while (productsQuery.Read())
            {
                products.Add(new ProductModel(
                    productsQuery.GetInt32(0), // idProduct
                    productsQuery.GetString(1), // name
                    productsQuery.GetFloat(2), // cost
                    productsQuery.GetString(3), // description
                    productsQuery.GetString(4), // category
                    productsQuery.GetString(5)  // supplier
                ));
            }

            return Results.Ok(products);
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas pobierania listy produktów: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas pobierania listy produktów.");
        }
    }

    private static async Task<IResult> CreateProduct(ILogger<Program> logger, IConfiguration configuration,
        ProductModel product)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Dodano nowy produkt: {productName}", product.Name);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();
            
            // Pobranie id kategorii
            var getCategoryIdQuery = @"SELECT id_category FROM categories WHERE name LIKE @CategoryName";

            await using var getCategoryIdCommand = new NpgsqlCommand(getCategoryIdQuery, connection);
            getCategoryIdCommand.Parameters.AddWithValue("CategoryName", product.Category);

            var categoryResults = await getCategoryIdCommand.ExecuteReaderAsync();
            
            int? categoryId = null;

            while (categoryResults.Read())
            {
                categoryId = categoryResults.GetInt32(0);
            }

            await categoryResults.DisposeAsync();

            if (categoryId == null)
            {
                return Results.Problem("Nie znaleziono wybranej kategorii");
            }
            
            logger.LogInformation("Id kategorii {nazwa} to {id}.", product.Category, categoryId );

            // Pobranie id dostawcy
            var getSupplierIdQuery = @"SELECT id_supplier FROM suppliers WHERE name LIKE @SupplierName";

            await using var getSupplierIdCommand = new NpgsqlCommand(getSupplierIdQuery, connection);
            getSupplierIdCommand.Parameters.AddWithValue("SupplierName", product.Supplier);

            var supplierResults = await getSupplierIdCommand.ExecuteReaderAsync();
            
            int? supplierId = null;

            while (supplierResults.Read())
            {
                supplierId = supplierResults.GetInt32(0);
            }

            await supplierResults.DisposeAsync();

            if (supplierId == null)
            {
                return Results.Problem("Nie znaleziono wybranego dostawcy");
            }
            
            logger.LogInformation("Id dostawcy {nazwa} to {id}.", product.Supplier, supplierId);

            // Tworzenie produktu
            var newProductQuery = @"
                        INSERT INTO Products (name, description, cost)
                        VALUES (@Name, @Description, @Cost)
                        RETURNING id_product"; // Zwróci id utworzonego wiersza

            await using var newProductCommand = new NpgsqlCommand(newProductQuery, connection);
            newProductCommand.Parameters.AddWithValue("Name", product.Name);
            newProductCommand.Parameters.AddWithValue("Description", product.Description);
            newProductCommand.Parameters.AddWithValue("Cost", product.Cost);

            var newProductId =
                await newProductCommand.ExecuteScalarAsync(); // Pobiera pierwszą kolumnę pierwszego wiersza wyników

            if (newProductId == null)
            {
                return Results.Problem("Nie udało się utworzyć produktu");
            }
            
            logger.LogInformation("Utworzono produkt z ID: {newProductId}", newProductId);
            
            // Dodanie produktu do kategorii
            var addToProductsCategoriesQuery = @"
                        INSERT INTO products_categories (id_product, id_category)
                        VALUES (@Id_product, @Id_category)";
            
            await using var addToProductsCategoriesCommand = new NpgsqlCommand(addToProductsCategoriesQuery, connection);
            addToProductsCategoriesCommand.Parameters.AddWithValue("Id_product", newProductId);
            addToProductsCategoriesCommand.Parameters.AddWithValue("Id_category", categoryId);

            await addToProductsCategoriesCommand.ExecuteNonQueryAsync();

            // Dodanie produktu do dostawcy
            var addToProductsSuppliersQuery = @"
                        INSERT INTO products_suppliers (id_product, id_supplier)
                        VALUES (@Id_product, @Id_supplier)";

            await using var addToProductsSuppliersCommand = new NpgsqlCommand(addToProductsSuppliersQuery, connection);
            addToProductsSuppliersCommand.Parameters.AddWithValue("Id_product", newProductId);
            addToProductsSuppliersCommand.Parameters.AddWithValue("Id_supplier", supplierId);

            await addToProductsSuppliersCommand.ExecuteNonQueryAsync();

            logger.LogInformation("Utworzono produkt z ID: {newProductId}", newProductId);
            return Results.Ok($"Produkt został utworzony pomyślnie z ID: {newProductId}");
            
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas zapisywania produktu: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas zapisywania produktu.");
        }
    }

    private static async Task<IResult> DeleteProduct(ILogger<Program> logger, IConfiguration configuration,
        int productId)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Usunięto produkt o ID: {productId}", productId);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = "DELETE FROM Products WHERE id_product = @IdProduct";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("IdProduct", productId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                logger.LogInformation("Produkt o ID {productId} został usunięty.", productId);
                return Results.Ok($"Produkt o ID {productId} został usunięty.");
            }
            else
            {
                logger.LogWarning("Nie znaleziono produktu o ID {productId}.", productId);
                return Results.NotFound($"Nie znaleziono produktu o ID {productId}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas usuwania produktu: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas usuwania produktu.");
        }
    }

    private static async Task<IResult> UpdateProduct(ILogger<Program> logger, IConfiguration configuration,
        int productId, ProductModel product)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Aktualizowanie danych produktu o ID: {productId}", productId);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            // Pobranie id kategorii
            var getCategoryIdQuery = @"SELECT id_category FROM categories WHERE name LIKE @CategoryName";
            await using var getCategoryIdCommand = new NpgsqlCommand(getCategoryIdQuery, connection);
            getCategoryIdCommand.Parameters.AddWithValue("CategoryName", product.Category);
            var categoryResults = await getCategoryIdCommand.ExecuteReaderAsync();

            int? categoryId = null;
            while (categoryResults.Read())
            {
                categoryId = categoryResults.GetInt32(0);
            }
            await categoryResults.DisposeAsync();

            if (categoryId == null)
            {
                return Results.Problem("Nie znaleziono wybranej kategorii");
            }

            // Pobranie id dostawcy
            var getSupplierIdQuery = @"SELECT id_supplier FROM suppliers WHERE name LIKE @SupplierName";
            await using var getSupplierIdCommand = new NpgsqlCommand(getSupplierIdQuery, connection);
            getSupplierIdCommand.Parameters.AddWithValue("SupplierName", product.Supplier);
            var supplierResults = await getSupplierIdCommand.ExecuteReaderAsync();

            int? supplierId = null;
            while (supplierResults.Read())
            {
                supplierId = supplierResults.GetInt32(0);
            }
            await supplierResults.DisposeAsync();

            if (supplierId == null)
            {
                return Results.Problem("Nie znaleziono wybranego dostawcy");
            }

            // Aktualizacja podstawowych danych produktu
            var updateProductQuery = @"
            UPDATE products
            SET name = @Name,
                cost = @Cost,
                description = @Description
            WHERE id_product = @IdProduct";

            await using var updateProductCommand = new NpgsqlCommand(updateProductQuery, connection);
            updateProductCommand.Parameters.AddWithValue("Name", product.Name);
            updateProductCommand.Parameters.AddWithValue("Cost", product.Cost);
            updateProductCommand.Parameters.AddWithValue("Description", product.Description);
            updateProductCommand.Parameters.AddWithValue("IdProduct", productId);

            var rowsAffected = await updateProductCommand.ExecuteNonQueryAsync();

            if (rowsAffected <= 0)
            {
                return Results.NotFound($"Nie znaleziono produktu o ID {productId}.");
            }

            // Aktualizacja kategorii
            var updateCategoryQuery = @"
            UPDATE products_categories 
            SET id_category = @CategoryId
            WHERE id_product = @ProductId";

            await using var updateCategoryCommand = new NpgsqlCommand(updateCategoryQuery, connection);
            updateCategoryCommand.Parameters.AddWithValue("CategoryId", categoryId);
            updateCategoryCommand.Parameters.AddWithValue("ProductId", productId);
            await updateCategoryCommand.ExecuteNonQueryAsync();

            // Aktualizacja dostawcy
            var updateSupplierQuery = @"
            UPDATE products_suppliers 
            SET id_supplier = @SupplierId
            WHERE id_product = @ProductId";

            await using var updateSupplierCommand = new NpgsqlCommand(updateSupplierQuery, connection);
            updateSupplierCommand.Parameters.AddWithValue("SupplierId", supplierId);
            updateSupplierCommand.Parameters.AddWithValue("ProductId", productId);
            await updateSupplierCommand.ExecuteNonQueryAsync();

            logger.LogInformation("Produkt o ID {productId} został zaktualizowany.", productId);
            return Results.Ok($"Produkt o ID {productId} został zaktualizowany.");
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas aktualizacji produktu: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas aktualizacji produktu.");
        }
    }
}
