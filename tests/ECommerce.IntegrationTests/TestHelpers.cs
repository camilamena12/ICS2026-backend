using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.IntegrationTests;

/// <summary>
/// Utilidades compartidas por las pruebas de integración:
/// generación de tokens JWT y siembra de datos en SQLite.
/// </summary>
public static class TestHelpers
{
	// Deben coincidir con la configuración JWT de CustomWebApplicationFactory
	private const string JwtKey = "ClaveExclusivaDeTesting12345678901234567890";
	private const string Issuer = "Testing";
	private const string Audience = "Testing";

	/// <summary>Genera un token JWT válido con el rol indicado (Admin o User).</summary>
	public static string CreateToken(string role, string username = "usuario-test", Guid? customerId = null)
	{
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, username),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new Claim(ClaimTypes.Role, role),
			new Claim("customerId", customerId?.ToString() ?? "")
		};

		var token = new JwtSecurityToken(
			issuer: Issuer,
			audience: Audience,
			claims: claims,
			expires: DateTime.Now.AddMinutes(30),
			signingCredentials: creds);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	/// <summary>Agrega el header Authorization: Bearer al cliente HTTP.</summary>
	public static void Authorize(this HttpClient client, string token)
	{
		client.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
	}

	/// <summary>Inserta un producto directamente en la base SQLite.</summary>
	public static async Task<Product> SeedProductAsync(
		CustomWebApplicationFactory factory,
		string sku,
		string name,
		decimal price = 1000m,
		int stock = 10,
		bool isActive = true)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

		var product = new Product(sku, name, price, stock, "Producto de prueba", "INT-" + sku)
		{
			IsActive = isActive
		};

		db.Set<Product>().Add(product);
		await db.SaveChangesAsync();
		return product;
	}

	/// <summary>Inserta un cliente directamente en la base SQLite.</summary>
	public static async Task<Customer> SeedCustomerAsync(CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

		var id = Guid.NewGuid().ToString("N")[..8];
		var customer = new Customer
		{
			Email = $"cliente-{id}@test.com",
			Name = $"Cliente {id}"
		};

		db.Set<Customer>().Add(customer);
		await db.SaveChangesAsync();
		return customer;
	}

	/// <summary>Lee un producto de la base SQLite sin usar caché del contexto.</summary>
	public static async Task<Product?> FindProductAsync(CustomWebApplicationFactory factory, Guid id)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
		return await db.Set<Product>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
	}

	/// <summary>Sufijo único para evitar colisiones de datos entre pruebas.</summary>
	public static string Unique() => Guid.NewGuid().ToString("N")[..8];
}
