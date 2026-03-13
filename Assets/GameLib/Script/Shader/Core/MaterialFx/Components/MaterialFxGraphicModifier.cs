using UnityEngine;
using UnityEngine.UI;

namespace Game.MaterialFx
{
    /// <summary>
    /// Unity UI の IMaterialModifier パイプラインに統合し、
    /// Mask/RectMask2D のステンシル処理と MaterialFx の変更を両立させるコンポーネント。
    /// GraphicAdapter から自動的に追加・管理される。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class MaterialFxGraphicModifier : MonoBehaviour, IMaterialModifier
    {
        Graphic _graphic;
        Material _modifiedMaterial;
        Material _lastBaseMaterial;
        bool _materialDirty = true;

        /// <summary>
        /// MaterialFx が変更を適用するベースマテリアル。
        /// このマテリアルに対して SetFloat/SetColor 等を呼び出す。
        /// GetModifiedMaterial が呼ばれるまでは null になる可能性がある。
        /// </summary>
        public Material MaterialInstance => _modifiedMaterial;

        /// <summary>
        /// マテリアルインスタンスが有効かどうか。
        /// </summary>
        public bool HasMaterialInstance => _modifiedMaterial != null;

        /// <summary>
        /// マテリアルインスタンスを即座に取得または作成する。
        /// GetModifiedMaterial がまだ呼ばれていない場合は、Graphic のベースマテリアルからクローンを作成する。
        /// </summary>
        public Material EnsureMaterialInstance()
        {
            if (_modifiedMaterial != null)
                return _modifiedMaterial;

            // GetModifiedMaterial がまだ呼ばれていない場合、
            // Graphic の material（共有マテリアル）から直接クローンを作成
            // 注意: materialForRendering は IMaterialModifier パイプラインを通った結果なので
            //       ここで呼ぶと循環参照になる可能性がある。material プロパティを使う。
            if (_graphic == null)
                _graphic = GetComponent<Graphic>();

            if (_graphic == null)
                return null;

            // material プロパティはデフォルトマテリアル（ベースシェーダー）を返す
            var baseMaterial = _graphic.material;
            if (baseMaterial == null)
                return null;

            _modifiedMaterial = new Material(baseMaterial)
            {
                name = baseMaterial.name + " (MaterialFx)"
            };
            _lastBaseMaterial = baseMaterial;
            _materialDirty = false;

            // 注意: ここで SetMaterialDirty を呼ぶと無限ループの原因になるため呼ばない。
            // GetModifiedMaterial は Canvas の更新時に自動的に呼ばれる。

            return _modifiedMaterial;
        }

        /// <summary>
        /// マテリアルの再生成をリクエストする。
        /// MaterialFx のプロパティ変更後に呼び出す。
        /// </summary>
        public void SetMaterialDirty()
        {
            _materialDirty = true;
            if (_graphic != null)
                _graphic.SetMaterialDirty();
        }

        void Awake()
        {
            _graphic = GetComponent<Graphic>();
        }

        void OnEnable()
        {
            if (_graphic != null)
                _graphic.SetMaterialDirty();
        }

        void OnDestroy()
        {
            if (_modifiedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_modifiedMaterial);
                else
                    DestroyImmediate(_modifiedMaterial);
                _modifiedMaterial = null;
            }
        }

        /// <summary>
        /// Unity UI の IMaterialModifier.GetModifiedMaterial 実装。
        /// Mask 等の他の Modifier の後に呼び出され、最終的なマテリアルを返す。
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!enabled || baseMaterial == null)
                return baseMaterial;

            // baseMaterial が変わった場合（Mask の追加/削除など）
            bool baseMaterialChanged = _lastBaseMaterial != baseMaterial;
            _lastBaseMaterial = baseMaterial;

            // baseMaterial は Mask 等によって既にステンシル設定が適用されている。
            // これをクローンして MaterialFx 用のインスタンスを作成する。
            if (_modifiedMaterial == null)
            {
                // 新規作成
                _modifiedMaterial = new Material(baseMaterial)
                {
                    name = baseMaterial.name + " (MaterialFx)"
                };
                _materialDirty = false;
            }
            else if (baseMaterialChanged)
            {
                // シェーダーが変わった場合のみ再作成
                // ステンシル設定だけの変更なら、プロパティのコピーで対応
                if (_modifiedMaterial.shader != baseMaterial.shader)
                {
                    if (Application.isPlaying)
                        Destroy(_modifiedMaterial);
                    else
                        DestroyImmediate(_modifiedMaterial);

                    _modifiedMaterial = new Material(baseMaterial)
                    {
                        name = baseMaterial.name + " (MaterialFx)"
                    };
                }
                else
                {
                    // シェーダーは同じなので、ステンシル設定のみをコピー
                    // MaterialFx で設定したプロパティは保持される
                    CopyStencilProperties(baseMaterial, _modifiedMaterial);
                }
                _materialDirty = false;
            }
            else if (_materialDirty)
            {
                // ステンシル設定のみを同期し、MaterialFx が設定した他のプロパティは保持
                CopyStencilProperties(baseMaterial, _modifiedMaterial);
                _materialDirty = false;
            }

            return _modifiedMaterial;
        }

        /// <summary>
        /// ステンシル関連のプロパティのみをソースからターゲットにコピーする。
        /// </summary>
        static void CopyStencilProperties(Material source, Material target)
        {
            // Unity UI Stencil Properties
            CopyFloatPropertyIfExists(source, target, "_Stencil");
            CopyFloatPropertyIfExists(source, target, "_StencilComp");
            CopyFloatPropertyIfExists(source, target, "_StencilOp");
            CopyFloatPropertyIfExists(source, target, "_StencilWriteMask");
            CopyFloatPropertyIfExists(source, target, "_StencilReadMask");
            CopyFloatPropertyIfExists(source, target, "_ColorMask");
            CopyFloatPropertyIfExists(source, target, "_UseUIAlphaClip");
        }

        static void CopyFloatPropertyIfExists(Material source, Material target, string propertyName)
        {
            if (!source.HasProperty(propertyName) || !target.HasProperty(propertyName))
                return;

            var propId = Shader.PropertyToID(propertyName);
            target.SetFloat(propId, source.GetFloat(propId));
        }
    }
}
