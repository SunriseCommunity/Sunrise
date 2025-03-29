namespace Sunrise.Tests.Extensions;

public static class DatabaseExtensions
{
    public static bool IsDatabaseForTesting(this string databaseName)
    {
        return databaseName.Contains("test");
    }

    public static DateTime ToDatabasePrecision(this DateTime date, int roundTicks = 10)
    {
        return new DateTime(date.Ticks - date.Ticks % roundTicks, date.Kind);
    }
}