#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unityroom.Client;

namespace Game.UnityRoom
{
    public interface IUnityRoomService
    {
        UniTask SendScoreAsync(float score, CancellationToken cancellationToken = default);
    }

    public sealed class UnityRoomSettings
    {
        public string HmacKey { get; set; } = string.Empty;
        public int ScoreboardId { get; set; } = 1;
    }

    public sealed class UnityRoomService : IUnityRoomService, IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
    {
        readonly UnityRoomSettings _settings;

        UnityroomClient? _client;
        bool _invalidConfigurationLogged;

        public UnityRoomService(UnityRoomSettings settings)
        {
            _settings = settings ?? new UnityRoomSettings();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            EnsureClient();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            ReleaseClient();
        }

        public void Dispose()
        {
            ReleaseClient();
        }

        public async UniTask SendScoreAsync(float score, CancellationToken cancellationToken = default)
        {
            if (!IsFinite(score))
            {
                Debug.LogWarning($"[UnityRoomService] Ignored non-finite score: {score}");
                return;
            }

            if (!EnsureClient())
            {
                return;
            }

            var roundedScore = Mathf.RoundToInt(score);
            try
            {
                await _client!.Scoreboards.SendAsync(new SendScoreRequest
                {
                    ScoreboardId = _settings.ScoreboardId,
                    Score = roundedScore,
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityRoomService] SendScore failed | ScoreboardId={_settings.ScoreboardId} Score={roundedScore} Error={ex.Message}");
            }
        }

        bool EnsureClient()
        {
            if (_client != null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_settings.HmacKey) || _settings.ScoreboardId <= 0)
            {
                if (!_invalidConfigurationLogged)
                {
                    _invalidConfigurationLogged = true;
                    Debug.LogWarning($"[UnityRoomService] Invalid configuration | ScoreboardId={_settings.ScoreboardId} HmacKeyEmpty={string.IsNullOrWhiteSpace(_settings.HmacKey)}");
                }

                return false;
            }

            try
            {
                _client = new UnityroomClient
                {
                    HmacKey = _settings.HmacKey,
                };
                return true;
                            }
            catch (Exception)
            {
                //Debug.LogWarning($"[UnityRoomService] Failed to initialize client | ScoreboardId={_settings.ScoreboardId} Error={ex.Message}");
                return false;
            }
        }

        void ReleaseClient()
        {
            _client?.Dispose();
            _client = null;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
