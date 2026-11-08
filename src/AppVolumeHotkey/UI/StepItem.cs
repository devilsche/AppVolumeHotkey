using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.UI;

public sealed class StepItem : INotifyPropertyChanged
{
    private int _number;
    private string _percentText = "";

    public StepItem(Step step)
    {
        Kind = step.Kind;
        _percentText = step.Kind == StepKind.Percent ? step.Value.ToString() : "";
    }

    public StepKind Kind { get; }
    public bool IsPercent => Kind == StepKind.Percent;
    public bool IsBaseline => Kind == StepKind.Baseline;

    public int Number
    {
        get => _number;
        set { _number = value; OnChanged(); OnChanged(nameof(AccessibleName)); }
    }

    public string PercentText
    {
        get => _percentText;
        set { _percentText = value; OnChanged(); OnChanged(nameof(AccessibleName)); }
    }

    public bool TryGetStep(out Step step)
    {
        if (IsBaseline)
        {
            step = Step.Baseline;
            return true;
        }
        if (int.TryParse(PercentText.Trim().TrimEnd('%').Trim(), out var v) && v is >= 0 and <= 100)
        {
            step = Step.Percent(v);
            return true;
        }
        step = Step.Baseline;
        return false;
    }

    public string AccessibleName => IsBaseline ? $"Stufe {Number}: Ausgangswert" : $"Stufe {Number}: {PercentText} Prozent";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
