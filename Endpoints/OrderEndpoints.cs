using System.Data;
using Microsoft.OpenApi.Models;
using Npgsql;
using NpgsqlTypes;
using WSB_project.Models;

namespace WSB_project.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/", GetAllOrders)
            .WithName("GetOrders")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca listę zamówień"
            })
            .Produces<List<OrderModel>>();

        routes.MapGet("/{columnName}/{orderBy}", GetAllOrders)
            .WithName("GetOrdersWithParams")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca listę zamówień"
            })
            .Produces<List<OrderModel>>();

        routes.MapPost("/", CreateOrder)
            .WithName("NewOrder")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Tworzy nowe zamówienie"
            });

        routes.MapDelete("/{orderId:int}", DeleteOrder)
            .WithName("DeleteOrder")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Usuwa zamówienie"
            });

        routes.MapPut("/{orderId:int}", UpdateOrder)
            .WithName("UpdateOrder")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Aktualizuje zamówienie"
            });
    }

    private static async Task<IResult> GetAllOrders(ILogger<Program> logger, IConfiguration configuration,
        string? columnName = "id_order", string? orderBy = "ASC")
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Pobrano listę wszystkich zamówień.");

        try
        {
            var orders = new List<OrderModel>();

            await using (var connection = new NpgsqlConnection(connString))
            {
                await connection.OpenAsync();

                // Włącz sortowanie w zapytaniu SQL
                var orderQuery = $@"
                SELECT * FROM Orders 
                ORDER BY {columnName} {orderBy}";

                await using (var orderCommand = new NpgsqlCommand(orderQuery, connection))
                {
                    await using (var orderReader = await orderCommand.ExecuteReaderAsync())
                    {
                        while (await orderReader.ReadAsync())
                        {
                            var idOrder = orderReader.GetInt32(0);
                            var orderDate = orderReader.GetDateTime(1);
                            var firstName = orderReader.GetString(2);
                            var lastName = orderReader.GetString(3);
                            var city = orderReader.GetString(4);
                            var country = orderReader.GetString(5);
                            var address = orderReader.GetString(6);
                            var postalCode = orderReader.GetString(7);
                            var email = orderReader.GetString(8);
                            var phone = orderReader.GetString(9);

                            // Create an order instance without products initially
                            orders.Add(new OrderModel(
                                idOrder,
                                orderDate,
                                firstName,
                                lastName,
                                city,
                                country,
                                address,
                                postalCode,
                                email,
                                phone,
                                new List<ProductInOrderModel>()
                            ));
                        }
                    }
                }
            }

            // Fetch products for each order
            foreach (var order in orders)
            {
                await using (var connection = new NpgsqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var productQuery = @"
                SELECT op.id_product, p.name, op.quantity 
                FROM orders_products op
                JOIN products p ON op.id_product = p.id_product
                WHERE op.id_order = @IdOrder";

                    await using (var productCommand = new NpgsqlCommand(productQuery, connection))
                    {
                        productCommand.Parameters.AddWithValue("IdOrder", order.IdOrder);

                        await using (var productReader = await productCommand.ExecuteReaderAsync())
                        {
                            var products = new List<ProductInOrderModel>();
                            while (await productReader.ReadAsync())
                            {
                                products.Add(new ProductInOrderModel(
                                    productReader.GetInt32(0), // id_product
                                    productReader.GetString(1), // product_name
                                    productReader.GetInt32(2) // quantity
                                ));
                            }

                            order.Products = products; // Assign the products to the order
                        }
                    }
                }
            }

            return Results.Ok(orders);
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas pobierania listy zamówień: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas pobierania listy zamówień.");
        }
    }

    private static async Task<IResult> CreateOrder(ILogger<Program> logger, IConfiguration configuration,
        OrderModel order)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Admin inicjuje tworzenie nowego zamówienia dla osoby: {imie}, {nazwisko}",
            order.FirstName, order.LastName);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            // Przygotowanie do wywołania procedury składowanej
            // Nazwa procedury musi dokładnie odpowiadać tej w bazie danych
            await using var cmd = new NpgsqlCommand("sp_create_order_with_products", connection)
            {
                // Określamy, że chcemy wywołać procedurę składowaną.
                // Alternatywnie, można użyć CommandType.Text i napisać:
                // cmd.CommandText = "SELECT * FROM sp_create_order_with_products(@p_firstname, @p_lastname, ...);";
                // Wtedy ExecuteScalarAsync() by działało bezpośrednio do odczytu new_order_id.
                // Dla CommandType.StoredProcedure z parametrem OUT, wartość odczytujemy inaczej.
                CommandType = CommandType.StoredProcedure
            };

            // Dodawanie parametrów do procedury składowanej
            cmd.Parameters.AddWithValue("p_firstname", order.FirstName);
            cmd.Parameters.AddWithValue("p_lastname", order.LastName);
            cmd.Parameters.AddWithValue("p_city", order.City);
            cmd.Parameters.AddWithValue("p_country", order.Country);
            cmd.Parameters.AddWithValue("p_address", order.Address);
            cmd.Parameters.AddWithValue("p_postalcode", order.PostalCode);
            cmd.Parameters.AddWithValue("p_email", order.Email);
            cmd.Parameters.AddWithValue("p_phone", order.Phone);

            // Przygotowanie tablic ID produktów i ilości
            var productIds = order.Products.Select(p => p.IdProduct).ToArray();
            var quantities = order.Products.Select(p => p.Quantity).ToArray();

            // Dodawanie parametrów tablicowych
            // Ważne jest, aby określić typ NpgsqlDbType.Array w połączeniu z typem elementów tablicy
            cmd.Parameters.AddWithValue("p_product_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer, productIds);
            cmd.Parameters.AddWithValue("p_quantities", NpgsqlDbType.Array | NpgsqlDbType.Integer, quantities);

            // Definicja parametru wyjściowego (OUT) dla new_order_id
            // Nazwa "new_order_id" musi pasować do nazwy parametru OUT w definicji funkcji PostgreSQL
            var outParamNewOrderId = new NpgsqlParameter("new_order_id", NpgsqlDbType.Integer)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outParamNewOrderId);

            // Wykonanie procedury składowanej
            // Ponieważ procedura ma efekt uboczny (wstawia dane) i nie zwraca wyniku przez SELECT, używamy ExecuteNonQueryAsync.
            // Wartość parametru OUT zostanie wypełniona po wykonaniu.
            await cmd.ExecuteNonQueryAsync();

            // Odczytanie wartości z parametru wyjściowego
            var newOrderId = (int)outParamNewOrderId.Value;

            logger.LogInformation(
                "Zamówienie zostało zapisane w bazie danych z ID {newOrderId} przy użyciu procedury składowanej.",
                newOrderId);
            return Results.Ok($"Zamówienie zostało utworzone pomyślnie z ID: {newOrderId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Wystąpił ogólny błąd podczas zapisywania zamówienia: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas zapisywania zamówienia.");
        }
    }

//usuwanie zamówienia
    private static async Task<IResult> DeleteOrder(ILogger<Program> logger, IConfiguration configuration, int orderId)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Admin zainicjował usuwanie zamówienie o ID: {orderId}", orderId);

        try
        {
            // Otwórz połączenie z bazą danych
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            // Zapytanie SQL (DELETE)
            var query = "DELETE FROM Orders WHERE id_order = @IdOrder";

            // Przygotowanie komendy z parametrami
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("IdOrder", orderId);

            // Wykonanie zapytania
            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                logger.LogInformation("Zamówienie o ID {orderId} zostało usunięte.", orderId);
                return Results.Ok($"Zamówienie o ID {orderId} zostało usunięte.");
            }
            else
            {
                logger.LogWarning("Nie znaleziono zamówienia o ID {orderId}.", orderId);
                return Results.NotFound($"Nie znaleziono zamówienia o ID {orderId}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas usuwania zamówienia: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas usuwania zamówienia.");
        }
    }

    private static async Task<IResult> UpdateOrder(ILogger<Program> logger, IConfiguration configuration,
        int orderId, OrderModel order)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("Aktualizowanie zamówienia o ID: {orderId}", orderId);

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            // Zaktualizowanie danych zamówienia
            var query = @"
                    UPDATE orders
                    SET firstname = @Firstname,
                        lastname = @Lastname,
                        city = @City,
                        country = @Country,
                        address = @Address,
                        postalcode = @PostalCode,
                        email = @Email,
                        phone = @Phone
                    WHERE id_order = @IdOrder";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Firstname", order.FirstName);
            command.Parameters.AddWithValue("Lastname", order.LastName);
            command.Parameters.AddWithValue("City", order.City);
            command.Parameters.AddWithValue("Country", order.Country);
            command.Parameters.AddWithValue("Address", order.Address);
            command.Parameters.AddWithValue("PostalCode", order.PostalCode);
            command.Parameters.AddWithValue("Email", order.Email);
            command.Parameters.AddWithValue("Phone", order.Phone);
            command.Parameters.AddWithValue("IdOrder", orderId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                logger.LogWarning("Nie znaleziono zamówienia o ID {orderId}.", orderId);
                return Results.NotFound($"Nie znaleziono zamówienia o ID {orderId}.");
            }

            // Teraz aktualizujemy produkty w zamówieniu
            // Najpierw usuń wszystkie produkty z zamówienia
            var deleteQuery = "DELETE FROM orders_products WHERE id_order = @IdOrder";

            await using (var deleteCommand = new NpgsqlCommand(deleteQuery, connection))
            {
                deleteCommand.Parameters.AddWithValue("IdOrder", orderId);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            // Następnie dodaj nowe produkty lub zaktualizuj istniejące
            foreach (var product in order.Products)
            {
                var insertQuery = @"
            INSERT INTO orders_products (id_order, id_product, quantity)
            VALUES (@IdOrder, @IdProduct, @Quantity)";

                await using var insertCommand = new NpgsqlCommand(insertQuery, connection);
                insertCommand.Parameters.AddWithValue("IdOrder", orderId);
                insertCommand.Parameters.AddWithValue("IdProduct", product.IdProduct);
                insertCommand.Parameters.AddWithValue("Quantity", product.Quantity);

                await insertCommand.ExecuteNonQueryAsync();
            }

            logger.LogInformation("Zamówienie o ID {orderId} zostało zaktualizowane.", orderId);
            return Results.Ok($"Zamówienie o ID {orderId} zostało zaktualizowane.");
        }
        catch (Exception ex)
        {
            logger.LogError("Wystąpił błąd podczas aktualizacji zamówienia: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas aktualizacji zamówienia.");
        }
    }
}