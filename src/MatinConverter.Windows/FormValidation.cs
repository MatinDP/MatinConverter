using System.Windows.Controls;
using System.Windows.Media;
namespace MatinConverter.Windows;

public static class FormValidation
{
    public static void EnsureValid()
    {
        if (Application.Current.MainWindow is { } window && HasError(window))
            throw new ArgumentException(L.Language == "fa" ? "مقدارهای مشخص‌شده با کادر قرمز را اصلاح کن." : "Correct the fields marked with a red border.");
    }
    private static bool HasError(DependencyObject item)
    {
        if (Validation.GetHasError(item))
            return true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++)
            if (HasError(VisualTreeHelper.GetChild(item, i)))
                return true;
        return false;
    }
}
