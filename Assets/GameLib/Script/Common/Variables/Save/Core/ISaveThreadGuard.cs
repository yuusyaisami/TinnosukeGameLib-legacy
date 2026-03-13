#nullable enable
namespace Game.Save
{
    public interface ISaveThreadGuard
    {
        bool TryAssertMainThread(string actionName, out string message);
    }
}
