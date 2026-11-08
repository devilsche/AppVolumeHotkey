using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey.UI;

/// <summary>Kleiner modaler Dialog für eine Texteingabe (z. B. Gruppenname).</summary>
public sealed class InputDialog : Window
{
    private readonly TextBox _box;

    private InputDialog(Window owner, string title, string label, string initial)
    {
        Owner = owner;
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var labelBlock = new Label { Content = "_" + label, Padding = new Thickness(0, 0, 0, 4) };
        _box = new TextBox { Text = initial, MinWidth = 320 };
        labelBlock.Target = _box;
        AutomationProperties.SetName(_box, label);

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_box.Text))
                DialogResult = true;
        };
        var cancel = new Button { Content = L.T("Common_Cancel"), IsCancel = true, MinWidth = 90 };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(labelBlock);
        panel.Children.Add(_box);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) => { _box.Focus(); _box.SelectAll(); };
    }

    public static string? Ask(Window owner, string title, string label, string initial = "")
    {
        var d = new InputDialog(owner, title, label, initial);
        return d.ShowDialog() == true ? d._box.Text.Trim() : null;
    }
}
