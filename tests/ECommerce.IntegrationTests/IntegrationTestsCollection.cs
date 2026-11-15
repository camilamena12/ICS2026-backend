
using Xunit;

namespace ECommerce.IntegrationTests;

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestsCollection
    : ICollectionFixture<CustomWebApplicationFactory>
{
}
