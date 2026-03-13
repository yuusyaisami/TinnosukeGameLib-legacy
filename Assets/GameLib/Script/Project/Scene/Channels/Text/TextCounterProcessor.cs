#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Game.Channel
{
    /// <summary>
    /// 文字列中の数値トークンを検出し、カウントアニメーション用の中間文字列を生成する。
    /// TextChannelPlayer/Backend はこのクラスの公開 API のみを使用する。
    /// </summary>
    public sealed class TextCounterProcessor
    {
        // 数値トークンのパターン: 符号付き整数または小数
        static readonly Regex NumberPattern = new(@"-?\d+(?:\.\d+)?", RegexOptions.Compiled);

        readonly List<Token> _prevTokens = new(8);
        readonly List<Token> _nextTokens = new(8);
        readonly List<NumericPair> _pairs = new(8);
        readonly StringBuilder _builder = new(128);

        string _prevText = string.Empty;
        string _nextText = string.Empty;
        SetTextSettings _settings;

        bool _isAnimating;
        float _elapsed;
        float _duration;

        /// <summary>
        /// 現在アニメーション中かどうか。
        /// </summary>
        public bool IsAnimating => _isAnimating;

        /// <summary>
        /// アニメーション完了後の最終テキスト。
        /// </summary>
        public string FinalText => _nextText;

        /// <summary>
        /// 新しいテキストでカウントを開始する。
        /// </summary>
        /// <param name="prevText">現在表示中のテキスト</param>
        /// <param name="nextText">新しいテキスト</param>
        /// <param name="settings">設定</param>
        /// <returns>true: カウントアニメーションを開始, false: 即時反映</returns>
        public bool Begin(string prevText, string nextText, in SetTextSettings settings)
        {
            _prevText = prevText ?? string.Empty;
            _nextText = nextText ?? string.Empty;
            _settings = settings;
            _elapsed = 0f;
            _duration = settings.CounterDurationSeconds;

            if (!settings.UseCounter || _duration <= 0f)
            {
                _isAnimating = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[TextCounter] Begin skipped: useCounter={settings.UseCounter} duration={_duration} prev='{_prevText}' next='{_nextText}'");
#endif
                return false;
            }

            Tokenize(_prevText, _prevTokens);
            Tokenize(_nextText, _nextTokens);

            if (!TryBuildPairs())
            {
                _isAnimating = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[TextCounter] Begin failed: no numeric pairs prevTokens={_prevTokens.Count} nextTokens={_nextTokens.Count} prev='{_prevText}' next='{_nextText}'");
#endif
                return false;
            }

            _isAnimating = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[TextCounter] Begin ok: pairs={_pairs.Count} duration={_duration} prev='{_prevText}' next='{_nextText}'");
#endif
            return true;
        }

        /// <summary>
        /// アニメーションを進行させ、現在のテキストを取得する。
        /// </summary>
        /// <param name="deltaTime">経過時間</param>
        /// <param name="currentText">現在表示すべきテキスト</param>
        /// <returns>true: アニメーション継続中, false: 完了</returns>
        public bool Update(float deltaTime, out string currentText)
        {
            if (!_isAnimating)
            {
                currentText = _nextText;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[TextCounter] Update skipped: not animating next='{_nextText}'");
#endif
                return false;
            }

            _elapsed += deltaTime;
            float t = _duration > 0f ? Math.Min(_elapsed / _duration, 1f) : 1f;
            float eased = ApplyEase(t, _settings.CounterEase);

            currentText = BuildInterpolatedText(eased);

            if (t >= 1f)
            {
                _isAnimating = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[TextCounter] Update finished: final='{_nextText}' elapsed={_elapsed}");
#endif
                return false;
            }

            return true;
        }

        /// <summary>
        /// アニメーションを即座に完了させる。
        /// </summary>
        public void Complete()
        {
            _isAnimating = false;
        }

        /// <summary>
        /// 数値を設定に従ってフォーマットする。
        /// </summary>
        public string FormatNumber(double value, in SetTextSettings settings)
        {
            value = ApplyRounding(value, settings.RoundingMode, settings.DecimalDigits);

            var format = BuildNumberFormat(settings.FixedIntegerDigits, settings.DecimalDigits, settings.UseThousandsSeparator);
            var result = value.ToString(format, CultureInfo.InvariantCulture);

            if (settings.ShowPlusSign && value > 0)
                result = "+" + result;

            return result;
        }

        #region Tokenization

        void Tokenize(string text, List<Token> tokens)
        {
            tokens.Clear();
            if (string.IsNullOrEmpty(text))
                return;

            var matches = NumberPattern.Matches(text);
            int lastEnd = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastEnd)
                {
                    tokens.Add(new Token
                    {
                        IsNumber = false,
                        Text = text.Substring(lastEnd, match.Index - lastEnd),
                        NumericValue = 0,
                    });
                }

                if (double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numVal))
                {
                    tokens.Add(new Token
                    {
                        IsNumber = true,
                        Text = match.Value,
                        NumericValue = numVal,
                    });
                }
                else
                {
                    tokens.Add(new Token
                    {
                        IsNumber = false,
                        Text = match.Value,
                        NumericValue = 0,
                    });
                }

                lastEnd = match.Index + match.Length;
            }

            if (lastEnd < text.Length)
            {
                tokens.Add(new Token
                {
                    IsNumber = false,
                    Text = text.Substring(lastEnd),
                    NumericValue = 0,
                });
            }
        }

        bool TryBuildPairs()
        {
            _pairs.Clear();

            // 数値トークンのみ抽出
            var prevNums = new List<(int index, Token token)>();
            var nextNums = new List<(int index, Token token)>();

            for (int i = 0; i < _prevTokens.Count; i++)
            {
                if (_prevTokens[i].IsNumber)
                    prevNums.Add((i, _prevTokens[i]));
            }

            for (int i = 0; i < _nextTokens.Count; i++)
            {
                if (_nextTokens[i].IsNumber)
                    nextNums.Add((i, _nextTokens[i]));
            }

            // 順序一致でペアを作成
            int pairCount = Math.Min(prevNums.Count, nextNums.Count);
            if (pairCount == 0 && nextNums.Count == 0)
                return false;

            for (int i = 0; i < pairCount; i++)
            {
                _pairs.Add(new NumericPair
                {
                    PrevIndex = prevNums[i].index,
                    NextIndex = nextNums[i].index,
                    FromValue = prevNums[i].token.NumericValue,
                    ToValue = nextNums[i].token.NumericValue,
                });
            }

            // 新規追加された数値は from = to とする
            for (int i = pairCount; i < nextNums.Count; i++)
            {
                _pairs.Add(new NumericPair
                {
                    PrevIndex = -1,
                    NextIndex = nextNums[i].index,
                    FromValue = nextNums[i].token.NumericValue,
                    ToValue = nextNums[i].token.NumericValue,
                });
            }

            return _pairs.Count > 0;
        }

        #endregion

        #region Interpolation

        string BuildInterpolatedText(float t)
        {
            _builder.Clear();

            // ペアの NextIndex -> 補間値のマップを作成
            var interpolatedMap = new Dictionary<int, double>(_pairs.Count);
            foreach (var pair in _pairs)
            {
                double interpolated = pair.FromValue + (pair.ToValue - pair.FromValue) * t;
                interpolatedMap[pair.NextIndex] = interpolated;
            }

            // _nextTokens を走査してテキストを構築
            for (int i = 0; i < _nextTokens.Count; i++)
            {
                var token = _nextTokens[i];
                if (token.IsNumber && interpolatedMap.TryGetValue(i, out var val))
                {
                    _builder.Append(FormatNumber(val, _settings));
                }
                else
                {
                    _builder.Append(token.Text);
                }
            }

            return _builder.ToString();
        }

        #endregion

        #region Formatting Helpers

        static double ApplyRounding(double value, NumberRoundingMode mode, int decimalDigits)
        {
            double multiplier = Math.Pow(10, decimalDigits);
            double scaled = value * multiplier;

            scaled = mode switch
            {
                NumberRoundingMode.Floor => Math.Floor(scaled),
                NumberRoundingMode.Ceil => Math.Ceiling(scaled),
                _ => Math.Round(scaled),
            };

            return scaled / multiplier;
        }

        static string BuildNumberFormat(int fixedIntegerDigits, int decimalDigits, bool useThousandsSeparator)
        {
            var sb = new StringBuilder(16);

            if (useThousandsSeparator)
            {
                if (fixedIntegerDigits > 0)
                {
                    sb.Append(new string('0', Math.Max(1, fixedIntegerDigits - 3)));
                    sb.Append(",");
                    sb.Append("000");
                }
                else
                {
                    sb.Append("#,##0");
                }
            }
            else
            {
                if (fixedIntegerDigits > 0)
                    sb.Append(new string('0', fixedIntegerDigits));
                else
                    sb.Append('0');
            }

            if (decimalDigits > 0)
            {
                sb.Append('.');
                sb.Append(new string('0', decimalDigits));
            }

            return sb.ToString();
        }

        static float ApplyEase(float t, DG.Tweening.Ease ease)
        {
            // DOTween の EaseManager を利用
            return DG.Tweening.Core.Easing.EaseManager.Evaluate(ease, null, t, 1f, 0f, 0f);
        }

        #endregion

        #region Internal Types

        struct Token
        {
            public bool IsNumber;
            public string Text;
            public double NumericValue;
        }

        struct NumericPair
        {
            public int PrevIndex;
            public int NextIndex;
            public double FromValue;
            public double ToValue;
        }

        #endregion
    }
}
