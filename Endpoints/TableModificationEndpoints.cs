using Microsoft.OpenApi.Models;
using Npgsql;
using WSB_project.Enums;

namespace WSB_project.Endpoints;

public static class TableModificationEndpoints
{


    public static void MapTableModificationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/modify", ModifyTable)
            .WithName("ModifyTable")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Modyfikuje strukturę tabeli (dodawanie/usuwanie/zmiana nazwy kolumny)"
            });
    }

    private static async Task<IResult> ModifyTable(ILogger<Program> logger, IConfiguration configuration, string tableName, TableModificationOperationEnum operation, string columnName, string? newColumnName = null, string? columnType = null)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Modyfikowanie tabeli: {tableName}, operacja: {operation}, kolumna: {columnName}", tableName, operation, columnName);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            string query = operation switch
            {
                TableModificationOperationEnum.Add => $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}",
                TableModificationOperationEnum.Drop => $"ALTER TABLE {tableName} DROP COLUMN {columnName}",
                TableModificationOperationEnum.Rename => $"ALTER TABLE {tableName} RENAME COLUMN {columnName} TO {newColumnName}",
                _ => throw new ArgumentException("Nieznana operacja. Dozwolone operacje to: Add, Drop, Rename.")
            };

            await using var command = new NpgsqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();

            logger.LogInformation("Operacja {operation} na tabeli {tableName} zakończona sukcesem.", operation, tableName);
            return Results.Ok($"Operacja {operation} na tabeli {tableName} zakończona sukcesem.");
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas modyfikacji tabeli: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas modyfikacji tabeli.");
        }
    }
}
