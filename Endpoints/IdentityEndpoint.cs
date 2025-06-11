using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WSB_project.Helpers;
using WSB_project.Models;

namespace WSB_project.Endpoints;

public static class IdentityEndpoint
{
    public static void MapIdentityEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/user", CreateUser)
            .WithName("CreateUser")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Tworzy nowego użytkownika"
            });

        routes.MapDelete("/user/{userId:int}", DeleteUser)
            .WithName("DeleteUser")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Usuwa użytkownika"
            });

        routes.MapPost("/role", CreateRole)
            .WithName("CreateRole")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Tworzy nową rolę"
            });

        routes.MapGet("/roles", GetAllRoles)
            .WithName("GetRoles")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Zwraca wszystkie dostępne role"
            })
            .Produces<List<RoleModel>>();

        routes.MapPost("/login", LoginUser)
            .WithName("LoginUser")
            .WithOpenApi(operation => new OpenApiOperation(operation)
            {
                Summary = "Loguje użytkownika i zwraca token JWT"
            });
    }

    // Tworzenie użytkownika
    private static async Task<IResult> CreateUser(ILogger<Program> logger, IConfiguration configuration, UserModel user)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = @"
        INSERT INTO Users (username, password, id_role)
        VALUES (@Username, @Password, @IdRole)";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Username", user.Username);
            var hashedPassword = PasswordHelper.HashPassword(user.Password);
            command.Parameters.AddWithValue("Password", hashedPassword);
            command.Parameters.AddWithValue("IdRole", user.IdRole);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            logger.LogInformation("Użytkownik {username} został utworzony.", user.Username);
            return Results.Ok("Użytkownik został utworzony.");
        }
        catch (Exception ex)
        {
            logger.LogError("Błąd podczas tworzenia użytkownika: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas tworzenia użytkownika.");
        }
    }


    // Usuwanie użytkownika
    private static async Task<IResult> DeleteUser(ILogger<Program> logger, IConfiguration configuration, int userId)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = "DELETE FROM Users WHERE id_user = @IdUser";
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("IdUser", userId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                logger.LogInformation("Użytkownik o ID {userId} został usunięty.", userId);
                return Results.Ok($"Użytkownik o ID {userId} został usunięty.");
            }

            return Results.NotFound($"Nie znaleziono użytkownika o ID {userId}.");
        }
        catch (Exception ex)
        {
            logger.LogError("Błąd podczas usuwania użytkownika: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas usuwania użytkownika.");
        }
    }

    // Tworzenie roli
    private static async Task<IResult> CreateRole(ILogger<Program> logger, IConfiguration configuration, RoleModel role)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = "INSERT INTO Roles (name) VALUES (@Name)";
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Name", role.Name);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            logger.LogInformation("Nowa rola {roleName} została utworzona.", role.Name);
            return Results.Ok("Rola została utworzona.");
        }
        catch (Exception ex)
        {
            logger.LogError("Błąd podczas tworzenia roli: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas tworzenia roli.");
        }
    }

    private static async Task<IResult> GetAllRoles(ILogger<Program> logger, IConfiguration configuration)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = "SELECT * FROM Roles";
            await using var command = new NpgsqlCommand(query, connection);

            await using var reader = await command.ExecuteReaderAsync();

            var roles = new List<RoleModel>();
            while (await reader.ReadAsync())
            {
                roles.Add(new RoleModel(
                    reader.GetInt32(0), // IdRole
                    reader.GetString(1)  // Name
                ));
            }

            return Results.Ok(roles);
        }
        catch (Exception ex)
        {
            logger.LogError("Błąd podczas pobierania ról: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas pobierania ról.");
        }
    }

    // Logowanie użytkownika i generowanie tokena JWT
    private static async Task<IResult> LoginUser(ILogger<Program> logger, IConfiguration configuration, [FromBody] LoginModel loginRequest)
    {
        var connString = configuration.GetConnectionString("DefaultConnection");

        try
        {
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();

            var query = @"
                SELECT u.id_user, u.username, u.password, r.name AS role
                FROM users u
                JOIN roles r ON u.id_role = r.id_role
                WHERE u.username = @Username";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("Username", loginRequest.Username);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync()) return Results.Unauthorized();
            
            var userId = reader.GetInt32(0);
            var role = reader.GetString(1);
            var passwordHash = reader.GetString(2);

            // Weryfikacja hasła
            if (!BCrypt.Net.BCrypt.Verify(loginRequest.Password, passwordHash))
            {
                return Results.Unauthorized();
            }

            // Generowanie tokena JWT
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtSettings:SecretKey"]
                                                                      ?? throw new ArgumentNullException()));
            
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer: configuration["JwtSettings:Issuer"],
                audience: configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(configuration["JwtSettings:ExpirationMinutes"])),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Results.Ok(new { Token = tokenString });
        }
        catch (Exception ex)
        {
            logger.LogError("Błąd podczas logowania użytkownika: {message}", ex.Message);
            return Results.Problem("Wystąpił błąd podczas logowania.");
        }
    }


}
