
using System.Net;
using Dsw2025Tpi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ECommerce.IntegrationTests;

[Collection("IntegrationTests")]
public class FactoryConfigurationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public FactoryConfigurationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Api_IniciaCorrectamente_ConSQLite()
    {
        // ARRANGE
        using var client = _factory.CreateClient();

        // ACT
        var response = await client.GetAsync("/healthcheck");

        using var scope = _factory.Services.CreateScope();

        var businessDb = scope.ServiceProvider
            .GetRequiredService<Dsw2025TpiContext>();

        var authDb = scope.ServiceProvider
            .GetRequiredService<AuthenticateContext>();

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(businessDb.Database.IsSqlite());
        Assert.True(authDb.Database.IsSqlite());

        Assert.True(await businessDb.Database.CanConnectAsync());
        Assert.True(await authDb.Database.CanConnectAsync());
    }
}