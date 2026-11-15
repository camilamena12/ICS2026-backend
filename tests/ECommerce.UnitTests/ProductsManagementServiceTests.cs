
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Domain.Interfaces;
using Dsw2025Tpi.Domain.Entities;
using System.Linq.Expressions;
using Dsw2025Tpi.Application.Exceptions;


namespace ECommerce.UnitTests;

public class ProductsManagementServiceTests
{
    [Fact]
    public async Task AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd()
    {
        // ARRANGE: Preparar los datos y las dependencias

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<ProductsManagementService>>();

        var request = new ProductModel.Request(
            Sku: "MOUSE-001",
            InternalCode: "INT-001",
            Name: "Mouse Gamer",
            Description: "Mouse para gaming",
            CurrentUnitPrice: 15000m,
            StockQuantity: 10
        );

        // Simulamos que el SKU todavía no existe
        repositoryMock
            .Setup(r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((Product?)null);

        // Simulamos que el repositorio agrega el producto
        repositoryMock
            .Setup(r => r.Add(It.IsAny<Product>()))
            .ReturnsAsync((Product product) => product);

        var service = new ProductsManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        // ACT: Ejecutar el método que queremos probar

        var resultado = await service.AddProduct(request);

        // ASSERT: Comprobar los resultados esperados

        Assert.NotNull(resultado);
        Assert.Equal("MOUSE-001", resultado.Sku);
        Assert.Equal("Mouse Gamer", resultado.Name);
        Assert.Equal(15000m, resultado.CurrentUnitPrice);
        Assert.Equal(10, resultado.StockQuantity);

        repositoryMock.Verify(
            r => r.Add(It.IsAny<Product>()),
            Times.Once
        );
    }
    //UT-02

    [Fact]
    public async Task AddProduct_WithDuplicateSku_ThrowsDuplicatedEntityException()
    {
        // ARRANGE: Preparar los datos

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<ProductsManagementService>>();

        // Producto que ya existe
        var productoExistente = new Product(
            "MOUSE-001",
            "Mouse Gamer",
            15000m,
            10,
            "Mouse para gaming",
            "INT-001"
        );

        // Nuevo producto con el mismo SKU
        var request = new ProductModel.Request(
            Sku: "MOUSE-001",
            InternalCode: "INT-002",
            Name: "Otro Mouse",
            Description: "Producto duplicado",
            CurrentUnitPrice: 20000m,
            StockQuantity: 5
        );

        // Simulamos que el repositorio encuentra el SKU
        repositoryMock
            .Setup(r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(productoExistente);

        var service = new ProductsManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        // ACT Y ASSERT: Verificar que lance la excepción
        await Assert.ThrowsAsync<DuplicatedEntityException>(
            () => service.AddProduct(request)
        );

        // ASSERT: Verificar que NO se agregue el producto
        repositoryMock.Verify(
            r => r.Add(It.IsAny<Product>()),
            Times.Never
        );
    }

    //UT-03

    [Theory]
    [InlineData(-100, 10)]
    [InlineData(15000, 0)]
    [InlineData(15000, -5)]
    public async Task AddProduct_WithInvalidStockOrPrice_ThrowsInvalidDataException(
        int precio,
        int stock)
    {
        // ARRANGE: Preparar datos inválidos

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<ProductsManagementService>>();

        var request = new ProductModel.Request(
            Sku: "MOUSE-002",
            InternalCode: "INT-002",
            Name: "Mouse Prueba",
            Description: "Producto de prueba",
            CurrentUnitPrice: precio,
            StockQuantity: stock
        );

        var service = new ProductsManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        // ACT Y ASSERT: Verificar que lance la excepción

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.AddProduct(request)
        );

        // ASSERT: No debe consultar ni guardar en el repositorio

        repositoryMock.Verify(
            r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()),
            Times.Never
        );

        repositoryMock.Verify(
            r => r.Add(It.IsAny<Product>()),
            Times.Never
        );
    }
    //UT-07

    [Fact]
    public async Task GetProducts_WhenIncludeInactiveIsFalse_ReturnsOnlyActiveProducts()
    {
        // ARRANGE: Preparar los datos y mocks

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<ProductsManagementService>>();

        // Simulamos tres productos: dos activos y uno inactivo
        var productos = new List<Product>
    {
        new Product("MOUSE-001", "Mouse Gamer", 15000m, 10)
        {
            IsActive = true
        },
        new Product("TEC-001", "Teclado Mecanico", 30000m, 5)
        {
            IsActive = true
        },
        new Product("MON-001", "Monitor Viejo", 50000m, 2)
        {
            IsActive = false
        }
    };

        // Simulamos que el repositorio aplica el filtro recibido
        repositoryMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Product, bool>> predicate,
                string[] includes) =>
                productos.Where(predicate.Compile()).ToList()
            );

        var service = new ProductsManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        // Filtro sin búsqueda y con página de 10 productos
        var filtro = new ProductModel.FilterProduct(
            Status: null,
            Search: null,
            PageNumber: 1,
            PageSize: 10
        );

        // ACT: Consultar productos sin incluir inactivos
        var resultado = await service.GetProducts(
            filtro,
            includeInactive: false
        );

        // ASSERT: Verificar los resultados

        // Deben aparecer solamente dos productos
        Assert.Equal(2, resultado.Total);
        Assert.Equal(2, resultado.ProductItems.Count);

        // Todos los productos devueltos deben estar activos
        Assert.All(resultado.ProductItems,
            p => Assert.True(p.IsActive));

        // El monitor inactivo no debe aparecer
        Assert.DoesNotContain(resultado.ProductItems,
            p => p.Sku == "MON-001");

        // Verificamos que se consultó el repositorio
        repositoryMock.Verify(
            r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()),
            Times.Once
        );
    }
    //UT-08

    [Theory]
    [InlineData("Mouse")]
    [InlineData("MOUSE-001")]
    [InlineData("INT-001")]
    [InlineData("mouse")]
    public async Task GetProducts_WithSearchTerm_ReturnsMatchingProducts(
        string terminoBusqueda)
    {
        // ARRANGE: Preparar productos y mocks

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<ProductsManagementService>>();

        var productos = new List<Product>
    {
        new Product(
            "MOUSE-001",
            "Mouse Gamer",
            15000m,
            10,
            "Mouse para gaming",
            "INT-001"
        )
        {
            IsActive = true
        },

        new Product(
            "TEC-001",
            "Teclado Mecanico",
            30000m,
            5,
            "Teclado para gaming",
            "INT-002"
        )
        {
            IsActive = true
        }
    };

        // Moq aplica el filtro que recibe del servicio
        repositoryMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Product, bool>> predicate,
                string[] includes) =>
                productos.Where(predicate.Compile()).ToList()
            );

        var service = new ProductsManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        var filtro = new ProductModel.FilterProduct(
            Status: null,
            Search: terminoBusqueda,
            PageNumber: 1,
            PageSize: 10
        );

        // ACT: Buscar productos
        var resultado = await service.GetProducts(filtro);

        // ASSERT: Verificar el resultado
        Assert.NotNull(resultado);

        Assert.Equal(1, resultado.Total);

        var productoEncontrado = Assert.Single(resultado.ProductItems);

        Assert.Equal("MOUSE-001", productoEncontrado.Sku);
        Assert.Equal("Mouse Gamer", productoEncontrado.Name);

        repositoryMock.Verify(
            r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()),
            Times.Once
        );
    }
    //UT-09

    [Fact]
    public async Task GetProducts_WithPaginationFilter_AppliesSkipAndTakeCorrectly()
    {
        // ARRANGE: Preparar los datos

        var repositoryMock = new Mock<IRepository>();
        var loggerMock = new Mock<ILogger<ProductsManagementService>>();

        // Creamos 15 productos activos
        var productos = Enumerable.Range(1, 15)
            .Select(i => new Product(
                $"SKU-{i:000}",
                $"Producto {i:00}",
                1000m,
                10,
                "Producto de prueba",
                $"INT-{i:000}"
            )
            {
                IsActive = true
            })
            .ToList();

        // Simulamos el repositorio con Moq
        repositoryMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Product, bool>> predicate,
                string[] includes) =>
                productos.Where(predicate.Compile()).ToList()
            );

        var service = new ProductsManagementService(
            repositoryMock.Object,
            loggerMock.Object
        );

        // Pedimos la segunda página, con 5 productos
        var filtro = new ProductModel.FilterProduct(
            Status: null,
            Search: null,
            PageNumber: 2,
            PageSize: 5
        );

        // ACT: Obtener la segunda página

        var resultado = await service.GetProducts(filtro);

        // ASSERT: Verificar la paginación

        Assert.NotNull(resultado);

        // Total general: 15 productos
        Assert.Equal(15, resultado.Total);

        // Segunda página: exactamente 5 productos
        Assert.Equal(5, resultado.ProductItems.Count);

        // Deben ser los productos 06, 07, 08, 09 y 10
        var skusEsperados = new[]
        {
        "SKU-006",
        "SKU-007",
        "SKU-008",
        "SKU-009",
        "SKU-010"
    };

        Assert.Equal(
            skusEsperados,
            resultado.ProductItems.Select(p => p.Sku)
        );

        // El repositorio debe consultarse una sola vez
        repositoryMock.Verify(
            r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()),
            Times.Once
        );
    }


}
