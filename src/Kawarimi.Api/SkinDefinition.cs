namespace Kawarimi;

public sealed record SkinDefinition
{
    public string? CharacterCode { get; init; }
    public int? CharacterId { get; init; }
    public required string SkinCode { get; init; }
    public string DisplayName { get; init; } = "";
    public required string ModelDirectory { get; init; }
    public string? Replaces { get; init; }
    public string ModId { get; init; } = "";

    public string FileCode => string.IsNullOrEmpty(Replaces) ? SkinCode : Replaces;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CharacterCode) && CharacterId is null)
            throw new ArgumentException("Either CharacterCode or CharacterId must be set.");
        if (CharacterId is < 0)
            throw new ArgumentException("CharacterId must not be negative.");
        if (!IsCode(SkinCode))
            throw new ArgumentException($"SkinCode [{SkinCode}] must be 1-7 letters/digits.");
        if (Replaces is { Length: > 0 } r && !IsCode(r))
            throw new ArgumentException($"Replaces [{Replaces}] must be an existing 1-7 letter/digit costume code.");
        if (string.IsNullOrWhiteSpace(ModelDirectory))
            throw new ArgumentException("ModelDirectory is required.");
    }

    static bool IsCode(string? s) => !string.IsNullOrEmpty(s) && s.Length <= 7 && s.All(char.IsAsciiLetterOrDigit);
}
