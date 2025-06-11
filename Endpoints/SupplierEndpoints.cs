using Microsoft.OpenApi.Models;
using Npgsql;
using WSB_project.Models;

namespace WSB_project.Endpoints;

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/", GetAllSuppliers)
            .WithName("GetSuppliers")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca listę dostawców"
            })
            .Produces<List<SupplierModel>>();
        
        routes.MapGet("/{columnName}/{orderBy}", GetAllSuppliers)
            .WithName("GetSuppliersWithParams")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca posortowaną listę dostawców"
            })
            .Produces<List<SupplierModel>>();

        routes.MapPost("/", CreateSupplier)
            .WithName("NewSupplier")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Tworzy nowego dostawcę"
            });

        routes.MapDelete("/{supplierId:int}", DeleteSupplier)
            .WithName("DeleteSupplier")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Usuwa dostawcę"
            });

        routes.MapPut("/{supplierId:int}", UpdateSupplier)
            .WithName("UpdateSupplier")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Aktualizuje dane dostawcy"
            });
    }

    private static async Task<IResult> GetAllSuppliers(ILogger<Program> logger, IConfiguration configuration, 
        string? columnName = "id_supplier", string? orderBy = "ASC")
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Pobrano listę wszystkich dostawców.");

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = "SELECT * FROM suppliers ORDER BY " + columnName + " " + orderBy;
            await using var command = new NpgsqlCommand(query, connection);
            var suppliersQuery = await command.ExecuteReaderAsync();
            var suppliers = new List<SupplierModel>();

            while (suppliersQuery.Read())
            {
                suppliers.Add(new SupplierModel(
                    suppliersQuery.GetInt32(0), // idSupplier
                    suppliersQuery.GetString(1), // name
                    suppliersQuery.GetString(2), // address
                    suppliersQuery.GetString(3)  // phone
                ));
            }

            return Results.Ok(suppliers);
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas pobierania listy dostawców: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas pobierania listy dostawców.");
        }
    }

    private static async Task<IResult> CreateSupplier(ILogger<Program> logger, IConfiguration configuration,
        SupplierModel supplier)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Dodano nowego dostawcę: {supplierName}", supplier.Name);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var newSupplierQuery = @"
                        INSERT INTO suppliers (name, address, phone)
                        VALUES (@Name, @Address, @Phone)
                        RETURNING id_supplier"; // Zwróci id utworzonego wiersza

            await using var newSupplierCommand = new NpgsqlCommand(newSupplierQuery, connection);
            newSupplierCommand.Parameters.AddWithValue("Name", supplier.Name);
            newSupplierCommand.Parameters.AddWithValue("Address", supplier.Address);
            newSupplierCommand.Parameters.AddWithValue("Phone", supplier.Phone);

            var newSupplierId =
                await newSupplierCommand.ExecuteScalarAsync(); // Pobiera pierwszą kolumnę pierwszego wiersza wyników

            if (newSupplierId == null)
            {
                return Results.Problem("Nie udało się utworzyć dostawcy");
            }

            logger.LogInformation("Utworzono dostawcę z ID: {newSupplierId}", newSupplierId);
            return Results.Ok($"Dostawca został utworzony pomyślnie z ID: {newSupplierId}");
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas zapisywania dostawcy: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas zapisywania dostawcy.");
        }
    }

    private static async Task<IResult> DeleteSupplier(ILogger<Program> logger, IConfiguration configuration,
        int supplierId)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Usunięto dostawcę o ID: {supplierId}", supplierId);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = "DELETE FROM suppliers WHERE id_supplier = @IdSupplier";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("IdSupplier", supplierId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                logger.LogInformation("Dostawca o ID {supplierId} został usunięty.", supplierId);
                return Results.Ok($"Dostawca o ID {supplierId} został usunięty.");
            }
            else
            {
                logger.LogWarning("Nie znaleziono dostawcy o ID {supplierId}.", supplierId);
                return Results.NotFound($"Nie znaleziono dostawcy o ID {supplierId}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas usuwania dostawcy: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas usuwania dostawcy.");
        }
    }

    private static async Task<IResult> UpdateSupplier(ILogger<Program> logger, IConfiguration configuration,
        int supplierId, SupplierModel supplier)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Aktualizowanie danych dostawcy o ID: {supplierId}", supplierId);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = @"
                        UPDATE suppliers
                        SET name = @Name,
                            address = @Address,
                            phone = @Phone
                        WHERE id_supplier = @IdSupplier";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Name", supplier.Name);
            command.Parameters.AddWithValue("Address", supplier.Address);
            command.Parameters.AddWithValue("Phone", supplier.Phone);
            command.Parameters.AddWithValue("IdSupplier", supplierId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                logger.LogInformation("Dostawca o ID {supplierId} został zaktualizowany.", supplierId);
                return Results.Ok($"Dostawca o ID {supplierId} został zaktualizowany.");
            }
            else
            {
                logger.LogWarning("Nie znaleziono dostawcy o ID {supplierId}.", supplierId);
                return Results.NotFound($"Nie znaleziono dostawcy o ID {supplierId}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas aktualizacji dostawcy: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas aktualizacji dostawcy.");
        }
    }
}
