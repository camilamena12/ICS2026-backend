using Microsoft.Extensions.Hosting;
using Dsw2025Tpi.Api;
using Dsw2025Tpi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ECommerce.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
	// Bases de datos temporales, separadas para negocio y autenticación
	private readonly SqliteConnection _businessConnection =
		new("Data Source=:memory:");

	private readonly SqliteConnection _authConnection =
		new("Data Source=:memory:");

	public CustomWebApplicationFactory()
	{
		_businessConnection.Open();
		_authConnection.Open();
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		// Evita la inicialización habitual de la API
		builder.UseEnvironment("Testing");

		// Configuración JWT exclusiva de pruebas
		builder.ConfigureAppConfiguration((context, config) =>
		{
			config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Jwt:Key"] = "ClaveExclusivaDeTesting12345678901234567890",
				["Jwt:Issuer"] = "Testing",
				["Jwt:Audience"] = "Testing"
			});
		});

		builder.ConfigureTestServices(services =>
		{
			// Quitar la configuración original de SQL Server
			services.RemoveAll<DbContextOptions<Dsw2025TpiContext>>();
			services.RemoveAll<IDbContextOptionsConfiguration<Dsw2025TpiContext>>();

			services.RemoveAll<DbContextOptions<AuthenticateContext>>();
			services.RemoveAll<IDbContextOptionsConfiguration<AuthenticateContext>>();

			// Configurar SQLite para los datos del negocio
			services.AddDbContext<Dsw2025TpiContext>(options =>
				options.UseSqlite(_businessConnection));

			// Configurar SQLite para usuarios y roles
			services.AddDbContext<AuthenticateContext>(options =>
				options.UseSqlite(_authConnection));
		});
	}

	protected override IHost CreateHost(IHostBuilder builder)
	{
		// Inicia la API en el entorno de Testing
		var host = base.CreateHost(builder);

		// Obtiene los servicios de la aplicación
		using var scope = host.Services.CreateScope();

		// Contexto de productos, clientes y órdenes
		var businessContext = scope.ServiceProvider
			.GetRequiredService<Dsw2025TpiContext>();

		// Contexto de usuarios y roles
		var authContext = scope.ServiceProvider
			.GetRequiredService<AuthenticateContext>();

		// Crear las tablas de las bases temporales
		businessContext.Database.EnsureCreated();
		authContext.Database.EnsureCreated();

		return host;
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			_businessConnection.Dispose();
			_authConnection.Dispose();
		}
	}
}