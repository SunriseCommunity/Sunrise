using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Users;

public class UserInventoryItemService(SunriseDbContext dbContext)
{
    public async Task<Result> SetInventoryItem(User user, ItemType itemType, int quantity)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var inventoryItem = await GetInventoryItem(user.Id, itemType);

            if (inventoryItem == null)
            {
                dbContext.UserInventoryItem.Add(new UserInventoryItem
                {
                    UserId = user.Id,
                    ItemType = itemType,
                    Quantity = quantity
                });
                await dbContext.SaveChangesAsync();
            }
            else
            {
                inventoryItem.Quantity = quantity;
                await UpdateInventoryItem(inventoryItem);
            }
        });
    }

    public async Task<Result> UpdateInventoryItem(UserInventoryItem userInventoryItem)
    {
        if (userInventoryItem.Quantity <= 0)
        {
            return await RemoveInventoryItem(userInventoryItem);
        }

        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(userInventoryItem);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<UserInventoryItem?> GetInventoryItem(int userId, ItemType type, QueryOptions? options = null, CancellationToken ct = default)
    {
        var inventoryItem = await dbContext.UserInventoryItem
            .Where(i => i.UserId == userId && i.ItemType == type)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);

        return inventoryItem;
    }

    private async Task<Result> RemoveInventoryItem(UserInventoryItem userInventoryItem)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UserInventoryItem.Remove(userInventoryItem);
            await dbContext.SaveChangesAsync();
        });
    }
}