#nullable enable
using UnityEngine;

namespace Game.Project.Bootstrap
{
    public sealed class KernelLiveBootHost : MonoBehaviour
    {
        [SerializeField] KernelLiveBootBundleAsset liveBootBundle = null!;

        bool _bootStarted;

        async void Start()
        {
            if (_bootStarted)
                return;

            if (liveBootBundle == null)
            {
                Debug.LogError("Kernel live boot host requires a KernelLiveBootBundleAsset.", this);
                return;
            }

            _bootStarted = true;
            KernelLiveBootResult result = await KernelLiveBootOrchestrator.ExecuteAsync(liveBootBundle);
            if (result.IsSuccessful)
                return;

            Debug.LogError("Kernel live boot failed: " + result.Message, this);
            if (result.Exception != null)
                Debug.LogException(result.Exception, this);
        }
    }
}