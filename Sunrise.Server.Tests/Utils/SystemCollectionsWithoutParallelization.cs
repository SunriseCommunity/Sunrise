namespace Sunrise.Server.Tests.Utils;

[CollectionDefinition(nameof(SystemCollectionsWithoutParallelization ), DisableParallelization = true)]
public class SystemCollectionsWithoutParallelization : ICollectionFixture<SunriseServerFactory>
{
}