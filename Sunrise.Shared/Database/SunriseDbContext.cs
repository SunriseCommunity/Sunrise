using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Users;

namespace Sunrise.Shared.Database;

public class SunriseDbContext : DbContext
{
    public SunriseDbContext()
    {
    }

    public SunriseDbContext(DbContextOptions<SunriseDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserFavouriteBeatmap> UserFavouriteBeatmaps { get; set; }
    public DbSet<UserMedals> UserMedals { get; set; }
    public DbSet<UserStats> UserStats { get; set; }
    public DbSet<UserStatsSnapshot> UserStatsSnapshot { get; set; }
    public DbSet<UserFile> UserFiles { get; set; }

    public DbSet<BeatmapFile> BeatmapFiles { get; set; }

    public DbSet<Medal> Medals { get; set; }
    public DbSet<MedalFile> MedalFiles { get; set; }

    public DbSet<EventUser> EventUsers { get; set; }
    public DbSet<Restriction> Restrictions { get; set; }

    public DbSet<Score> Scores { get; set; }
}