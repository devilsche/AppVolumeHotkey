using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Core.Localization;

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
    public bool IsMute => Kind == StepKind.Mute;

    public int Number
    {
        get => _number;
        set { _number = value; OnChanged(); OnChanged(nameof(AccessibleName)); OnChanged(nameof(PercentName)); OnChanged(nameof(UpName)); OnChanged(nameof(DownName)); OnChanged(nameof(RemoveName)); }
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
        if (IsMute)
        {
            step = Step.Mute;
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

    public string AccessibleName => IsBaseline ? L.F("Profile_StepAccBaseline", Number)
        : IsMute ? L.F("Profile_StepAccMute", Number)
        : L.F("Profile_StepAccPercent", Number, PercentText);

    public string PercentName => L.F("Profile_StepPercentA11y", Number);
    public string UpName => L.F("Profile_StepUpA11y", Number);
    public string DownName => L.F("Profile_StepDownA11y", Number);
    public string RemoveName => L.F("Profile_StepRemoveA11y", Number);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
