#ifndef GAME_ADVANCEDFLIP2D_INCLUDED
#define GAME_ADVANCEDFLIP2D_INCLUDED

// ============================================================================
// AdvancedFlip2D - 擬似3D回転 + 奥行き縮小 + 歪み（せん断/曲げ）
// 仕様書 Section 4-9 準拠
// ============================================================================

struct AdvancedFlip2DParams
{
    float  enabled;           // 0 = disabled, 1 = enabled
    float  eulerDegX;         // Euler X (deg)
    float  eulerDegY;         // Euler Y (deg)
    float  eulerDegZ;         // Euler Z (deg)
    float2 pivotLocal;        // ローカル座標での回転中心
    
    // 奥行き（擬似透視）
    float  perspective;       // 奥行きによる縮小の強さ (0=無効)
    float  depthScale;        // 回転で生成されるzをどれくらい奥行きとして扱うか
    float  perspSign;         // +1 / -1 環境による奥行き方向補正
    float  scaleClampMin;     // 縮小下限
    float  scaleClampMax;     // 拡大上限
    
    // 歪み（Warp）
    float2 shear;             // せん断 (x: X += Y * shearX, y: Y += X * shearY)
    float2 bend;              // 曲げ (signed quadratic)
    
    // フォールバック
    float2 fallbackHalfSize;  // サイズ推定できない場合のデフォルト
};

// ----------------------------------------------------------------------------
// ユーティリティ：Euler XYZ 回転（度→ラジアン変換込み）
// ----------------------------------------------------------------------------
inline float3 RotateEulerX(float3 v, float angleDeg)
{
    float rad = radians(angleDeg);
    float c = cos(rad);
    float s = sin(rad);
    return float3(v.x, v.y * c - v.z * s, v.y * s + v.z * c);
}

inline float3 RotateEulerY(float3 v, float angleDeg)
{
    float rad = radians(angleDeg);
    float c = cos(rad);
    float s = sin(rad);
    return float3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
}

inline float3 RotateEulerZ(float3 v, float angleDeg)
{
    float rad = radians(angleDeg);
    float c = cos(rad);
    float s = sin(rad);
    return float3(v.x * c - v.y * s, v.x * s + v.y * c, v.z);
}

inline float3 RotateEulerXYZ(float3 v, float3 eulerDeg)
{
    v = RotateEulerX(v, eulerDeg.x);
    v = RotateEulerY(v, eulerDeg.y);
    v = RotateEulerZ(v, eulerDeg.z);
    return v;
}

// ----------------------------------------------------------------------------
// メイン変形関数
// 計算順序（仕様書 Section 4）:
//   1. Pivotへ移動
//   2. 歪み（shear/bend）
//   3. 擬似3D回転（Euler XYZ）
//   4. 奥行き縮小（perspective）
//   5. Pivotを戻す
// ----------------------------------------------------------------------------
inline float3 AdvancedFlip2D_Apply(float3 posOS, float2 halfSize, AdvancedFlip2DParams p)
{
    // 無効なら素通し
    if (p.enabled <= 0.5)
        return posOS;
    
    // halfSize が無効な場合はフォールバック
    halfSize = max(halfSize, p.fallbackHalfSize);
    halfSize = max(halfSize, float2(1e-4, 1e-4));
    
    // 1. Pivot へ移動
    float3 v = posOS - float3(p.pivotLocal, 0);
    
    // 正規化座標 (-1..1) に変換
    float2 n = v.xy / halfSize;
    
    // 2. せん断（shear）
    float2 n0 = n;
    n.x = n0.x + n0.y * p.shear.x;
    n.y = n0.y + n0.x * p.shear.y;
    
    // 3. 曲げ（bend）- signed quadratic で対称に曲がる
    n0 = n;
    n.x += p.bend.x * (n0.y * abs(n0.y));
    n.y += p.bend.y * (n0.x * abs(n0.x));
    
    // ローカル単位に戻す
    v.xy = n * halfSize;
    
    // 4. 擬似3D回転（Euler XYZ）
    float3 euler = float3(p.eulerDegX, p.eulerDegY, p.eulerDegZ);
    v = RotateEulerXYZ(v, euler);
    
    // 5. 奥行き縮小（perspective）- 回転後の z を使用
    if (p.perspective > 0.0)
    {
        float maxHalf = max(halfSize.x, halfSize.y);
        float zN = (v.z * p.depthScale) / maxHalf;
        float s = 1.0 / (1.0 + zN * p.perspSign * p.perspective);
        s = clamp(s, p.scaleClampMin, p.scaleClampMax);
        v.xy *= s;
    }
    
    // 6. Pivot を戻す（z は回転後の値を保持）
    v.x += p.pivotLocal.x;
    v.y += p.pivotLocal.y;
    
    // 2D レンダリングなので z は 0 に戻す（または保持するかは設計次第）
    v.z = 0;
    
    return v;
}

#endif // GAME_ADVANCEDFLIP2D_INCLUDED
