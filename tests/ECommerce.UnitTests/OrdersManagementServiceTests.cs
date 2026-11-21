
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Dsw2025Tpi.Application.Exceptions;


namespace ECommerce.UnitTests;

public class OrdersManagementServiceTests
{
    [Fact]
    public async Task CreateOrder_WithValidItemsAndStock_CalculatesTotalAndDecrementsProductStock()
    {
        // ARRANGE: Preparar los datos y los mocks

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<OrdersManagementService>>();

        var cliente = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Juan Prueba",
            Email = "juan@example.com"
        };

        var producto = new Product(
            "MOUSE-001",
            "Mouse Gamer",
            15000m,
            10,
            "Mouse para gaming",
            "INT-001"
        );

        var request = new OrderRequest(
            CustomerId: cliente.Id,
            ShippingAddress: "San Miguel de Tucuman",
            BillingAddress: "San Miguel de Tucuman",
            Notes: "Orden de prueba",
            Items: new List<OrderItemRequest>
            {
                new OrderItemRequest(producto.Id, 3)
            }
        );

        // Simulamos que el cliente existe
        repositoryMock
            .Setup(r => r.GetById<Customer>(
                cliente.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(cliente);

        // Simulamos que el producto existe
        repositoryMock
            .Setup(r => r.GetById<Product>(
                producto.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(producto);

        // Simulamos la actualización del stock
        repositoryMock
            .Setup(r => r.Update(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);

        // Simulamos el guardado de la orden
        repositoryMock
            .Setup(r => r.Add(It.IsAny<Order>()))
            .ReturnsAsync((Order o) => o);

        var service = new OrdersManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        // ACT: Crear la orden

        var resultado = await service.CreateOrder(request);

        // ASSERT: Verificar resultados

        Assert.NotNull(resultado);

        // 3 unidades x $15000 = $45000
        Assert.Equal(45000m, resultado.TotalAmount);

        // Stock inicial 10 - 3 compradas = 7
        Assert.Equal(999, producto.StockQuantity);

        // Comprobar los datos de la orden
        var item = Assert.Single(resultado.Items);
        Assert.Equal(producto.Id, item.ProductId);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(15000m, item.UnitPrice);

        // El repositorio debe actualizar el producto una vez
        repositoryMock.Verify(
            r => r.Update(It.IsAny<Product>()),
            Times.Once
        );

        // El repositorio debe guardar la orden una vez
        repositoryMock.Verify(
            r => r.Add(It.IsAny<Order>()),
            Times.Once
        );
    }
    //UT-05

    [Fact]
    public async Task CreateOrder_WhenProductStockIsInsufficient_ThrowsInsufficientStockException()
    {
        // ARRANGE: Preparar los datos

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<OrdersManagementService>>();

        var cliente = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Juan Prueba",
            Email = "juan@example.com"
        };

        // Producto con solamente 2 unidades
        var producto = new Product(
            "MOUSE-001",
            "Mouse Gamer",
            15000m,
            2,
            "Mouse para gaming",
            "INT-001"
        );

        // Intentamos comprar 5 unidades
        var request = new OrderRequest(
            CustomerId: cliente.Id,
            ShippingAddress: "San Miguel de Tucuman",
            BillingAddress: "San Miguel de Tucuman",
            Notes: "Orden sin stock suficiente",
            Items: new List<OrderItemRequest>
            {
            new OrderItemRequest(producto.Id, 5)
            }
        );

        // Simulamos que el cliente existe
        repositoryMock
            .Setup(r => r.GetById<Customer>(
                cliente.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(cliente);

        // Simulamos que el producto tiene solo 2 unidades
        repositoryMock
            .Setup(r => r.GetById<Product>(
                producto.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(producto);

        var service = new OrdersManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        // ACT Y ASSERT: Verificar la excepción esperada
        await Assert.ThrowsAsync<InsufficientStockException>(
            () => service.CreateOrder(request)
        );

        // ASSERT: El stock NO debe modificarse
        Assert.Equal(2, producto.StockQuantity);

        // El producto NO debe actualizarse
        repositoryMock.Verify(
            r => r.Update(It.IsAny<Product>()),
            Times.Never
        );

        // La orden NO debe guardarse
        repositoryMock.Verify(
            r => r.Add(It.IsAny<Order>()),
            Times.Never
        );
    }
    //ut-06

    [Fact]
    public async Task CreateOrder_WhenCustomerDoesNotExist_ThrowsCustomerNotFoundException()
    {
        // ARRANGE: Preparar los datos

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<OrdersManagementService>>();

        // Simulamos un ID de cliente inexistente
        var clienteId = Guid.NewGuid();
        var productoId = Guid.NewGuid();

        var request = new OrderRequest(
            CustomerId: clienteId,
            ShippingAddress: "San Miguel de Tucuman",
            BillingAddress: "San Miguel de Tucuman",
            Notes: "Orden con cliente inexistente",
            Items: new List<OrderItemRequest>
            {
            new OrderItemRequest(productoId, 2)
            }
        );

        // Simulamos que el cliente NO existe
        repositoryMock
            .Setup(r => r.GetById<Customer>(
                clienteId,
                It.IsAny<string[]>()))
            .ReturnsAsync((Customer?)null);

        var service = new OrdersManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        // ACT Y ASSERT: Verificar que lance la excepción

        await Assert.ThrowsAsync<CustomerNotFoundException>(
            () => service.CreateOrder(request)
        );

        // ASSERT: Verificar que consultó al cliente
        repositoryMock.Verify(
            r => r.GetById<Customer>(
                clienteId,
                It.IsAny<string[]>()),
            Times.Once
        );

        // No debe consultar productos
        repositoryMock.Verify(
            r => r.GetById<Product>(
                It.IsAny<Guid>(),
                It.IsAny<string[]>()),
            Times.Never
        );

        // No debe modificar el stock
        repositoryMock.Verify(
            r => r.Update(It.IsAny<Product>()),
            Times.Never
        );

        // No debe guardar ninguna orden
        repositoryMock.Verify(
            r => r.Add(It.IsAny<Order>()),
            Times.Never
        );
    }


}
