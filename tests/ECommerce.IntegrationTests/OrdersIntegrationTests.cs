using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ECommerce.IntegrationTests;

[Collection("IntegrationTests")]
public class OrdersIntegrationTests
{
	private readonly CustomWebApplicationFactory _factory;

	public OrdersIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;
	}

	private static object NewOrderPayload(Guid customerId, Guid productId, int quantity) => new
	{
		customerId,
		shippingAddress = "Calle Falsa 123",
		billingAddress = "Calle Falsa 123",
		notes = "Orden de prueba de integración",
		items = new[] { new { productId, quantity } }
	};

	[Fact]
	public async Task CreateOrder_WhenUnauthenticated_ReturnsUnauthorized()
	{
		// ARRANGE
		using var client = _factory.CreateClient(); // sin token
		var payload = NewOrderPayload(Guid.NewGuid(), Guid.NewGuid(), 1);

		// ACT
		var response = await client.PostAsJsonAsync("/api/orders", payload);

		// ASSERT
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task CreateOrder_WhenAuthenticatedWithValidData_ReturnsCreatedAndPersistsOrderAndStockInSqlite()
	{
		// ARRANGE: cliente y producto con stock inicial 10 en SQLite
		var customer = await TestHelpers.SeedCustomerAsync(_factory);
		var product = await TestHelpers.SeedProductAsync(
			_factory, "ORD-" + TestHelpers.Unique(), "Mouse Gamer", price: 15000m, stock: 10);

		using var client = _factory.CreateClient();
		client.Authorize(TestHelpers.CreateToken("User", customerId: customer.Id));

		// ACT: comprar 3 unidades
		var response = await client.PostAsJsonAsync(
			"/api/orders", NewOrderPayload(customer.Id, product.Id, 3));

		// ASSERT: respuesta HTTP
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		Assert.NotNull(response.Headers.Location);

		// ASSERT: la orden se guardó en SQLite con el total correcto
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
		var order = await db.Set<Order>().AsNoTracking()
			.FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

		Assert.NotNull(order);
		Assert.Equal(45000m, order!.TotalAmount);

		// ASSERT: el stock del producto bajó de 10 a 7
		var updated = await TestHelpers.FindProductAsync(_factory, product.Id);
		Assert.NotNull(updated);
		Assert.Equal(7, updated!.StockQuantity);
	}

	[Fact]
	public async Task CreateOrder_WhenStockIsInsufficient_ReturnsBadRequestWithErrorMessage()
	{
		// ARRANGE: producto con stock 2, se piden 5
		var customer = await TestHelpers.SeedCustomerAsync(_factory);
		var product = await TestHelpers.SeedProductAsync(
			_factory, "ORD-" + TestHelpers.Unique(), "Teclado", price: 8000m, stock: 2);

		using var client = _factory.CreateClient();
		client.Authorize(TestHelpers.CreateToken("User", customerId: customer.Id));

		// ACT
		var response = await client.PostAsJsonAsync(
			"/api/orders", NewOrderPayload(customer.Id, product.Id, 5));

		// ASSERT: 400 con un mensaje que indica la falta de stock
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		var body = await response.Content.ReadAsStringAsync();
		Assert.Contains("stock", body, StringComparison.OrdinalIgnoreCase);

		// ASSERT: el stock no se modificó
		var unchanged = await TestHelpers.FindProductAsync(_factory, product.Id);
		Assert.Equal(2, unchanged!.StockQuantity);
	}
}
