#nullable enable

namespace Game.Fire
{
    public interface IOutputFirePattern
    {
        void OnFireContextReceived(in FireContext context);
    }
}
