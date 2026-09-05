using System.Globalization;
using System.IO;
using System.Windows.Data;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI.Converters;

public sealed class ScanRootAvailabilityConverter : IValueConverter
{
    public IPortablePathResolver? Resolver { get; set; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ScanRoot root) return string.Empty;
        return Resolver?.GetAvailability(root) == ScanRootAvailability.Available ? "Available" : "Missing";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
