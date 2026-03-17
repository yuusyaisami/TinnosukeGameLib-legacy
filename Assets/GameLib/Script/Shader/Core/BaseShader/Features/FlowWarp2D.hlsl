#ifndef GAME_FLOW_WARP_2D_INCLUDED
#define GAME_FLOW_WARP_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// FlowWarp2D.hlsl - UV 歪み・フローエフェクト (v2.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v2.0 準拠
//
// 機能:
//   - TextureSlot から GB チャンネルをベクトルとして読み取り
//   - UV に歪みを適用
//   - Speed で時間アニメーション
//
// v2.0 変更点:
//   - binding 引数を削除
//   - TextureSlotRef から直接 external texture を解決
//   - time は内部で _Time.y を使用
//   - 旧互換ラッパーを削除
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

// ---------------------------------------------------------------------------
// FlowWarp パラメータ構造体
// ---------------------------------------------------------------------------
struct FlowWarp2DParams
{
    float          enabled;
    TextureSlotRef source;
    float          strength;       // 後方互換用
    half2          strengthXY;     // X/Y 個別強度
    float          speed;
};

// ---------------------------------------------------------------------------
// CBUFFER から FlowWarp2DParams を生成
// ---------------------------------------------------------------------------
inline FlowWarp2DParams MakeFlowWarp2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float2 strengthXY,
    float speed)
{
    FlowWarp2DParams p;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.strength = max(abs(strengthXY.x), abs(strengthXY.y));  // 後方互換
    p.strengthXY = strengthXY;
    p.speed = speed;
    return p;
}

// デフォルト値で初期化（無効状態）
inline FlowWarp2DParams MakeDefaultFlowWarp2DParams()
{
    FlowWarp2DParams p;
    p.enabled = 0;
    p.source = MakeDefaultTextureSlotRef();
    p.source.channelMask = CHANNEL_RG;  // FlowWarp は RG をベクトルとして使用
    p.strength = 0.05;
    p.strengthXY = half2(0.05, 0.05);
    p.speed = 1.0;
    return p;
}

// ---------------------------------------------------------------------------
// FlowWarp UV 変形（テクスチャサンプル前に適用）
// ★v2.0: binding 引数なし、time は _Time.y を使用
// ★修正: UV ドリフトを防止するため、フロー値を中心化
// ★修正: strengthXY で X/Y 個別の強度を適用
//
// 引数の役割:
//   targetUV   : 歪ませる対象の UV（最終的にサンプリングに使われる）
//   uvLocal    : スプライトローカル UV (0..1) - ComputeSlotUV で UVSpace 計算に使用
//   uvMain     : 元テクスチャ UV - ComputeSlotUV で UVSpace=TextureRaw 時に使用
//   screenUV   : スクリーン座標 UV - ComputeSlotUV で UVSpace=Screen 時に使用
//
// 戻り値: 歪み適用後の targetUV
// ---------------------------------------------------------------------------
inline float2 FlowWarp2D_WarpUV(
    float2 targetUV,
    float2 uvLocal,
    float2 uvMain,
    float2 screenUV,
    FlowWarp2DParams p)
{
    if (p.enabled < 0.5)
        return targetUV;
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return targetUV;
    
    // ソース UV を計算（UVSpace に応じて uvLocal/uvMain/screenUV から選択）
    // v2.1: ComputeSlotUV に slotType を追加したため、最後の引数に p.source.slotType を渡す
    float2 sourceUV = ComputeSlotUV(uvLocal, uvMain, screenUV, p.source.uvSpace, p.source.tilingOffset, p.source.slotType);
    
    // ★修正: 時間オフセットを frac で正規化してループを滑らかに
    float time = _Time.y;
    float timePhase = frac(time * p.speed * 0.1);
    
    // 2つのフェーズでサンプルしてクロスフェード（シームレスループ）
    float2 uv1 = sourceUV + float2(timePhase, timePhase * 0.7);
    float2 uv2 = sourceUV + float2(frac(timePhase + 0.5), frac(timePhase * 0.7 + 0.5));
    
    // フローベクトルを取得
    half4 raw1 = SampleSlotRaw(uv1, p.source.slotType);
    half4 raw2 = SampleSlotRaw(uv2, p.source.slotType);
    
    half2 flow1 = ExtractVector2(raw1, p.source.channelMask, p.source.remap);
    half2 flow2 = ExtractVector2(raw2, p.source.channelMask, p.source.remap);
    
    // クロスフェード（sin で滑らかに遷移）
    float blend = sin(timePhase * 3.14159) * sin(timePhase * 3.14159);
    half2 flow = lerp(flow1, flow2, blend);
    
    // ★重要: フローベクトルは既に ExtractVector2 で [-1,1] に変換済み
    // ★修正: strengthXY で X/Y 個別に強度を適用
    float2 offset = flow * p.strengthXY;
    
    // targetUV にオフセットを適用
    return targetUV + offset;
}

// 後方互換: 4引数版（uvMain を省略した場合は uvLocal を使用）
inline float2 FlowWarp2D_WarpUV(
    float2 targetUV,
    float2 uvLocal,
    float2 screenUV,
    FlowWarp2DParams p)
{
    return FlowWarp2D_WarpUV(targetUV, uvLocal, uvLocal, screenUV, p);
}

// ---------------------------------------------------------------------------
// Surface2D ベースの FlowWarp
// ---------------------------------------------------------------------------
inline float2 FlowWarp2D_WarpUVFromSurface(Surface2D s, FlowWarp2DParams p)
{
    // targetUV = uvMain, uvLocal, uvMain, screenUV の5引数版を使用
    return FlowWarp2D_WarpUV(s.uvMain, s.uvLocal, s.uvMain, s.screenUV, p);
}

// ---------------------------------------------------------------------------
// Surface2D に FlowWarp を適用（uvMain を書き換え）
// 注意: これはサンプル前に呼ぶ必要がある
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyFlowWarp(Surface2D s, FlowWarp2DParams p)
{
    if (p.enabled < 0.5)
        return s;
    
    s.uvMain = FlowWarp2D_WarpUVFromSurface(s, p);
    return s;
}

#endif // GAME_FLOW_WARP_2D_INCLUDED
