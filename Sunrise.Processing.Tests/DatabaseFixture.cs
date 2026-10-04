using Xunit;

namespace Sunrise.Processing.Tests;

public class IntegrationDatabaseFixture : Sunrise.Tests.IntegrationDatabaseFixture
{
}

[CollectionDefinition("Integration tests collection")]
public class DatabaseTestCollection : ICollectionFixture<IntegrationDatabaseFixture>
{
}