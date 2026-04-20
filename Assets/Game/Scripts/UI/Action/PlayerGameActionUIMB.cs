using System;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;
using VNext = Game.Commands.VNext;
/*
namespace Game.Actions
{
    [Serializable]
    public class ActionBarUISetting
    {
        [LabelText("Color")]
        public GageColor key;
        // このアクションバーを表示する際に実行されるコマンド群 - Runtime側で実行される
        [LabelText("Show Commands")]
        public VNext.CommandListData showCommands = new VNext.CommandListData();
        [LabelText("Hide Commands")]
        public VNext.CommandListData hideCommands = new VNext.CommandListData();
    }
    public interface IPlayerGameActionUISettings
    {
        RectTransform SelectorParentTransform { get; }
        RectTransform BarParentTransform { get; }
        GenericRuntimeTemplateSO ActionBarPrefab { get; }
        Vector3 ActionBarLocalPosition { get; }
        ActionBarUISetting[] ActionBarSettings { get; }
        VNext.CommandListData ShowActionBarCommands { get; }
        VNext.CommandListData HideActionBarCommands { get; }
        VNext.CommandListData StopSelectorCommands { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerGameActionUIMB : MonoBehaviour, IFeatureInstaller, IPlayerGameActionUISettings
    {
        [Header("Action Bar UI")]
        [SerializeField] RectTransform _selectorParentTransform = null!;
        [SerializeField] RectTransform _barParentTransform = null!;
        [SerializeField] GenericRuntimeTemplateSO _actionBarPrefab = null!;
        [SerializeField] Vector3 _actionBarLocalPosition = Vector3.zero;

        [BoxGroup("Action Bar Settings")]
        [LabelText("Color Commands")]
        [TableList(AlwaysExpanded = true)]
        [SerializeField] ActionBarUISetting[] _actionBarSettings = Array.Empty<ActionBarUISetting>();
        [BoxGroup("Commands")]
        [LabelText("Show Action Bar")]
        [SerializeField]
        [VNext.CommandListFunctionName("PlayerGameActionUI.ShowActionBar")]
        VNext.CommandListData _showActionBarCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Hide Action Bar")]
        [SerializeField]
        [VNext.CommandListFunctionName("PlayerGameActionUI.HideActionBar")]
        VNext.CommandListData _hideActionBarCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Stop Selector")]
        [SerializeField]
        [VNext.CommandListFunctionName("PlayerGameActionUI.StopSelector")]
        VNext.CommandListData _stopSelectorCommands = new();


        public RectTransform SelectorParentTransform => _selectorParentTransform;
        public RectTransform BarParentTransform => _barParentTransform;
        public GenericRuntimeTemplateSO ActionBarPrefab => _actionBarPrefab;
        public Vector3 ActionBarLocalPosition => _actionBarLocalPosition;
        public ActionBarUISetting[] ActionBarSettings => _actionBarSettings;
        public VNext.CommandListData ShowActionBarCommands => _showActionBarCommands;
        public VNext.CommandListData HideActionBarCommands => _hideActionBarCommands;
        public VNext.CommandListData StopSelectorCommands => _stopSelectorCommands;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scopeNode)
        {
            builder.RegisterInstance<IPlayerGameActionUISettings>(this);
            builder.Register<PlayerGameActionUIService>(RuntimeLifetime.Singleton)
                .As<IPlayerGameActionUIService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(scopeNode);
        }
    }
}
*/
