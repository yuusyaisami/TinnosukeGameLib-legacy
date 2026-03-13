#ifndef GAME_FLASH2D_INCLUDED
#define GAME_FLASH2D_INCLUDED
struct Flash2DParams
{
    float enabled;
    float3 color;
    float amount;
    float mode;
    float blinkEnabled;
    float blinkAmplitude;
    float blinkSpeed;
    float blinkPhaseOffset;
};

inline Flash2DParams MakeFlash2DParams()
{
    Flash2DParams p;
    p.enabled = _FlashEnabled;
    p.color   = _FlashColor.rgb;
    p.amount  = _FlashAmount;
    p.mode    = _FlashMode;
    p.blinkEnabled = _FlashBlinkEnabled;
    p.blinkAmplitude = _FlashBlinkAmplitude;
    p.blinkSpeed = _FlashBlinkSpeed;
    p.blinkPhaseOffset = _FlashBlinkPhaseOffset;
    return p;
}
// Flash 処理を 1 箇所に集約。
// enabled  : _FlashEnabled
// flashRGB : _FlashColor.rgb
// amount   : _FlashAmount
// mode     : _FlashMode (0:Lerp 1:Add)
// blink    : amount + (0..1)*_FlashBlinkAmplitude
inline float3 ApplyFlash2D(
    float3 baseColor,
    Flash2DParams p)
{
    if (p.enabled <= 0.5f)
        return baseColor;

    float flashAmount = p.amount;
    if (p.blinkEnabled > 0.5f)
    {
        float blinkAmplitude = max(0.0f, p.blinkAmplitude);
        if (blinkAmplitude > 1e-5f)
        {
            float t = _Time.y * p.blinkSpeed + p.blinkPhaseOffset;
            float blink = (0.5f + 0.5f * sin(t)) * blinkAmplitude;
            flashAmount += blink;
        }
    }
    // BlinkEnabled=false の場合は常に base amount のみを使用する。
    flashAmount = saturate(flashAmount);
    if (flashAmount <= 1e-5f)
        return baseColor;

    if (p.mode < 0.5f)
    {
        // Lerp
        return lerp(baseColor, p.color, flashAmount);
    }
    else
    {
        // Add
        return baseColor + p.color * flashAmount;
    }
}


inline Surface2D Surface2D_ApplyFlash(Surface2D s, Flash2DParams p)
{
    s.color = ApplyFlash2D(s.color, p);
    return s;
}

#endif // GAME_FLASH2D_INCLUDED
