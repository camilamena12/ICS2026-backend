using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ECommerce.IntegrationTests;

[Collection("IntegrationTests")]
public class ProductsIntegrationTests
{
	private readonly CustomWebApplicationFactory _factory;

	public ProductsIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;
	}

	private static object NewProductPayload(string sku, string name = "Mouse Gamer") => new
	{
		sku,
		internalCode = "INT-" + sku,
		name,
		description = "Producto creado en prueba de integración",
		currentUnitPrice = 15000m,
		stockQuantity = 10
	};

	// ---------------------------------------------------------------
	// Creación de productos (Admin)
	// ---------------------------------------------------------------

	[Fact]
	public async Task AddProduct_WhenUnauthenticatedOrNotAdmin_ReturnsUnauthorizedOrForbidden()
	{
		// ARRANGE
		var sku = "SKU-" + TestHelpers.Unique();

		// ACT 1: petición anónima (sin token)
		using var anonymous = _factory.CreateClient();
		var anonymousResponse = await anonymous.PostAsJsonAsync("/api/products", NewProductPayload(sku));

		// ACT 2: usuario autenticado pero con rol distinto de Admin
		using var user = _factory.CreateClient();
		user.Authorize(TestHelpers.CreateToken("User"));
		var userResponse = await user.PostAsJsonAsync("/api/products", NewProductPayload(sku));

		// ASSERT
		Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, userResponse.StatusCode);
	}

	[Fact]
	public async Task AddProduct_WhenAuthenticatedAsAdmin_PersistsInSqliteAndReturnsCreated()
	{
		// ARRANGE
		var sku = "SKU-" + TestHelpers.Unique();
		using var client = _factory.CreateClient();
		client.Authorize(TestHelpers.CreateToken("Admin"));

		// ACT
		var response = await client.PostAsJsonAsync("/api/products", NewProductPayload(sku));

		// ASSERT: respuesta HTTP
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);

		// ASSERT: la fila existe realmente en SQLite con los valores correctos
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
		var saved = await db.Set<Product>().AsNoTracking().FirstOrDefaultAsync(p => p.Sku == sku);

		Assert.NotNull(saved);
		Assert.Equal("Mouse Gamer", saved!.Name);
		Assert.Equal(15000m, saved.CurrentUnitPrice);
		Assert.Equal(10, saved.StockQuantity);
		Assert.True(saved.IsActive);
	}

	[Fact]
	public async Task AddProduct_WhenSkuAlreadyExistsInDatabase_ReturnsConflictStatusCode()
	{
		// ARRANGE: producto pre-insertado en la base
		var sku = "SKU-" + TestHelpers.Unique();
		await TestHelpers.SeedProductAsync(_factory, sku, "Producto existente");

		using var client = _factory.CreateClient();
		client.Authorize(TestHelpers.CreateToken("Admin"));

		// ACT: intentar crear otro con el mismo SKU
		var response = await client.PostAsJsonAsync("/api/products", NewProductPayload(sku, "Producto duplicado"));

		// ASSERT: no debe ser un 500; el middleware responde 409 o 400
		Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
		Assert.True(
			response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest,
			$"Se esperaba 409 o 400 pero se recibió {(int)response.StatusCode}");
	}

	// ---------------------------------------------------------------
	// Listado de productos
	// ---------------------------------------------------------------

	[Fact]
	public async Task GetProducts_PublicEndpoint_ReturnsOkWithPaginatedJsonStructure()
	{
		// ARRANGE: al menos un producto para evitar el 204 NoContent
		await TestHelpers.SeedProductAsync(_factory, "SKU-" + TestHelpers.Unique(), "Producto publico");
		using var client = _factory.CreateClient(); // sin credenciales

		// ACT
		var response = await client.GetAsync("/api/products");

		// ASSERT
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var root = json.RootElement;

		Assert.True(root.TryGetProperty("productItems", out var items));
		Assert.Equal(JsonValueKind.Array, items.ValueKind);
		// El contrato de paginación devuelve el total de elementos
		Assert.True(root.TryGetProperty("total", out var total));
		Assert.True(total.GetInt32() >= 1);
	}

	[Fact]
	public async Task GetProducts_WithQueryStringFilters_ReturnsFilteredResultsFromSqlite()
	{
		// ARRANGE: 3 productos con un sufijo único para aislar la búsqueda
		var tag = TestHelpers.Unique();
		await TestHelpers.SeedProductAsync(_factory, $"MOU-{tag}", $"Mouse Gamer {tag}");
		await TestHelpers.SeedProductAsync(_factory, $"TEC-{tag}", $"Teclado Mecanico {tag}");
		await TestHelpers.SeedProductAsync(_factory, $"MON-{tag}", $"Monitor {tag}");

		using var client = _factory.CreateClient();

        // ACT: búsqueda "mouse" (minúsculas) + paginación por query string
        var filtro = Uri.EscapeDataString($"gamer {tag}");
        var response = await client.GetAsync($"/api/products?search={filtro}&pageNumber=1&pageSize=10");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var items = json.RootElement.GetProperty("productItems");

		Assert.Equal(1, items.GetArrayLength());
		Assert.Equal($"Mouse Gamer {tag}", items[0].GetProperty("name").GetString());
	}

	[Fact]
	public async Task GetAuthProducts_AdminEndpoint_WhenCalledByAdmin_IncludesInactiveProducts()
	{
		// ARRANGE: producto inactivo en SQLite
		var tag = TestHelpers.Unique();
		var sku = $"INA-{tag}";
		await TestHelpers.SeedProductAsync(_factory, sku, $"Producto Inactivo {tag}", isActive: false);

		using var adminClient = _factory.CreateClient();
		adminClient.Authorize(TestHelpers.CreateToken("Admin"));
		using var publicClient = _factory.CreateClient();

		// ACT
		var adminResponse = await adminClient.GetAsync($"/api/products/admin?search={tag}");
		var publicResponse = await publicClient.GetAsync($"/api/products?search={tag}");

		// ASSERT: el endpoint admin lo incluye...
		Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
		using var json = JsonDocument.Parse(await adminResponse.Content.ReadAsStringAsync());
		var items = json.RootElement.GetProperty("productItems");
		Assert.Equal(1, items.GetArrayLength());
		Assert.Equal(sku, items[0].GetProperty("sku").GetString());
		Assert.False(items[0].GetProperty("isActive").GetBoolean());

		// ...y el público no (204 NoContent porque no hay productos activos que coincidan)
		Assert.Equal(HttpStatusCode.NoContent, publicResponse.StatusCode);
	}
}
