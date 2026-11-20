using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwarfVolon.Core.WorldGen;

namespace DwarfVolon.ViewModels;

/// <summary>Um card da tela simples: categoria (ex.: Rios) com seus níveis.</summary>
public partial class ModifierCategoryViewModel : ViewModelBase
{
    private readonly Action _onChanged;
    private bool _suppressNotify;

    public ModifierCategoryViewModel(ModifierCategory category, Action onChanged)
    {
        Category = category;
        _onChanged = onChanged;
        Options = new ObservableCollection<ModifierOptionViewModel>(category.Options.Select(o => new ModifierOptionViewModel(o)));
        _suppressNotify = true;
        SelectedOption = Options.First(o => o.Option == category.DefaultOption);
        _suppressNotify = false;
    }

    public ModifierCategory Category { get; }
    public string Id => Category.Id;
    public string Name => Category.Name;
    public string Icon => Category.Icon;
    public string Description => Category.Description;
    public ObservableCollection<ModifierOptionViewModel> Options { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    public partial ModifierOptionViewModel SelectedOption { get; set; }

    public bool IsModified => !SelectedOption.Option.IsDefault;

    partial void OnSelectedOptionChanged(ModifierOptionViewModel oldValue, ModifierOptionViewModel newValue)
    {
        // A ListBox pode mandar null ao recriar itens; nesse caso mantém a seleção anterior.
        if (newValue is null && oldValue is not null)
        {
            SelectedOption = oldValue;
            return;
        }
        if (!_suppressNotify) _onChanged();
    }

    /// <summary>Seleciona sem disparar a reconstrução (usado ao aplicar perfis em lote).</summary>
    public void SelectSilently(string? optionId)
    {
        var target = (optionId is null ? null : Options.FirstOrDefault(o => string.Equals(o.Option.Id, optionId, StringComparison.OrdinalIgnoreCase)))
                     ?? Options.First(o => o.Option.IsDefault);
        _suppressNotify = true;
        SelectedOption = target;
        _suppressNotify = false;
    }
}

public sealed class ModifierOptionViewModel(ModifierOption option)
{
    public ModifierOption Option { get; } = option;
    public string Name => Option.Name;
    public string Description => Option.Description;
    public string Notes => string.Join(Environment.NewLine, Option.Notes.Select(n => "⚠ " + n));
    public bool HasNotes => Option.Notes.Count > 0;
}

public partial class ProfileViewModel(WorldGenProfile profile, Action<WorldGenProfile> apply) : ViewModelBase
{
    public WorldGenProfile Profile { get; } = profile;
    public string Name => Profile.Name;
    public string Description => Profile.Description;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [RelayCommand]
    private void Apply() => apply(Profile);
}

public sealed record SummaryLine(string Icon, string Category, string Option);
