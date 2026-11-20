using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DwarfVolon.Core.WorldGen;

namespace DwarfVolon.ViewModels;

public partial class TokenGroupViewModel(string name, IEnumerable<TokenRowViewModel> rows) : ViewModelBase
{
    public string Name { get; } = name;

    public ObservableCollection<TokenRowViewModel> Rows { get; } = new(rows);

    public string Header => $"{Name}  ({Rows.Count(r => r.IsVisible)})";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    public void RefreshHeader() => OnPropertyChanged(nameof(Header));
}

/// <summary>Uma linha do editor: um token com seus argumentos editáveis.</summary>
public partial class TokenRowViewModel : ViewModelBase
{
    private readonly WorldGenToken? _baseToken;
    private readonly Action<TokenRowViewModel> _onEdited;

    public TokenRowViewModel(WorldGenToken token, WorldGenToken? baseToken, WorldGenTokenInfo info, Action<TokenRowViewModel> onEdited)
    {
        Name = token.Name;
        Key = token.Key;
        _baseToken = baseToken;
        _onEdited = onEdited;
        DisplayName = Key is null ? info.DisplayName : $"{info.DisplayName}: {Key}";
        Description = string.IsNullOrWhiteSpace(info.Description) ? info.DisplayName : info.Description;
        TokenLabel = Key is null ? Name : $"{Name}:{Key}";
        BaseText = baseToken is null ? "Não existe na base (adicionado)" : $"Base: {string.Join(':', baseToken.Values)}";

        var labels = info.IsKeyed ? info.Args.Skip(1).ToList() : info.Args.ToList();
        Args = new ObservableCollection<ArgViewModel>(token.Values.Select((value, i) =>
        {
            var argInfo = i < labels.Count ? labels[i] : new WorldGenArgInfo($"Valor {i + 1}");
            var baseValue = baseToken is not null && i < baseToken.Values.Count ? baseToken.Values[i] : null;
            return new ArgViewModel(this, argInfo, value, baseValue);
        }));
        IsChanged = ComputeChanged();
    }

    public string Name { get; }
    public string? Key { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string TokenLabel { get; }
    public string BaseText { get; }
    public ObservableCollection<ArgViewModel> Args { get; }

    [ObservableProperty]
    public partial bool IsChanged { get; private set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    public bool HasErrors => Args.Any(a => a.HasError);

    public WorldGenToken ToToken()
    {
        var values = Args.Select(a => a.Value.Trim());
        return new WorldGenToken(Name, Key is null ? values : values.Prepend(Key));
    }

    public bool Matches(string filter) =>
        DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        TokenLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        Description.Contains(filter, StringComparison.OrdinalIgnoreCase);

    internal void OnArgChanged()
    {
        IsChanged = ComputeChanged();
        if (!HasErrors) _onEdited(this);
    }

    private bool ComputeChanged() =>
        _baseToken is null || !_baseToken.Values.SequenceEqual(Args.Select(a => a.Value.Trim()));
}

public partial class ArgViewModel : ViewModelBase
{
    private readonly TokenRowViewModel _row;
    private readonly WorldGenArgInfo _info;

    public ArgViewModel(TokenRowViewModel row, WorldGenArgInfo info, string value, string? baseValue)
    {
        _row = row;
        _info = info;
        BaseValue = baseValue;
        Value = value;
    }

    public string Label => _info.Label;
    public string? BaseValue { get; }
    public IReadOnlyList<string>? Choices => _info.Choices;

    public bool IsFlag => _info.Kind == WorldGenArgKind.Flag;
    public bool IsChoice => _info.Kind == WorldGenArgKind.Choice && Choices is { Count: > 0 };
    public bool IsTextual => !IsFlag && !IsChoice;

    public string ToolTip => BaseValue is null ? Label : $"{Label} (base: {BaseValue})";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoolValue), nameof(IsChanged))]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; private set; }

    public bool IsChanged => BaseValue is null || BaseValue != Value.Trim();

    public bool BoolValue
    {
        get => Value.Trim() == "1";
        set => Value = value ? "1" : "0";
    }

    partial void OnValueChanged(string value)
    {
        HasError = _info.Kind is WorldGenArgKind.Number or WorldGenArgKind.Flag &&
                   !int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        // O construtor também passa por aqui antes da linha estar pronta.
        if (_row.Args is not null) _row.OnArgChanged();
    }
}
