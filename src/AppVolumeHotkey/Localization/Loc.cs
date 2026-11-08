using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey.Localization;

/// <summary>Bindbare Quelle für Texte: <c>Loc.Instance["Key"]</c>. Meldet beim Sprachwechsel alle Texte als geändert.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Loc() => L.Changed += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

    public string this[string key] => L.T(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>XAML: <c>Content="{l:T Main_Groups}"</c> – Text wechselt live mit der Sprache.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }

    public TExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
