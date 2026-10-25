
using Dsw2025Tpi.Api.NewFolder;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Data.Helper;
using Dsw2025Tpi.Data.Repositories;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

namespace Dsw2025Tpi.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configuración de logs
        builder.Services.AddLogging(config =>
        {
            config.ClearProviders();
            config.AddConsole();

            var path = builder.Configuration.GetValue<string>("LogPath");
            if (!string.IsNullOrEmpty(path))
            {
                config.AddFile(path);
            }
        });

        builder.Services.AddControllers().AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Desarollo de software TPI",
                Version = "v1",
            });

            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Ingrese el token",
                Type = SecuritySchemeType.ApiKey
            });

            o.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                    Array.Empty<string>()
                }
            });
        });

        builder.Services.AddHealthChecks();

        builder.Services.AddDbContext<AuthenticateContext>(options =>
        {
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("Dsw2025TpiEntities"));
        });

        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password = new PasswordOptions
            {
                RequiredLength = 8
            };
        })
        .AddEntityFrameworkStores<AuthenticateContext>()
        .AddDefaultTokenProviders()
        .AddErrorDescriber<SpanishIdentityErrorDescriber>();

        // Configuración JWT
        var jwtConfig = builder.Configuration.GetSection("Jwt");
        var keyText = jwtConfig["Key"]
            ?? throw new ArgumentNullException("JWT Key");

        var key = Encoding.UTF8.GetBytes(keyText);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme =
                JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme =
                JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtConfig["Issuer"],
                ValidAudience = jwtConfig["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };
        });

        builder.Services.AddDbContext<Dsw2025TpiContext>(options =>
        {
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("Dsw2025TpiEntities"));
        });

        builder.Services.AddSingleton<JwtTokenService>();
        builder.Services.AddAuthorization();

        builder.Services.AddScoped<IRepository, EfRepository>();
        builder.Services.AddScoped<ProductsManagementService>();
        builder.Services.AddScoped<OrdersManagementService>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        var app = builder.Build();

        // Crear roles y configurar el administrador
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;

            try
            {
                var userManager =
                    services.GetRequiredService<UserManager<IdentityUser>>();

                var roleManager =
                    services.GetRequiredService<RoleManager<IdentityRole>>();

                const string adminRole = "Admin";
                const string userRole = "User";

                if (!await roleManager.RoleExistsAsync(adminRole))
                {
                    var result = await roleManager.CreateAsync(
                        new IdentityRole(adminRole));

                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "No se pudo crear el rol Admin: " +
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                    }

                    Console.WriteLine($"Rol '{adminRole}' creado.");
                }

                if (!await roleManager.RoleExistsAsync(userRole))
                {
                    var result = await roleManager.CreateAsync(
                        new IdentityRole(userRole));

                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "No se pudo crear el rol User: " +
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                    }

                    Console.WriteLine($"Rol '{userRole}' creado.");
                }

                // Las credenciales se configuran en las variables de entorno.
                var adminUsername = builder.Configuration["ADMIN_USERNAME"];
                var adminEmail = builder.Configuration["ADMIN_EMAIL"];
                var adminPassword = builder.Configuration["ADMIN_PASSWORD"];

                if (!string.IsNullOrWhiteSpace(adminUsername) &&
                    !string.IsNullOrWhiteSpace(adminEmail) &&
                    !string.IsNullOrWhiteSpace(adminPassword))
                {
                    var adminUser =
                        await userManager.FindByNameAsync(adminUsername);

                    if (adminUser == null)
                    {
                        adminUser = new IdentityUser
                        {
                            UserName = adminUsername,
                            Email = adminEmail,
                            EmailConfirmed = true
                        };

                        var createResult = await userManager.CreateAsync(
                            adminUser,
                            adminPassword);

                        if (!createResult.Succeeded)
                        {
                            Console.WriteLine(
                                $"Error al crear admin '{adminUsername}':");

                            foreach (var error in createResult.Errors)
                            {
                                Console.WriteLine($"- {error.Description}");
                            }
                        }
                        else
                        {
                            var roleResult = await userManager.AddToRoleAsync(
                                adminUser, adminRole);

                            if (!roleResult.Succeeded)
                            {
                                Console.WriteLine(
                                    "No se pudo asignar el rol Admin: " +
                                    string.Join("; ",
                                        roleResult.Errors.Select(e => e.Description)));
                            }
                            else
                            {
                                Console.WriteLine(
                                    $"Administrador '{adminUsername}' creado correctamente.");
                            }
                        }
                    }
                    else
                    {
                        if (!await userManager.IsInRoleAsync(adminUser, adminRole))
                        {
                            var roleResult = await userManager.AddToRoleAsync(
                                adminUser, adminRole);

                            if (!roleResult.Succeeded)
                            {
                                Console.WriteLine(
                                    "No se pudo asignar el rol Admin: " +
                                    string.Join("; ",
                                        roleResult.Errors.Select(e => e.Description)));
                            }
                            else
                            {
                                Console.WriteLine(
                                    $"Rol Admin asignado a '{adminUsername}'.");
                            }
                        }
                        else
                        {
                            Console.WriteLine(
                                $"El administrador '{adminUsername}' ya existe.");
                        }
                    }
                }
                else
                {
                    Console.WriteLine(
                        "No se configuraron las variables de entorno del administrador.");
                }

                // Conservar la carga inicial de clientes.
                var context =
                    services.GetRequiredService<Dsw2025TpiContext>();

                context.Seedwork<Customer>("customers.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Error durante la carga de roles, administrador o clientes:");
                Console.WriteLine(ex);
            }
        }

        // Configuración del pipeline HTTP
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowFrontend");

        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/healthcheck");

        app.Run();
    }
}
