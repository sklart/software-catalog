using System.Globalization;
using System.IO;
using System.Windows.Data;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI.Converters;

public sealed class ScanRootAvailabilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ScanRoot root) return string.Empty;
        var path = root.PathKind == ScanRootPathKind.RelativeToApplication ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, root.StoredPath)) : root.StoredPath;
        return Directory.Exists(path) ? "Available" : "Missing";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
