#nullable enable

namespace Game
{
	public interface IScopeInstaller
	{
		void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope);
	}
}
