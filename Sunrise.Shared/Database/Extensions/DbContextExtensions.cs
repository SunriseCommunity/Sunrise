namespace Sunrise.Shared.Database.Extensions;

public static class DbContextExtensions
{
    // TODO: T should be extends from Entity
    public static void UpdateEntity<T>(this SunriseDbContext dbContext, T entity) where T : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(T));
        var keyProperty = entityType?.FindPrimaryKey()?.Properties.FirstOrDefault();

        if (keyProperty == null)
            throw new InvalidOperationException($"Entity {typeof(T).Name} does not have a primary key.");

        var keyPropertyName = keyProperty.Name;
        var keyValue = typeof(T).GetProperty(keyPropertyName)?.GetValue(entity);

        if (keyValue == null)
            throw new InvalidOperationException($"Entity {typeof(T).Name} has a null primary key value.");

        var existingEntry = dbContext.ChangeTracker.Entries<T>()
            .FirstOrDefault(e => keyProperty.PropertyInfo!.GetValue(e.Entity)?.Equals(keyValue) == true);

        if (existingEntry == null)
        {
            dbContext.Update(entity);
        }
        else
        {
            existingEntry.CurrentValues.SetValues(entity);
        }
    }
}