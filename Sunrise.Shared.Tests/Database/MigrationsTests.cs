using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Sunrise.Tests.Abstracts;
using Xunit;

namespace Sunrise.Shared.Tests.Database;

[Collection("Integration tests collection")]
public class MigrationsTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public void TestModelHasNoChangesMissingFromMigrations()
    {
        // Arrange
        var context = Database.DbContext;
        var snapshotModel = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;

        if (snapshotModel is IMutableModel mutableModel)
            snapshotModel = mutableModel.FinalizeModel();

        snapshotModel = context.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel);

        // Act
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        // Assert
        Assert.Empty(differences);
    }
}
