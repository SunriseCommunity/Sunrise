using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Tests.Abstracts;

namespace Sunrise.Shared.Tests.Application;

[Collection("Integration tests collection")]
public class RecurringJobsTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task TestSaveUsersStatsSnapshotsShouldSavesSnapshotsForAllGameModesForUser()
    {
        // Arrange
        var user = await CreateTestUser();

        await Database.DbContext.UserStats.ExecuteUpdateAsync(setters => setters
            .SetProperty(
                us => us.PerformancePoints,
                us => 1
            )
        );

        // Act
        await RecurringJobs.SaveUsersStatsSnapshots(CancellationToken.None);

        // Assert
        var snapshots = await Database.Users.Stats.Snapshots.GetUserAllStatsSnapshot(user.Id, CancellationToken.None);

        Assert.Equal(Enum.GetValues<GameMode>().Length, snapshots.Count);
        Assert.All(snapshots,
            snapshot =>
            {
                var snaps = snapshot.GetSnapshots();
                Assert.NotEmpty(snaps);
            });
    }

    [Fact]
    public async Task TestSaveUsersStatsSnapshotsShouldIgnoreSavesSnapshotsForInactiveUser()
    {
        // Arrange
        var user = await CreateTestUser();

        await Database.DbContext.UserStats.ExecuteUpdateAsync(setters => setters
            .SetProperty(
                us => us.PerformancePoints,
                us => 1
            )
        );

        await Database.Users.Moderation.DisableUser(user.Id);

        // Act
        await RecurringJobs.SaveUsersStatsSnapshots(CancellationToken.None);

        // Assert
        var snapshots = await Database.Users.Stats.Snapshots.GetUserAllStatsSnapshot(user.Id, CancellationToken.None);

        Assert.Equal(Enum.GetValues<GameMode>().Length, snapshots.Count);
        Assert.All(snapshots,
            snapshot =>
            {
                var snaps = snapshot.GetSnapshots();
                Assert.Empty(snaps);
            });
    }

    [Fact]
    public async Task TestSaveUsersStatsSnapshotsShouldIgnoreSavesSnapshotsForRestrictedUser()
    {
        // Arrange
        var user = await CreateTestUser();

        await Database.DbContext.UserStats.ExecuteUpdateAsync(setters => setters
            .SetProperty(
                us => us.PerformancePoints,
                us => 1
            )
        );

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "test");

        // Act
        await RecurringJobs.SaveUsersStatsSnapshots(CancellationToken.None);

        // Assert
        var snapshots = await Database.Users.Stats.Snapshots.GetUserAllStatsSnapshot(user.Id, CancellationToken.None);

        Assert.Equal(Enum.GetValues<GameMode>().Length, snapshots.Count);
        Assert.All(snapshots,
            snapshot =>
            {
                var snaps = snapshot.GetSnapshots();
                Assert.Empty(snaps);
            });
    }

    [Fact]
    public async Task TestSaveUsersStatsSnapshotsShouldIgnoreSavesSnapshotsForUserStatsWithZeroPerformancePoints()
    {
        // Arrange
        var user = await CreateTestUser();

        await Database.DbContext.UserStats.ExecuteUpdateAsync(setters => setters
            .SetProperty(
                us => us.PerformancePoints,
                us => 0
            )
        );

        await Database.Users.Moderation.DisableUser(user.Id);

        // Act
        await RecurringJobs.SaveUsersStatsSnapshots(CancellationToken.None);

        // Assert
        var snapshots = await Database.Users.Stats.Snapshots.GetUserAllStatsSnapshot(user.Id, CancellationToken.None);

        Assert.Equal(Enum.GetValues<GameMode>().Length, snapshots.Count);
        Assert.All(snapshots,
            snapshot =>
            {
                var snaps = snapshot.GetSnapshots();
                Assert.Empty(snaps);
            });
    }


    [Fact]
    public async Task TestSaveUsersStatsSnapshotsShouldSavesSnapshotsForAllGameModesForMultipleUsers()
    {
        // Arrange
        var users = await CreateTestUsers(5);

        await Database.DbContext.UserStats.ExecuteUpdateAsync(setters => setters
            .SetProperty(
                us => us.PerformancePoints,
                us => 1
            )
        );

        // Act
        await RecurringJobs.SaveUsersStatsSnapshots(CancellationToken.None);

        // Assert
        foreach (var gameMode in Enum.GetValues<GameMode>())
        {
            var snapshot = await Database.Users.Stats.Snapshots.GetUsersAllStatsSnapshot(users.Select(u => u.Id).ToList(), gameMode, CancellationToken.None);
            Assert.NotNull(snapshot);

            var emptySnapshots = snapshot.Where(kv => kv.Value.GetSnapshots().Count == 0).ToList();
            Assert.Empty(emptySnapshots);


            foreach (var x in users.Select((x, i) => new
                     {
                         Value = x,
                         Index = i
                     }))
            {
                var (user, index) = (x.Value, x.Index);

                var userSnapshot = snapshot[user.Id];
                var latestSnapshot = userSnapshot.GetSnapshots().FirstOrDefault();
                Assert.NotNull(latestSnapshot);

                Assert.Equal(latestSnapshot.Rank, index + 1);
            }
        }
    }
    
    [Fact]
    public async Task TestSaveUsersStatsSnapshotsShouldNotTakeLongerThanExpectedForMultipleUsers()
    {
        // Arrange
        var users = await CreateTestUsers(100);

        await Database.DbContext.UserStats.ExecuteUpdateAsync(setters => setters
            .SetProperty(
                us => us.PerformancePoints,
                us => 1
            )
        );

        await Database.FlushAndUpdateRedisCache(false);

        var timer = Stopwatch.StartNew();

        // Act
        await RecurringJobs.SaveUsersStatsSnapshots(CancellationToken.None);

        timer.Stop();

        // Assert
        foreach (var gameMode in Enum.GetValues<GameMode>())
        {
            var snapshot = await Database.Users.Stats.Snapshots.GetUsersAllStatsSnapshot(users.Select(u => u.Id).ToList(), gameMode, CancellationToken.None);
            Assert.NotNull(snapshot);

            var emptySnapshots = snapshot.Where(kv => kv.Value.GetSnapshots().Count == 0).ToList();
            Assert.Empty(emptySnapshots);
        }

        Assert.True(timer.ElapsedMilliseconds < 1500, $"Login took too long, possible performance issue with multiple active sessions. Took: {timer.ElapsedMilliseconds}ms");
    }
}