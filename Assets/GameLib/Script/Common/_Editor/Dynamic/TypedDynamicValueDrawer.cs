// Assets/GameLib/Script/Common/_Editor/Dynamic/TypedDynamicValueDrawer.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Game.Common;
using Game.DI;
using Game.Channel;
using Game.Movement;
using Game.StateMachine;
using Game.MaterialFx;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using Object = UnityEngine.Object;
using Game.Commands.VNext;
using Game.Health;
using Game.Profile;
using Game.StatusEffect;
using Game.Trait;

namespace Game.Common.Editor
{
    /// <summary>
    /// DynamicValue&lt;T&gt; 用の Odin Drawer。
    /// 型パラメータ T に基づいて、利用可能なソースを制限する。
    /// </summary>
    public sealed class TypedDynamicValueDrawer<T>
        : OdinValueDrawer<DynamicValue<T>>
    {
        static readonly Dictionary<string, bool> ExpandedStates = new();

        const float ButtonMinW = 44f;
        const float ButtonMaxW = 180f;
        const float ButtonPad = 10f;

        Type[] _allowedSourceTypes;
        string[] _sourceTypeNames;
        GUIContent[] _sourceTypeContents;

        protected override void Initialize()
        {
            base.Initialize();
            CacheAllowedSourceTypes();
        }

        void CacheAllowedSourceTypes()
        {
            var targetType = typeof(T);
            var allowedList = new List<Type>();

            // 1. Literal（型固定）
            var literalType = GetTypedLiteralSourceType(targetType);
            if (literalType != null)
                allowedList.Add(literalType);

            // 1.1 Catalog-based literal (BaseProfileData 派生 / IDynamicManagedRefValue 実装型)
            if (literalType == null && DynamicManagedRefSourceCatalog.TryGetLiteralSourceType(targetType, out var catalogLiteralType))
                allowedList.Add(catalogLiteralType);

            if (targetType == typeof(AnimationSpritePreset))
                allowedList.Add(typeof(AssetAnimationSpritePresetSource));
            if (targetType == typeof(StateMachinePreset))
                allowedList.Add(typeof(AssetStateMachinePresetSource));
            if (targetType == typeof(StateAnimationPreset))
                allowedList.Add(typeof(AssetStateAnimationPresetSource));
            if (targetType == typeof(HealthPreset))
                allowedList.Add(typeof(AssetHealthPresetSource));
            if (targetType == typeof(BaseStatusEffectDefinitionData))
                allowedList.Add(typeof(AssetStatusEffectDefinitionSource));
            if (targetType == typeof(StatusEffectStackPreset))
                allowedList.Add(typeof(AssetStatusEffectStackPresetSource));
            if (targetType == typeof(StatusEffectGlobalLifetimeSettings))
                allowedList.Add(typeof(AssetStatusEffectGlobalLifetimeSettingsSource));
            if (targetType == typeof(StatusEffectGlobalUseCooldownSettings))
                allowedList.Add(typeof(AssetStatusEffectGlobalUseCooldownSettingsSource));
            if (targetType == typeof(StatusEffectGlobalCountSettings))
                allowedList.Add(typeof(AssetStatusEffectGlobalCountSettingsSource));
            if (targetType == typeof(MotionPreset))
                allowedList.Add(typeof(AssetMotionPresetSource));
            if (targetType == typeof(TraitDefinitionSO))
            {
                allowedList.Add(typeof(AssetTraitDefinitionSource));
                allowedList.Add(typeof(HolderTraitDefinitionSource));
            }
            if (targetType == typeof(BaseRuntimeTemplatePreset))
            {
                allowedList.Add(typeof(AssetRuntimeTemplatePresetSource));
            }
            if (targetType == typeof(ParticleRuntimeTemplatePreset))
            {
                allowedList.Add(typeof(AssetParticleRuntimeTemplatePresetSource));
            }
            if (targetType == typeof(FirePatternRuntimeTemplatePreset))
            {
                allowedList.Add(typeof(AssetFirePatternRuntimeTemplatePresetSource));
            }
            if (targetType == typeof(SpawnPatternRuntimeTemplatePreset))
            {
                allowedList.Add(typeof(AssetSpawnPatternRuntimeTemplatePresetSource));
            }

            // 1.4 Catalog-based asset sources (IDynamicValueAsset<T> 実装 SO を自動収集)
            DynamicManagedRefSourceCatalog.AppendAssetSourceTypes(targetType, allowedList);

            // 1.5 Actor world position (Vector2/Vector3 only)
            if (targetType == typeof(Vector2))
            {
                allowedList.Add(typeof(ActorWorldPosition2Source));
                allowedList.Add(typeof(ActorDirectionDistance2Source));
                allowedList.Add(typeof(TargetChannelDirectionFromActor2Source));
                allowedList.Add(typeof(TargetChannelTargetPosition2Source));
                allowedList.Add(typeof(TransformAnimationChannelPosition2Source));
                allowedList.Add(typeof(TransformAnimationChannelDirection2Source));
                allowedList.Add(typeof(AreaChannelPosition2Source));
                allowedList.Add(typeof(SplitVector2Source));
                allowedList.Add(typeof(VisualBoundsValue2Source));
            }
            else if (targetType == typeof(Vector3))
            {
                allowedList.Add(typeof(ActorWorldPosition3Source));
                allowedList.Add(typeof(ActorDirectionDistance3Source));
                allowedList.Add(typeof(TargetChannelDirectionFromActor3Source));
                allowedList.Add(typeof(TargetChannelTargetPosition3Source));
                allowedList.Add(typeof(TransformAnimationChannelPosition3Source));
                allowedList.Add(typeof(TransformAnimationChannelDirection3Source));
                allowedList.Add(typeof(AreaChannelPosition3Source));
                allowedList.Add(typeof(SplitVector3Source));
                allowedList.Add(typeof(VisualBoundsValue3Source));
            }

            // 2. Expression sources
            if (targetType == typeof(bool))
            {
                allowedList.Add(typeof(BoolExpressionSource));
                allowedList.Add(typeof(GameStateMachineCompareBoolSource));
                allowedList.Add(typeof(StateMachineOptionIsSetBoolSource));
                allowedList.Add(typeof(ActorDistanceCompareBoolSource));
                allowedList.Add(typeof(ActorSourceExistsSource));
                allowedList.Add(typeof(SharedActorSourceExistsSource));
                allowedList.Add(typeof(UIModalStackActorMatchSource));
                allowedList.Add(typeof(DataExistsBoolSource));
                allowedList.Add(typeof(StatusEffectOperationEnabledBoolSource));
            }
            else if (targetType == typeof(int))
            {
                allowedList.Add(typeof(IntExpressionSource));
                allowedList.Add(typeof(GameStateMachineStateIdIntSource));
            }
            else if (targetType == typeof(float))
            {
                allowedList.Add(typeof(FloatExpressionSource));
                allowedList.Add(typeof(TimerValueSource));
                allowedList.Add(typeof(ActorWorldPositionXSource));
                allowedList.Add(typeof(ActorWorldPositionYSource));
                allowedList.Add(typeof(TransformAnimationChannelAngleSource));
            }
            else if (targetType == typeof(Vector2))
            {
                allowedList.Add(typeof(Vector2ExpressionSource));
                allowedList.Add(typeof(Vector2XYExpressionSource));
            }
            else if (targetType == typeof(Vector3))
            {
                allowedList.Add(typeof(Vector3ExpressionSource));
                allowedList.Add(typeof(Vector3XYZExpressionSource));
            }

            // 2.5 Random sources
            if (targetType == typeof(int))
            {
                allowedList.Add(typeof(RandomIntRangeSource));
                allowedList.Add(typeof(RandomWeightedIntListSource));
            }
            else if (targetType == typeof(float))
            {
                allowedList.Add(typeof(RandomFloatRangeSource));
                allowedList.Add(typeof(RandomWeightedFloatListSource));
            }
            else if (targetType == typeof(bool))
                allowedList.Add(typeof(RandomBoolSource));
            else if (targetType == typeof(Vector2))
                allowedList.Add(typeof(RandomVector2RangeSource));
            else if (targetType == typeof(Vector3))
                allowedList.Add(typeof(RandomVector3RangeSource));
            else if (targetType == typeof(Vector4))
                allowedList.Add(typeof(RandomVector4RangeSource));
            else if (targetType == typeof(string))
            {
                allowedList.Add(typeof(RandomWeightedStringListSource));
                allowedList.Add(typeof(RichTextSource));
                allowedList.Add(typeof(StatusEffectStackDescriptionSource));
                allowedList.Add(typeof(SceneNameSource));
                allowedList.Add(typeof(SharedActorSourceTagSource));
            }
            else if (targetType == typeof(Color))
                allowedList.Add(typeof(RandomWeightedColorListSource));
            else if (targetType == typeof(MaterialFxPayload))
                allowedList.Add(typeof(RandomMaterialFxSource));

            // 2.75 Random weighted runtime template pick
            if (typeof(BaseRuntimeTemplateSO).IsAssignableFrom(targetType))
                allowedList.Add(typeof(RandomWeightedRuntimeTemplateListSource));
            if (typeof(BaseRuntimeTemplatePreset).IsAssignableFrom(targetType))
                allowedList.Add(typeof(RandomWeightedRuntimeTemplatePresetListSource));

            // 3. UnityObject 派生型の場合は UnityObjectRefSource<T>
            if (typeof(Object).IsAssignableFrom(targetType))
            {
                var genericRefType = typeof(UnityObjectRefSource<>).MakeGenericType(targetType);
                allowedList.Add(genericRefType);
            }

            // 4. 汎用ソース（Var/Blackboard）
            allowedList.Add(typeof(VarStoreSource));
            allowedList.Add(typeof(SelfBlackboardSource));
            allowedList.Add(typeof(OtherBlackboardSource));

            if (targetType == typeof(int)
                || targetType == typeof(float)
                || targetType == typeof(bool)
                || targetType == typeof(string)
                || targetType == typeof(Vector2)
                || targetType == typeof(Vector3)
                || targetType == typeof(Vector4)
                || targetType == typeof(Color))
            {
                allowedList.Add(typeof(SelfGridBlackboardSource));
                allowedList.Add(typeof(OtherGridBlackboardSource));
            }

            if (targetType == typeof(int))
            {
                allowedList.Add(typeof(SelfGridBlackboardColumnCountSource));
                allowedList.Add(typeof(OtherGridBlackboardColumnCountSource));
                allowedList.Add(typeof(SelfGridBlackboardRowCountSource));
            }

            // 5. Scalar（int / float / string に対応）
            if (targetType == typeof(int) || targetType == typeof(float) || targetType == typeof(string))
            {
                // Allow scalar sources for numeric/string values.
                // DynamicVariant handles float -> int / string conversions.
                allowedList.Add(typeof(SelfScalarSource));
                allowedList.Add(typeof(OtherScalarSource));
            }

            _allowedSourceTypes = allowedList.ToArray();
            _sourceTypeNames = _allowedSourceTypes.Select(t => GetSourceDisplayName(t)).ToArray();
            _sourceTypeContents = _sourceTypeNames.Select(n => new GUIContent(n)).ToArray();
        }

        static Type GetTypedLiteralSourceType(Type targetType)
        {
            if (targetType == typeof(int)) return typeof(LiteralIntSource);
            if (targetType == typeof(float)) return typeof(LiteralFloatSource);
            if (targetType == typeof(bool)) return typeof(LiteralBoolSource);
            if (targetType == typeof(string)) return typeof(LiteralStringSource);
            if (targetType == typeof(Vector2)) return typeof(LiteralVector2Source);
            if (targetType == typeof(Vector3)) return typeof(LiteralVector3Source);
            if (targetType == typeof(Vector4)) return typeof(LiteralVector4Source);
            if (targetType == typeof(Color)) return typeof(LiteralColorSource);
            if (targetType == typeof(AnimationSpritePreset)) return typeof(LiteralAnimationSpritePresetSource);
            if (targetType == typeof(StateMachinePreset)) return typeof(LiteralStateMachinePresetSource);
            if (targetType == typeof(StateAnimationPreset)) return typeof(LiteralStateAnimationPresetSource);
            if (targetType == typeof(HealthPreset)) return typeof(LiteralHealthPresetSource);
            if (targetType == typeof(MaterialFxPayload)) return typeof(LiteralMaterialFxPayloadSource);
            if (targetType == typeof(MotionPreset)) return typeof(LiteralMotionPresetSource);
            if (targetType == typeof(TransformAnimationPreset)) return typeof(LiteralTransformAnimationPresetSource);
            if (targetType == typeof(CommandListData)) return typeof(LiteralCommandListDataSource);
            if (targetType == typeof(BaseStatusEffectDefinitionData)) return typeof(LiteralStatusEffectDefinitionSource);
            if (targetType == typeof(StatusEffectStackPreset)) return typeof(LiteralStatusEffectStackPresetSource);
            if (targetType == typeof(StatusEffectGlobalLifetimeSettings)) return typeof(LiteralStatusEffectGlobalLifetimeSettingsSource);
            if (targetType == typeof(StatusEffectGlobalUseCooldownSettings)) return typeof(LiteralStatusEffectGlobalUseCooldownSettingsSource);
            if (targetType == typeof(StatusEffectGlobalCountSettings)) return typeof(LiteralStatusEffectGlobalCountSettingsSource);
            if (targetType == typeof(BaseRuntimeTemplatePreset)) return typeof(LiteralRuntimeTemplatePresetSource);
            if (targetType == typeof(ParticleRuntimeTemplatePreset)) return typeof(LiteralParticleRuntimeTemplatePresetSource);
            if (targetType == typeof(FirePatternRuntimeTemplatePreset)) return typeof(LiteralFirePatternRuntimeTemplatePresetSource);
            if (targetType == typeof(SpawnPatternRuntimeTemplatePreset)) return typeof(LiteralSpawnPatternRuntimeTemplatePresetSource);
            return null;
        }

        static string GetSourceDisplayName(Type sourceType)
        {
            if (sourceType == null) return "None";

            if (sourceType == typeof(SelfGridBlackboardSource)) return "Grid Cell Value (Self)";
            if (sourceType == typeof(OtherGridBlackboardSource)) return "Grid Cell Value (Other)";
            if (sourceType == typeof(SelfGridBlackboardColumnCountSource)) return "Grid Column Count At Row (Self)";
            if (sourceType == typeof(OtherGridBlackboardColumnCountSource)) return "Grid Column Count At Row (Other)";
            if (sourceType == typeof(SelfGridBlackboardRowCountSource)) return "Grid Row Count (Self)";

            var name = sourceType.Name;

            // Generic types
            if (sourceType.IsGenericType)
            {
                var genericDef = sourceType.GetGenericTypeDefinition();
                if (genericDef == typeof(ManagedRefLiteralSource<>))
                    return "Literal";
                if (genericDef == typeof(ManagedRefAssetSource<,>))
                    return "Asset";

                var baseName = name.Substring(0, name.IndexOf('`'));
                var args = sourceType.GetGenericArguments();
                return $"{baseName}<{string.Join(", ", args.Select(a => a.Name))}>";
            }

            // Remove "Source" suffix for cleaner display
            if (name.EndsWith("Source"))
                name = name.Substring(0, name.Length - 6);

            return name;
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            try
            {
                // MotionPreset は専用の簡易描画を使う。
                // 目的:
                // - DynamicValue<MotionPreset> の型候補を Motion 用に限定
                // - ShowIf/再描画の相性でフィールドが消えるケースを回避
                if (typeof(T) == typeof(MotionPreset))
                {
                    DrawMotionPresetLayout(label);
                    return;
                }

                var root = Property;
                var sourceProp = FindChild(root, "_source");
                if (sourceProp == null)
                {
                    CallNextDrawer(label);
                    return;
                }

                var defaultLiteralAttr = Property.GetAttribute<DynamicValueDefaultLiteralAttribute>();

                var target = root.Tree?.UnitySerializedObject?.targetObject;
                var key = $"{target?.GetInstanceID() ?? 0}:{root.Path}";

                if (!ExpandedStates.TryGetValue(key, out var expanded))
                    ExpandedStates[key] = expanded = false;

                var currentSource = sourceProp.ValueEntry?.WeakSmartValue as IDynamicSource;
                if (currentSource == null)
                {
                    var literalType = GetTypedLiteralSourceType(typeof(T));
                    if (literalType != null)
                    {
                        var newSource = DynamicValueDefaultLiteralHelper.CreateFromAttribute(literalType, defaultLiteralAttr)
                            ?? (IDynamicSource)Activator.CreateInstance(literalType);
                        sourceProp.ValueEntry.WeakSmartValue = newSource;
                        currentSource = newSource;
                    }
                }
                else if (TryConvertLegacyLiteralSource(sourceProp, currentSource, out var converted))
                {
                    sourceProp.ValueEntry.WeakSmartValue = converted;
                    currentSource = converted;
                }

                var currentIndex = GetCurrentSourceIndex(currentSource);

                // --- 1行目：Label + Type選択 + ボタン ---
                EditorGUILayout.BeginHorizontal();
                {
                    // Label
                    if (label != null && !string.IsNullOrEmpty(label.text))
                        GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

                    // Type選択（Popup）
                    EditorGUI.BeginChangeCheck();
                    var newIndex = EditorGUILayout.Popup(currentIndex, _sourceTypeContents, GUILayout.MinWidth(100f));
                    if (EditorGUI.EndChangeCheck() && newIndex != currentIndex && newIndex >= 0 && newIndex < _allowedSourceTypes.Length)
                    {
                        var newType = _allowedSourceTypes[newIndex];
                        var newSource = DynamicValueDefaultLiteralHelper.CreateFromAttribute(newType, defaultLiteralAttr)
                            ?? (IDynamicSource)Activator.CreateInstance(newType);
                        sourceProp.ValueEntry.WeakSmartValue = newSource;
                    }

                    // ボタン
                    var buttonText = BuildButtonLabel(sourceProp, currentSource);
                    var btnContent = new GUIContent(buttonText)
                    {
                        tooltip = currentSource?.SourceTypeName ?? "None"
                    };
                    var btnW = Mathf.Clamp(
                        EditorStyles.miniButton.CalcSize(btnContent).x + ButtonPad,
                        ButtonMinW, ButtonMaxW);

                    var oldBg = GUI.backgroundColor;
                    if (expanded) GUI.backgroundColor = new Color(0.85f, 0.95f, 1.0f);

                    if (GUILayout.Button(btnContent, EditorStyles.miniButton, GUILayout.Width(btnW)))
                        ExpandedStates[key] = expanded = !expanded;

                    GUI.backgroundColor = oldBg;
                }
                EditorGUILayout.EndHorizontal();

                var headerRect = GUILayoutUtility.GetLastRect();
                var evt = Event.current;
                if (evt != null && evt.type == EventType.ContextClick && headerRect.Contains(evt.mousePosition))
                {
                    if (currentSource is FloatExpressionSource || currentSource is IntExpressionSource)
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Open Graph Preview"), false, () =>
                        {
                            ExpressionGraphPreviewWindow.Open(currentSource);
                        });
                        menu.ShowAsContext();
                        evt.Use();
                    }
                }

                // --- 展開：Sourceの中身を描画 ---
                if (!expanded) return;

                EditorGUI.indentLevel++;
                if (currentSource == null)
                {
                    EditorGUILayout.HelpBox("Source is null. Select a Type.", MessageType.Info);
                }
                else
                {
                    // children を描画（深い SerializeReference 構造でも壊れにくいように、
                    // 可視ノードのみをスナップショット経由で安全描画する）
                    sourceProp.State.Expanded = true;
                    var snapshot = new List<InspectorProperty>(sourceProp.Children.Count);
                    for (int i = 0; i < sourceProp.Children.Count; i++)
                    {
                        snapshot.Add(sourceProp.Children[i]);
                    }

                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        var c = snapshot[i];
                        if (c == null)
                            continue;
                        if (!c.State.Visible)
                            continue;
                        if (!string.IsNullOrEmpty(c.Name) && c.Name[0] == '$')
                            continue;
                        if (!string.IsNullOrEmpty(c.Name) && c.Name[0] == '#')
                            c.State.Expanded = true;

                        DrawChildSafely(c);
                    }
                }
                EditorGUI.indentLevel--;
            }
            catch (Exception ex)
            {
                if (ex is ExitGUIException)
                    throw;

                Debug.LogWarning($"[TypedDynamicValueDrawer<{typeof(T).Name}>] Fallback to default drawer due to error: {ex.Message}");
                CallNextDrawer(label);
            }
        }

        void DrawMotionPresetLayout(GUIContent label)
        {
            var root = Property;
            var sourceProp = FindChild(root, "_source");
            if (sourceProp == null || sourceProp.ValueEntry == null)
            {
                CallNextDrawer(label);
                return;
            }

            var motionSourceTypes = new[] { typeof(LiteralMotionPresetSource), typeof(AssetMotionPresetSource) };
            var motionSourceNames = new[] { "Literal", "Asset" };

            var currentSource = sourceProp.ValueEntry?.WeakSmartValue as IDynamicSource;
            if (currentSource == null ||
                (currentSource.GetType() != motionSourceTypes[0] && currentSource.GetType() != motionSourceTypes[1]))
            {
                sourceProp.ValueEntry.WeakSmartValue = Activator.CreateInstance(motionSourceTypes[0]);
                currentSource = sourceProp.ValueEntry.WeakSmartValue as IDynamicSource;
            }

            int currentIndex = currentSource != null && currentSource.GetType() == motionSourceTypes[1] ? 1 : 0;

            EditorGUILayout.BeginHorizontal();
            if (label != null && !string.IsNullOrEmpty(label.text))
                GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUILayout.Popup(currentIndex, motionSourceNames, GUILayout.MinWidth(100f));
            if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < motionSourceTypes.Length && nextIndex != currentIndex)
            {
                sourceProp.ValueEntry.WeakSmartValue = Activator.CreateInstance(motionSourceTypes[nextIndex]);
                currentSource = sourceProp.ValueEntry.WeakSmartValue as IDynamicSource;
            }
            EditorGUILayout.EndHorizontal();

            sourceProp.State.Expanded = true;
            EditorGUI.indentLevel++;

            // MotionPreset では value 子のみを描画し、SerializeReference 再描画時の子列挙崩れを避ける。
            var valueProp = FindChild(sourceProp, "value");
            if (valueProp != null)
            {
                DrawChildSafely(valueProp);
            }
            else
            {
                DrawChildSafely(sourceProp);
            }

            EditorGUI.indentLevel--;
        }

        static void DrawChildSafely(InspectorProperty child)
        {
            if (child == null)
                return;

            var oldLabelWidth = EditorGUIUtility.labelWidth;
            var oldIndent = EditorGUI.indentLevel;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldContentColor = GUI.contentColor;
            var oldBackgroundColor = GUI.backgroundColor;
            try
            {
                child.Draw();
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUI.indentLevel = oldIndent;
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
            }
        }

        int GetCurrentSourceIndex(IDynamicSource source)
        {
            if (source == null) return -1;

            var sourceType = source.GetType();
            for (int i = 0; i < _allowedSourceTypes.Length; i++)
            {
                if (_allowedSourceTypes[i] == sourceType)
                    return i;
            }
            return -1;
        }

        static InspectorProperty FindChild(InspectorProperty parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.Children.Count; i++)
            {
                var c = parent.Children[i];
                if (c.Name == name) return c;
            }
            return null;
        }

        static string BuildButtonLabel(InspectorProperty sourceProp, IDynamicSource src)
        {
            var initials = GetTypeInitials(src?.SourceTypeName);
            if (string.IsNullOrEmpty(initials))
                return "None";

            if (TryGetButtonDetail(sourceProp, src, out var detail) && !string.IsNullOrEmpty(detail))
                return TrimForButton(detail, 48);

            return initials;
        }

        static string TrimForButton(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var singleLine = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (singleLine.Length <= maxLength)
                return singleLine;

            return singleLine.Substring(0, maxLength - 3) + "...";
        }

        static string GetTypeInitials(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var ch in typeName)
            {
                if (char.IsUpper(ch)) sb.Append(ch);
            }
            if (sb.Length > 0)
                return sb.ToString();

            return typeName.Substring(0, 1).ToUpperInvariant();
        }

        static bool TryGetButtonDetail(InspectorProperty sourceProp, IDynamicSource src, out string detail)
        {
            detail = null;
            if (sourceProp == null || src == null)
                return false;

            var sourceType = src.GetType();
            if (sourceType.IsGenericType)
            {
                var genericDef = sourceType.GetGenericTypeDefinition();
                if (genericDef == typeof(ManagedRefLiteralSource<>))
                    return TryGetManagedRefLiteralDetail(sourceProp, out detail);

                if (genericDef == typeof(ManagedRefAssetSource<,>))
                    return TryGetManagedRefAssetDetail(sourceProp, out detail);
            }

            if (src is BindingPresetAssetSource)
            {
                if (TryGetChildValue(sourceProp, "_asset", out var assetRaw) && assetRaw is Object assetObj)
                {
                    detail = assetObj != null ? assetObj.name : "null";
                    return true;
                }

                return TryGetChildValueAsString(sourceProp, "_asset", out detail);
            }

            if (src is LiteralTransformAnimationPresetSource)
            {
                if (TryGetChildValue(sourceProp, "value", out var raw) && raw is TransformAnimationPreset preset)
                {
                    detail = $"{preset.Steps?.Count ?? 0} steps";
                    return true;
                }

                return TryGetChildValueAsString(sourceProp, "value", out detail);
            }

            // 型固定 Literal
            if (src is LiteralIntSource or LiteralFloatSource or LiteralBoolSource
                or LiteralStringSource or LiteralVector2Source or LiteralVector3Source
                or LiteralVector4Source or LiteralColorSource
                or LiteralAnimationSpritePresetSource or LiteralMaterialFxPayloadSource
                or LiteralMotionPresetSource or LiteralTransformAnimationPresetSource or LiteralRuntimeTemplatePresetSource
                or LiteralParticleRuntimeTemplatePresetSource or LiteralFirePatternRuntimeTemplatePresetSource
                or LiteralSpawnPatternRuntimeTemplatePresetSource)
            {
                return TryGetChildValueAsString(sourceProp, "value", out detail);
            }

            // Expression Sources
            if (src is BoolExpressionSource or FloatExpressionSource)
            {
                if (TryGetChildValueAsString(sourceProp, "_expression", out detail))
                {
                    // 長すぎる場合は省略
                    if (detail != null && detail.Length > 20)
                        detail = detail.Substring(0, 17) + "...";
                    return true;
                }
                return false;
            }

            // Variable系
            switch (src)
            {
                case VarStoreSource:
                    return TryGetVarKeyDetail(sourceProp, out detail);
                case SelfBlackboardSource:
                case OtherBlackboardSource:
                    return TryGetChildValueAsString(sourceProp, "blackboardKey", out detail);
                case SelfScalarSource:
                case OtherScalarSource:
                    return TryGetChildValueAsString(sourceProp, "scalarKey", out detail);
            }

            // UnityObject系
            if (TryGetChildValue(sourceProp, "objectValue", out var obj) && obj is Object unityObj)
            {
                detail = unityObj != null ? (!string.IsNullOrEmpty(unityObj.name) ? unityObj.name : unityObj.GetType().Name) : "null";
                return true;
            }

            if (src is TransformAnimationChannelPosition2Source
                or TransformAnimationChannelPosition3Source
                or TransformAnimationChannelDirection2Source
                or TransformAnimationChannelDirection3Source
                or TransformAnimationChannelAngleSource)
            {
                return TryGetChildValueAsString(sourceProp, "channelTag", out detail);
            }

            if (src is SharedActorSourceExistsSource or SharedActorSourceTagSource)
            {
                return TryGetChildValueAsString(sourceProp, "sharedHubActorSource", out detail);
            }

            return false;
        }

        static bool TryGetManagedRefLiteralDetail(InspectorProperty sourceProp, out string detail)
        {
            detail = null;
            if (!TryGetChildValue(sourceProp, "value", out var raw))
                return false;

            if (raw == null)
            {
                detail = "null";
                return true;
            }

            if (raw is BaseProfileData profileData)
            {
                var name = GetProfileDisplayName(profileData);
                var count = profileData.GetBindingCount();
                detail = $"{name} [b:{count}]";
                return true;
            }

            if (raw is ScriptableObject so)
            {
                detail = !string.IsNullOrEmpty(so.name) ? so.name : so.GetType().Name;
                return true;
            }

            detail = raw.ToString();
            return !string.IsNullOrEmpty(detail);
        }

        static bool TryGetManagedRefAssetDetail(InspectorProperty sourceProp, out string detail)
        {
            detail = null;
            if (!TryGetChildValue(sourceProp, "value", out var raw))
                return false;

            if (raw == null)
            {
                detail = "null";
                return true;
            }

            if (raw is ScriptableObject so)
            {
                detail = !string.IsNullOrEmpty(so.name) ? so.name : so.GetType().Name;
                return true;
            }

            detail = raw.ToString();
            return !string.IsNullOrEmpty(detail);
        }

        static string GetProfileDisplayName(BaseProfileData profileData)
        {
            if (profileData == null)
                return "null";

            if (profileData is CustomProfileDefinition custom && !string.IsNullOrEmpty(custom.ProfileName))
                return custom.ProfileName;

            var toString = profileData.ToString();
            if (!string.IsNullOrEmpty(toString)
                && !string.Equals(toString, profileData.GetType().Name, StringComparison.Ordinal)
                && !string.Equals(toString, profileData.GetType().FullName, StringComparison.Ordinal))
            {
                return toString;
            }

            return profileData.GetType().Name;
        }

        static bool TryGetVarKeyDetail(InspectorProperty sourceProp, out string detail)
        {
            detail = null;
            var keyProp = FindChild(sourceProp, "key");
            if (keyProp == null)
                return false;

            if (TryGetChildValueAsString(keyProp, "stableKey", out var stable) && !string.IsNullOrEmpty(stable))
            {
                detail = stable;
                return true;
            }

            if (TryGetChildValueAsString(keyProp, "varId", out var id))
            {
                detail = id;
                return true;
            }

            return false;
        }

        static bool TryGetChildValue(InspectorProperty parent, string childName, out object value)
        {
            value = null;
            var child = FindChild(parent, childName);
            if (child?.ValueEntry == null)
                return false;

            value = child.ValueEntry.WeakSmartValue;
            return true;
        }

        static bool TryGetChildValueAsString(InspectorProperty parent, string childName, out string value)
        {
            value = null;
            if (!TryGetChildValue(parent, childName, out var raw))
                return false;

            if (raw == null)
            {
                value = "null";
                return true;
            }

            value = raw.ToString();
            return true;
        }

        static bool TryConvertLegacyLiteralSource(InspectorProperty sourceProp, IDynamicSource currentSource, out IDynamicSource converted)
        {
            converted = null!;
            if (currentSource is not LiteralSource)
                return false;

            if (!TryGetChildValue(sourceProp, "type", out var typeObj) || typeObj is not LiteralSource.LiteralType literalType)
                return false;

            var targetType = typeof(T);

            if (targetType == typeof(int) && literalType == LiteralSource.LiteralType.Int && TryGetChildValue(sourceProp, "intValue", out var iv) && iv is int i)
            {
                converted = new LiteralIntSource(i);
                return true;
            }

            if (targetType == typeof(float) && literalType == LiteralSource.LiteralType.Float && TryGetChildValue(sourceProp, "floatValue", out var fv) && fv is float f)
            {
                converted = new LiteralFloatSource(f);
                return true;
            }

            if (targetType == typeof(bool) && literalType == LiteralSource.LiteralType.Bool && TryGetChildValue(sourceProp, "boolValue", out var bv) && bv is bool b)
            {
                converted = new LiteralBoolSource(b);
                return true;
            }

            if (targetType == typeof(string) && literalType == LiteralSource.LiteralType.String && TryGetChildValue(sourceProp, "stringValue", out var sv) && sv is string s)
            {
                converted = new LiteralStringSource(s);
                return true;
            }

            if (targetType == typeof(Vector2) && literalType == LiteralSource.LiteralType.Vector2 && TryGetChildValue(sourceProp, "vector2Value", out var v2) && v2 is Vector2 vec2)
            {
                converted = new LiteralVector2Source(vec2);
                return true;
            }

            if (targetType == typeof(Vector3) && literalType == LiteralSource.LiteralType.Vector3 && TryGetChildValue(sourceProp, "vector3Value", out var v3) && v3 is Vector3 vec3)
            {
                converted = new LiteralVector3Source(vec3);
                return true;
            }

            if (targetType == typeof(Vector4) && literalType == LiteralSource.LiteralType.Vector4 && TryGetChildValue(sourceProp, "vector4Value", out var v4) && v4 is Vector4 vec4)
            {
                converted = new LiteralVector4Source(vec4);
                return true;
            }

            if (targetType == typeof(Color) && literalType == LiteralSource.LiteralType.Color && TryGetChildValue(sourceProp, "colorValue", out var c) && c is Color col)
            {
                converted = new LiteralColorSource(col);
                return true;
            }

            return false;
        }
    }
}
#endif
