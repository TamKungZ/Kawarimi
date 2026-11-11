namespace Kawarimi;

public interface IKawarimiContext
{
    string GameDirectory { get; }
    string ModDirectory { get; }
    ISkinRegistry Skins { get; }
    void Log(string message);
}
