using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using NetAdapterManager.Models;

namespace NetAdapterManager.ViewModels;

public sealed class LocalizedValueExtension : MarkupExtension
{
    public string Path { get; set; } = ".";
    public string Kind { get; set; } = "group";
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding { Converter = new LocalizedValueConverter(), ConverterParameter = Kind, Mode = BindingMode.OneWay };
        binding.Bindings.Add(new Binding(Path));
        binding.Bindings.Add(new Binding(nameof(L.CurrentLanguage)) { Source = L.Instance });
        return binding.ProvideValue(serviceProvider);
    }
}

public sealed class LocalizedValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] == DependencyProperty.UnsetValue) return string.Empty;
        return (string)parameter switch
        {
            "count" => L.F("Adapter.Count", values[0]),
            "groupSummary" => L.F("Group.Summary", GroupIds.Display(values[0] as string)),
            _ => GroupIds.Display(values[0] as string)
        };
    }
    public object[] ConvertBack(object value, Type[] types, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
