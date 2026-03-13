#nullable enable
using System.Collections.Generic;

namespace Game.UI
{
    public interface IUIInputNavigateService
    {
        bool ReceiveInputEvent(in UIInputEvent e);

        void Register(UIInputNavigateObjectService obj);
        void Unregister(UIInputNavigateObjectService obj);
    }

    /// <summary>
    /// UISelection が入力を消費しなかった場合に、特定入力で特定 UIElement を選択させるための管理サービス。
    /// 実際の Select はここが行い、各要素は UIInputNavigateObjectService 経由で登録される。
    /// </summary>
    public sealed class UIInputNavigateManagerService : IUIInputNavigateService, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IUISelectionService _selection;
        readonly List<UIInputNavigateObjectService> _objects = new();

        public UIInputNavigateManagerService(IUISelectionService selection)
        {
            _selection = selection;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // no-op
        }

        public void OnRelease(IScopeNode scope, bool isDestroy)
        {
            _objects.Clear();
        }

        public void Register(UIInputNavigateObjectService obj)
        {
            if (obj == null)
                return;

            if (!_objects.Contains(obj))
                _objects.Add(obj);
        }

        public void Unregister(UIInputNavigateObjectService obj)
        {
            if (obj == null)
                return;

            _objects.Remove(obj);
        }

        public bool ReceiveInputEvent(in UIInputEvent e)
        {
            if (_objects.Count == 0)
                return false;

            for (int i = 0; i < _objects.Count; i++)
            {
                var obj = _objects[i];
                if (!obj.IsEnabled)
                    continue;

                if (!obj.Trigger.Matches(in e))
                    continue;

                var target = obj.Owner;
                if (target == null)
                    continue;

                // "強制的" といっても、Modal 境界や Active/Visible は守る（TrySelect が判定する）。
                if (_selection.TrySelect(target))
                {
                    if (obj.ResendInputOnSelect)
                    {
                        _selection.SendInputToCurrentSelection(in e);
                    }

                    return true;
                }

                return false;
            }

            return false;
        }
    }
}
