using CSharpFunctionalExtensions;

namespace Sunrise.Shared.Utils;

public static class ResultUtil
{
    public static async Task<Result<T>> TryExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            var result = await action();
            return Result.Success(result);
        }
        catch (ApplicationException ex)
        {
            return Result.Failure<T>(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<T>($"{ex.Message}\n{ex.InnerException}\n{ex.StackTrace}");
        }
    }
    
    public static async Task<Result> TryExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
            return Result.Success();
        }
        catch (ApplicationException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure($"{ex.Message}\n{ex.InnerException}\n{ex.StackTrace}");
        }
    }
}