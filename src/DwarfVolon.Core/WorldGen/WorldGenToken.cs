namespace DwarfVolon.Core.WorldGen;

/// <summary>
/// Um token do world_gen.txt no formato <c>[NOME:arg1:arg2:...]</c>.
/// Imutável: para alterar, crie um novo token e use <see cref="WorldGenParameterSet.Set(WorldGenToken)"/>.
/// </summary>
public sealed class WorldGenToken : IEquatable<WorldGenToken>
{
    public WorldGenToken(string name, IEnumerable<string>? args = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim().ToUpperInvariant();
        Args = (args ?? []).ToArray();
    }

    public WorldGenToken(string name, params string[] args) : this(name, (IEnumerable<string>)args) { }

    public string Name { get; }

    /// <summary>Todos os argumentos, incluindo a chave (ex.: <c>SWAMP</c> em <c>REGION_COUNTS</c>).</summary>
    public IReadOnlyList<string> Args { get; }

    /// <summary>Chave de identidade para tokens que se repetem com um primeiro argumento distinto (ex.: REGION_COUNTS).</summary>
    public string? Key => WorldGenTokenCatalog.IsKeyed(Name) && Args.Count > 0 ? Args[0] : null;

    /// <summary>Argumentos sem a chave. Para tokens sem chave é igual a <see cref="Args"/>.</summary>
    public IReadOnlyList<string> Values => Key is null ? Args : Args.Skip(1).ToArray();

    public bool Matches(string name, string? key = null) =>
        string.Equals(Name, name, StringComparison.OrdinalIgnoreCase) &&
        (key is null || string.Equals(Key, key, StringComparison.OrdinalIgnoreCase));

    public WorldGenToken WithValues(IEnumerable<string> values) =>
        Key is null ? new WorldGenToken(Name, values) : new WorldGenToken(Name, values.Prepend(Key));

    public string ToText() => Args.Count == 0 ? $"[{Name}]" : $"[{Name}:{string.Join(':', Args)}]";

    /// <summary>Interpreta o conteúdo entre colchetes, ex.: <c>RIVER_MINS:100:100</c>.</summary>
    public static WorldGenToken Parse(string inner)
    {
        var parts = inner.Split(':');
        return new WorldGenToken(parts[0], parts.Skip(1));
    }

    public override string ToString() => ToText();

    public bool Equals(WorldGenToken? other) =>
        other is not null && Name == other.Name && Args.SequenceEqual(other.Args);

    public override bool Equals(object? obj) => Equals(obj as WorldGenToken);

    public override int GetHashCode() => HashCode.Combine(Name, string.Join(':', Args));
}
