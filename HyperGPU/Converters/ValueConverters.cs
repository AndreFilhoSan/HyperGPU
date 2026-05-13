using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using HyperGPU.Models;

namespace HyperGPU.Converters;

public sealed class HostCheckStateBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ReadyBrush = new(ColorHelper.FromArgb(255, 34, 197, 94));
    private static readonly SolidColorBrush WarningBrush = new(ColorHelper.FromArgb(255, 245, 158, 11));
    private static readonly SolidColorBrush ErrorBrush = new(ColorHelper.FromArgb(255, 239, 68, 68));
    private static readonly SolidColorBrush NeutralBrush = new(ColorHelper.FromArgb(255, 148, 163, 184));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is HostCheckState state
            ? state switch
            {
                HostCheckState.Ready => ReadyBrush,
                HostCheckState.Warning => WarningBrush,
                HostCheckState.Error => ErrorBrush,
                _ => NeutralBrush,
            }
            : NeutralBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class GpuDeviceBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush NvidiaBrush = new(ColorHelper.FromArgb(255, 118, 185, 0));
    private static readonly SolidColorBrush AmdBrush = new(ColorHelper.FromArgb(255, 237, 28, 36));
    private static readonly SolidColorBrush IntelBrush = new(ColorHelper.FromArgb(255, 0, 113, 197));
    private static readonly SolidColorBrush GenericBrush = new(ColorHelper.FromArgb(255, 99, 102, 241));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not GpuDeviceInfo gpu)
        {
            return GenericBrush;
        }

        if (gpu.IsNvidia)
        {
            return NvidiaBrush;
        }

        if (gpu.IsAmd)
        {
            return AmdBrush;
        }

        if (gpu.IsIntel)
        {
            return IntelBrush;
        }

        return GenericBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class StepCompletionBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush CompletedBrush = new(ColorHelper.FromArgb(255, 34, 197, 94));
    private static readonly SolidColorBrush PendingBrush = new(ColorHelper.FromArgb(255, 148, 163, 184));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? CompletedBrush : PendingBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class StepCompletionBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush CompletedBrush = new(ColorHelper.FromArgb(26, 22, 163, 74));
    private static readonly SolidColorBrush PendingBrush = new(ColorHelper.FromArgb(255, 45, 45, 45));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? CompletedBrush : PendingBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class StepCompletionGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? "\uE73E" : "\uE70D";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class WorkflowStepFillConverter : IValueConverter
{
    private static readonly SolidColorBrush CompleteBrush = new(ColorHelper.FromArgb(255, 15, 61, 42));
    private static readonly SolidColorBrush PendingBrush = new(ColorHelper.FromArgb(255, 69, 50, 17));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? CompleteBrush : PendingBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class WorkflowStepStrokeConverter : IValueConverter
{
    private static readonly SolidColorBrush CompleteBrush = new(ColorHelper.FromArgb(180, 34, 197, 94));
    private static readonly SolidColorBrush PendingBrush = new(ColorHelper.FromArgb(190, 245, 158, 11));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? CompleteBrush : PendingBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class WorkflowStepAccentConverter : IValueConverter
{
    private static readonly SolidColorBrush CompleteBrush = new(ColorHelper.FromArgb(255, 34, 197, 94));
    private static readonly SolidColorBrush PendingBrush = new(ColorHelper.FromArgb(255, 245, 158, 11));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? CompleteBrush : PendingBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class WorkflowStepIconBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush CompleteBrush = new(ColorHelper.FromArgb(48, 34, 197, 94));
    private static readonly SolidColorBrush PendingBrush = new(ColorHelper.FromArgb(56, 245, 158, 11));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? CompleteBrush : PendingBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class ValidationBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ErrorBrush = new(ColorHelper.FromArgb(255, 239, 68, 68));
    private static readonly SolidColorBrush TransparentBrush = new(ColorHelper.FromArgb(0, 0, 0, 0));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? ErrorBrush : TransparentBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class ValidationThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? new Microsoft.UI.Xaml.Thickness(2) : new Microsoft.UI.Xaml.Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
