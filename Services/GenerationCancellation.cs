namespace MealGeniusBackend.Services;

// AsyncLocal carries the job deadline into nested service scopes and parallel generation stages.
public sealed class GenerationCancellation
{
    private readonly AsyncLocal<CancellationToken> current = new();
    public CancellationToken Token { get => current.Value; set => current.Value = value; }
}
