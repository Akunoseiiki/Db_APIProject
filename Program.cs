using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using WSB_project.Endpoints;
using WSB_project.Models;

namespace WSB_project;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Konfiguracja pochodząca z appsettings.json
        builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Konfiguracja logowania
        builder.Services.AddLogging(p => p.AddConsole());

        // Dodanie usług
        builder.Services.AddAuthorization();

        // Konfiguracja JWT
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"];
        var key = Encoding.ASCII.GetBytes(secretKey);

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                };
            });

        // CORS - allow all origins - pozwala na łączenie z dowolnego adresu ip
        builder.Services.AddCors(options =>
            options.AddPolicy("AllowAll",
                builder =>
                {
                    builder.AllowAnyOrigin() // Pozwól na żądania z dowolnego źródła
                        .AllowAnyMethod() // Pozwól na dowolną metodę (GET, POST, itd.)
                        .AllowAnyHeader(); // Pozwól na dowolne nagłówki
                }));

        // Swagger/OpenAPI
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] {}
                }
            });
        });

        builder.Services.AddAuthorization(options =>
        {
            // Dodanie polityki admina, uwzględniającej rolę z niestandardowego claim
            options.AddPolicy("AdminPolicy", policy =>
                policy.RequireClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "admin"));
        });

        var connString = builder.Configuration.GetConnectionString("DefaultConnection");

        var app = builder.Build();

        app.UseCors("AllowAll");

        // Konfiguracja swaggera
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Middleware dla autoryzacji
        app.UseAuthentication(); // Dodaj middleware autoryzacji
        app.UseAuthorization();

        app.MapGroup("/categories")
            .WithTags("categories")
            .MapCategoryEndpoints();

        app.MapGroup("/orders")
            .WithTags("orders")
            .MapOrderEndpoints();

        app.MapGroup("/products")
            .WithTags("products")
            .MapProductEndpoints();

        app.MapGroup("/suppliers")
            .WithTags("suppliers")
            .MapSupplierEndpoints();

        app.MapGroup("/table")
            .WithTags("table")
            .MapTableModificationEndpoints();

        app.MapGroup("/identity")
            .WithTags("identity")
            .MapIdentityEndpoint();

        app.Run();
    }
}
