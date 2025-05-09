namespace Sunrise.Shared.Helpers;

public static class JsonStringFlagEnumHelper
{
    public static TEnum CombineFlags<TEnum>(IEnumerable<TEnum>? values) where TEnum : struct, Enum
    {
        if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
            throw new InvalidOperationException($"{typeof(TEnum).Name} must have [Flags] attribute.");

        var result = (values ?? []).Aggregate(0, (current, value) => current | Convert.ToInt32(value));

        return (TEnum)Enum.ToObject(typeof(TEnum), result);
    }

    public static IEnumerable<TEnum> SplitFlags<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
            throw new InvalidOperationException($"{typeof(TEnum).Name} must have [Flags] attribute.");

        var intValue = Convert.ToInt32(value);

        foreach (TEnum flag in Enum.GetValues(typeof(TEnum)))
        {
            var flagValue = Convert.ToInt32(flag);

            if (flagValue != 0 && (intValue & flagValue) == flagValue)
            {
                yield return flag;
            }
        }

        if (intValue == 0 && Enum.IsDefined(typeof(TEnum), 0))
        {
            yield return (TEnum)Enum.ToObject(typeof(TEnum), 0);
        }
    }
}