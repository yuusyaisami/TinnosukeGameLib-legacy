// Game.Field
using UnityEngine;
using VContainer;

namespace Game.Field
{
    public abstract class FieldMB : MonoBehaviour
    {
        IFieldRegistry _registry;
        bool _registered;

        [Inject]
        public void Construct(IFieldRegistry registry)
        {
            _registry = registry;

            if (!_registered && isActiveAndEnabled)
            {
                _registry.Register(this);
                _registered = true;
            }
        }

        protected virtual void OnEnable()
        {
            if (_registry != null && !_registered)
            {
                _registry.Register(this);
                _registered = true;
            }
        }

        protected virtual void OnDisable()
        {
            if (_registry != null && _registered)
            {
                _registry.Unregister(this);
                _registered = false;
            }
        }
    }
}
