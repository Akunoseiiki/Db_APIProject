using Microsoft.OpenApi.Models;
using Npgsql;
using WSB_project.Models;

namespace WSB_project.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/", GetAllCategories)
            .WithName("GetCategories")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca listę kategorii"
            })
            .Produces<List<CategoryModel>>();
        
        routes.MapGet("/{columnName}/{orderBy}", GetAllCategories)
            .WithName("GetCategoriesWithParams")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca posortowaną listę kategorii"
            })
            .Produces<List<CategoryModel>>();

        routes.MapPost("/", CreateCategory)
            .WithName("NewCategory")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Tworzy nową kategorię"
            })
            .RequireAuthorization("AdminPolicy");

        routes.MapDelete("/{categoryId:int}", DeleteCategory)
            .WithName("DeleteCategory")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Usuwa kategorię"
            })
            .RequireAuthorization("AdminPolicy");

        routes.MapPut("/{categoryId:int}", UpdateCategory)
            .WithName("UpdateCategory")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Aktualizuję kategorię"
            })
            .RequireAuthorization("AdminPolicy");
    }

    private static async Task<IResult> GetAllCategories(ILogger<Program> logger, IConfiguration configuration, 
        string? columnName = "id_category", string? orderBy = "ASC")
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Pobrano listę wszystkich kategorii.");

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            if (columnName == "idCategory")
            {
                columnName = "id_category";
            }

            var query = "SELECT * FROM categories ORDER BY " + columnName + " " + orderBy;
            await using var command = new NpgsqlCommand(query, connection);
            var categoriesQuery = await command.ExecuteReaderAsync();
            var categories = new List<CategoryModel>();
            while (categoriesQuery.Read())
            {
                categories.Add(new CategoryModel(
                    categoriesQuery.GetInt32(0), // idCategory
                    categoriesQuery.GetString(1) // name
                ));
            }
            return Results.Ok(categories);
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas pobierania listy kategorii: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas pobierania listy kategorii.");
        }
    }

    private static async Task<IResult> CreateCategory(ILogger<Program> logger, IConfiguration configuration,
        CategoryModel category)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Admin dodał nową kategorię: {categoryName}", category.Name);

        try
        {
            // Otwórz połączenie z bazą danych
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            // Przykładowe zapytanie SQL (INSERT)
            var query = @"
            INSERT INTO Categories (name)
            VALUES (@Name)";

            // Przygotowanie komendy z parametrami
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Name", category.Name);

            // Wykonanie zapytania
            var rowsAffected = await command.ExecuteNonQueryAsync();

            logger.LogInformation("Kategoria została zapisana w bazie danych. Wstawiono {rowsAffected} wierszy.",
                rowsAffected);
            return Results.Ok("Kategoria została utworzona pomyślnie.");
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas zapisywania kategorii: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas zapisywania kategorii.");
        }
    }

    private static async Task<IResult> DeleteCategory(ILogger<Program> logger, IConfiguration configuration, int categoryId)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");

        try
        {
            // Otwórz połączenie z bazą danych
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            // Zapytanie SQL (DELETE)
            var query = "DELETE FROM Categories WHERE id_category = @IdCategory";

            // Przygotowanie komendy z parametrami
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("IdCategory", categoryId);

            // Wykonanie zapytania
            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                logger.LogInformation("Kategoria o ID {categoryId} została usunięta.", categoryId);
                
                logger.LogInformation("Admin usunął kategorię o ID: {categoryId}", categoryId);
                return Results.Ok($"Kategoria o ID {categoryId} została usunięta.");
            }
            else
            {
                logger.LogWarning("Nie znaleziono kategorii o ID {categoryId}.", categoryId);
                return Results.NotFound($"Nie znaleziono kategorii o ID {categoryId}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas usuwania kategorii: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas usuwania kategorii.");
        }
    }

    private static async Task<IResult> UpdateCategory(ILogger<Program> logger, IConfiguration configuration,
      int categoryId, CategoryModel category)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Aktualizowanie nazwy kategorii o ID: {supplierId}", categoryId);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = @"
                        UPDATE categories
                        SET name = @Name
                        WHERE id_category = @IdCategory";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Name", category.Name);
            command.Parameters.AddWithValue("IdCategory", categoryId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                logger.LogInformation("Kategoria o ID {categoryId} została zaktualizowana.", categoryId);
                return Results.Ok($"Kategoria o ID {categoryId} została zaktualizowana.");
            }
            else
            {
                logger.LogWarning("Nie znaleziono kategorii o ID {categoryId}.", categoryId);
                return Results.NotFound($"Nie znaleziono dostawcy o ID {categoryId}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas aktualizacji dostawcy: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas aktualizacji dostawcy.");
        }
    }
}