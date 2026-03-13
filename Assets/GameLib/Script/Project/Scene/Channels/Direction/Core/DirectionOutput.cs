using UnityEngine;

namespace Game.Direction
{
    public sealed class DirectionOutput : IDirectionOutput
    {
        Vector2 _outputValue;
        Vector2 _targetValue;
        uint _version;

        public Vector2 OutputValue => _outputValue;
        public Vector2 TargetValue => _targetValue;
        public uint Version => _version;

        public bool HasChanged(uint lastVersion) => _version != lastVersion;

        public void SetValues(Vector2 target, Vector2 output)
        {
            if (_outputValue != output || _targetValue != target)
            {
                _outputValue = output;
                _targetValue = target;
                _version++;
                return;
            }

            if (_targetValue != target)
            {
                _targetValue = target;
                _version++;
            }
        }
    }
}
