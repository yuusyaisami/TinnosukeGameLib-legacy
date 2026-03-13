// Game.Save.SaveDataTypes.cs
//
// 保存データの構造体定義

using System;

namespace Game.Save
{
    // ================================================================
    // Blackboard Save Data
    // ================================================================

    /// <summary>
    /// Blackboard 保存データのルート。
    /// </summary>
    [Serializable]
    public struct BlackboardSave
    {
        public int SaveVer;
        public BlackboardRecord[] Records;

        public static BlackboardSave Empty => new BlackboardSave
        {
            SaveVer = 1,
            Records = Array.Empty<BlackboardRecord>()
        };
    }

    /// <summary>
    /// Blackboard 変数レコード。
    /// </summary>
    [Serializable]
    public struct BlackboardRecord
    {
        public string Key;
        public string Type;  // Int, Float, Bool, String, Vector2, Vector3, Color, Unknown
        public string Value; // InvariantCulture 文字列
    }

    // ================================================================
    // Scalar Save Data
    // ================================================================

    /// <summary>
    /// Scalar 保存データのルート。
    /// </summary>
    [Serializable]
    public struct ScalarSave
    {
        public int SaveVer;
        public ScalarRecord[] Records;

        public static ScalarSave Empty => new ScalarSave
        {
            SaveVer = 1,
            Records = Array.Empty<ScalarRecord>()
        };
    }

    /// <summary>
    /// Scalar キーごとの保存レコード。
    /// </summary>
    [Serializable]
    public struct ScalarRecord
    {
        public int KeyId;
        public float LocalBase;
        public ScalarPersistentLayerData[] Layers;
    }

    /// <summary>
    /// 計算レイヤーごとの集約データ。
    /// </summary>
    [Serializable]
    public struct ScalarPersistentLayerData
    {
        public string CalcLayer;
        public float AddSum;
        public float PreMul;
        public float PostMul;

        public bool HasEffect =>
            !IsZero(AddSum) || !IsOne(PreMul) || !IsOne(PostMul);

        static bool IsZero(float v) => Math.Abs(v) < 1e-6f;
        static bool IsOne(float v) => Math.Abs(v - 1f) < 1e-6f;
    }

    // ================================================================
    // Profile Data
    // ================================================================

    /// <summary>
    /// プロファイル情報。
    /// </summary>
    [Serializable]
    public struct SaveProfile
    {
        public string ProfileId;
        public string DisplayName;
        public long CreatedAt;
        public long LastSavedAt;
    }

    /// <summary>
    /// プロファイル一覧のメタデータ。
    /// </summary>
    [Serializable]
    public struct ProfileMeta
    {
        public string ActiveProfileId;
        public SaveProfile[] Profiles;

        public static ProfileMeta Default => new ProfileMeta
        {
            ActiveProfileId = "default",
            Profiles = new[] { new SaveProfile { ProfileId = "default", DisplayName = "Default" } }
        };
    }
}
