using System.ComponentModel;
using System.Reflection;

namespace WallP.Helpers;

public sealed record EnumPickerItem<T>(T Value, string Display) where T : struct, Enum;

public static class EnumPickerHelper
{
    /// <summary>
    /// Returns one item per enum member, using the [Description] attribute as the
    /// display string (or the enum name if there's no attribute).
    /// </summary>
    public static IList<EnumPickerItem<T>> ItemsFor<T>() where T : struct, Enum
    {
        return Enum.GetValues<T>()
            .Select(v => new EnumPickerItem<T>(v, GetDescription(v)))
            .ToList();
    }

    private static string GetDescription<T>(T value) where T : Enum
    {
        var name = value.ToString();
        var field = typeof(T).GetField(name);
        var attr = field?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? name;
    }
}
