using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Scalar;
using Game.Scalar.Generated;
using VContainer;
using Game.Vars.Generated;
using UnityEngine;
using DG.Tweening;
using VNext = Game.Commands.VNext;
/*
namespace Game.Actions
{
    // ゲームの概要, 三つのゲージが各コマンドのアクションとなる。
    // 赤は攻撃、青は防御、緑はスキル。
    // 各ゲージは特定の範囲と値を持ち、その範囲内でアクションが発動される。


    /*
    # 戦闘システム
    アクションの種類
    物理攻撃: 一般的な攻撃
    シールド(防御): 特定のダメージをカットできる
 
    ゲームには三つの色、赤、青、緑がある。それぞれには固有のアクションとなっている。
    これらがゲージに表示され、左右を行き来するハンドルを停止させることで、
    その停止した位置の色の効果を発動する。
    Base倍率x1
    1. 赤: 基本的な物理攻撃xBase
    2. 青: 基本的なシールド(パークによって変わる)xBase
    3. 緑: 基本的なスキル(パークによって変わる)xBase
    4. グレー: 準備, 次のターンのシールドと物理攻撃のxBaseが1.25に変わる。  
    またこれらの色はかぶることがあり、かぶった場合そのかぶった色に変化する
    色はかぶりにくく、最初は赤青緑ともに15%の割合で、かぶらないようにできている。
    合成の場合Base倍率が0.7に変わる
    1. 紫(赤x青): 攻撃xBaseした後、シールドxBaseを行う+敵に弱体化x0.75
    2. 黄(赤x緑): スキルx1.5を打つ+次の単色赤の攻撃x1.25
    3. 水(青x緑): シールドx1.5する+微小回復ができる
    4. 白(赤x青x緑): すべての行動を行えるxBase
    またパークによってAdditional効果を入れることができる, 
    これにより、その色以上に、プラスの効果を打つことができる、
    パークはスロットシステムで基本存在している。
    またスキルは少し特殊であり、最初から選べる仕組みになっている。
    ただしゲーム内でスキルは変えることができる。
    */
/*
public interface IPlayerGameActionService
{
    event Action<GageColor> OnGageStopped;
    float CurrentSelectorPosition { get; }
    GageColor CurrentGageColor { get; }
    bool TryGetActionGageSnapshot(GageColor color, out ActionGageSnapshot snapshot);
}

public class ActionGage
{
    private GageColor _gageColor;
    private ScalarKey _actionRangeKey;
    private ScalarKey _actionValueKey;
    private float _currentGageActionPosition;
    private float _currentGageActionRange;
    private float _currentActionValue;

    // 
    public GageColor GageColor => _gageColor;

    public ScalarKey ActionRangeKey => _actionRangeKey;
    public ScalarKey ActionValueKey => _actionValueKey;

    public float CurrentGageActionPosition
    {
        get => _currentGageActionPosition;
        set => _currentGageActionPosition = value;
    }

    public float CurrentGageActionRange
    {
        get => _currentGageActionRange;
        set => _currentGageActionRange = value;
    }

    public float CurrentActionValue
    {
        get => _currentActionValue;
        set => _currentActionValue = value;
    }

    public ActionGage(GageColor gageColor, ScalarKey actionRangeKey, ScalarKey actionValueKey)
    {
        _gageColor = gageColor;
        _actionRangeKey = actionRangeKey;
        _actionValueKey = actionValueKey;
    }
}

public readonly struct ActionGageSnapshot
{
    public readonly GageColor Color;
    public readonly float Position;
    public readonly float Range;
    public readonly float Value;

    public ActionGageSnapshot(GageColor color, float position, float range, float value)
    {
        Color = color;
        Position = position;
        Range = range;
        Value = value;
    }
}

public enum GageColor
{
    Red,
    Blue,
    Green,
    Gray,
    Purple, // Red + Blue
    Yellow, // Red + Green
    Cyan,   // Blue + Green
    White   // Red + Blue + Green
}
public class PlayerGameActionService : IPlayerGameActionService
{
    // 三つのアクションgageの変数
    private ActionGage _redActionGage;
    private ActionGage _blueActionGage;
    private ActionGage _greenActionGage;

    private ActionGage _purpleActionGage; // 赤青
    private ActionGage _yellowActionGage; // 赤緑
    private ActionGage _cyanActionGage; // 青緑

    private ActionGage _whiteActionGage; // 赤青緑

    private IBlackboardService _blackboardService;
    private IPlayerGameActionSettings _settings;
    private VNext.ICommandRunner _commandRunner;
    private GageColor _currentGageColor = GageColor.Gray;
    private float _currentSelectorPosition;
    private float _sectorSpeed;

    private bool _isStopGage = false;

    public IBlackboardService SelfBlackboard => _blackboardService;
    public event Action<GageColor> OnGageStopped;
    public float CurrentSelectorPosition => _currentSelectorPosition;
    public GageColor CurrentGageColor => _currentGageColor;

    public PlayerGameActionService(
        IBlackboardService blackboard,
        IPlayerGameActionSettings settings,
        VNext.ICommandRunner commandRunner)
    {
        _blackboardService = blackboard;
        _settings = settings;
        _commandRunner = commandRunner;

        // 各アクションゲージの初期化
        _redActionGage = new ActionGage(
            GageColor.Red,
            ScalarKeys.GameLogic.PlayerProfile.BaseStatus.GageProfiles.Red.RedRange,
            ScalarKeys.GameLogic.PlayerProfile.BaseStatus.GageProfiles.Red.RedAttackDamage);

        _blueActionGage = new ActionGage(
            GageColor.Blue,
            ScalarKeys.GameLogic.PlayerProfile.BaseStatus.GageProfiles.Blue.BlueRange,
            ScalarKeys.GameLogic.PlayerProfile.BaseStatus.GageProfiles.Blue.BlueShieldValue);
        _greenActionGage = new ActionGage(
            GageColor.Green,
            ScalarKeys.GameLogic.PlayerProfile.BaseStatus.GageProfiles.Green.GreenRange,
            ScalarKeys.GameLogic.PlayerProfile.BaseStatus.GageProfiles.Green.GreenSkillValue);



    }

    public UniTask BuildGage(CancellationToken cancellationToken)
    {
        // 初期化処理、ActionGageの範囲と値を設定
        // ルール1 赤, 青, 緑は極力かぶらないようにランダムにPositionを設定する、またRangeの範囲もPosition暫定には影響する, Rangeは中心からの広がり具合を決める
        // 例: Rangeが0.3だった場合、Positionが0.5ならば、0.35-0.65の範囲がその色の範囲となる
        // ルール2 Positionは0-1の範囲で決められるが必ずRange内に収まるようにする, つまりrangeが0.3だったとした場合、Positionは0.15-0.85の範囲で決められる
        // ただし極力、赤、青、緑がかぶらないようにする

        // ルール3 かぶった場合、合成色になる、赤青=紫, 赤緑=黄, 青緑=水, 赤青緑=白
        // もしかぶる場所が一切ない場合は、グレーになる、これは特にActionGageの設定は必要ない
        // Sector決定時に、各色のActionGageのPositionとRangeを参照して決定するが、ヒットが一切ない場合、グレーになる
        if (cancellationToken.IsCancellationRequested)
            return UniTask.FromCanceled(cancellationToken);

        const float defaultRange = 0.15f;
        var scalarService = ResolveScalarService();

        ApplyActionGageValues(_redActionGage, scalarService, defaultRange);
        ApplyActionGageValues(_blueActionGage, scalarService, defaultRange);
        ApplyActionGageValues(_greenActionGage, scalarService, defaultRange);

        var redRange = _redActionGage.CurrentGageActionRange;
        var blueRange = _blueActionGage.CurrentGageActionRange;
        var greenRange = _greenActionGage.CurrentGageActionRange;

        var bestRed = RandomPosition(redRange);
        var bestBlue = RandomPosition(blueRange);
        var bestGreen = RandomPosition(greenRange);
        var bestCost = CalculateOverlapCost(bestRed, redRange, bestBlue, blueRange, bestGreen, greenRange);

        // Sample multiple layouts and pick the one with the least overlap.
        for (int i = 1; i < 24; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled(cancellationToken);

            var candidateRed = RandomPosition(redRange);
            var candidateBlue = RandomPosition(blueRange);
            var candidateGreen = RandomPosition(greenRange);
            var cost = CalculateOverlapCost(candidateRed, redRange, candidateBlue, blueRange, candidateGreen, greenRange);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestRed = candidateRed;
                bestBlue = candidateBlue;
                bestGreen = candidateGreen;
                if (bestCost <= 0f)
                    break;
            }
        }

        _redActionGage.CurrentGageActionPosition = bestRed;
        _blueActionGage.CurrentGageActionPosition = bestBlue;
        _greenActionGage.CurrentGageActionPosition = bestGreen;
        _currentGageColor = GageColor.Gray;

        return ExecuteSettingsCommandsAsync(_settings?.BuildGageCommands, cancellationToken, "BuildGage");
    }
    // セクターがただ動き始めるだけ
    public async UniTask StartGageSelectorMovementAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        await ExecuteSettingsCommandsAsync(_settings?.StartGageCommands, cancellationToken, "StartGage");
        if (cancellationToken.IsCancellationRequested)
            return;

        _isStopGage = false;
        float sign = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            // ストップ条件をここに追加可能
            if (_isStopGage)
            {
                break;
            }

            // ゲージセレクターの位置を更新
            float deltaValue = _sectorSpeed * Time.deltaTime;

            // easingを追加する DG.Tweening
            Ease easeType = Ease.Linear; // ここでイージングの種類を指定
            float finalDeltaValue = DOVirtual.EasedValue(0f, deltaValue, 1f, easeType);

            _currentSelectorPosition += finalDeltaValue * sign;

            // 位置が1を超えたら0に戻す（pinponさせる）
            if (_currentSelectorPosition > 1f)
            {
                sign = -1;
                _currentSelectorPosition = 1f;
            }
            else if (_currentSelectorPosition < 0f)
            {
                sign = 1;
                _currentSelectorPosition = 0f;
            }


            await UniTask.Yield(cancellationToken);
        }
    }

    // セクターを停止させる
    public void StopGageSelector()
    {
        if (_isStopGage)
            return;

        // セクターの速度を0にして停止させる
        _sectorSpeed = 0f;
        // 今の位置を保存する

        float stoppedPosition = Mathf.Clamp01(_currentSelectorPosition);
        _isStopGage = true;

        // 停止した位置に基づいてActionGageを決定するロジックをここに追加可能
        _currentGageColor = DetermineGageColor(stoppedPosition);
        var vars = CreateStopVarStore(_currentGageColor);
        ExecuteSettingsCommandsAsync(_settings?.StopGageSectorCommands, CancellationToken.None, "StopGageSector", vars).Forget();
        OnGageStopped?.Invoke(_currentGageColor);
    }

    UniTask ExecuteSettingsCommandsAsync(
        VNext.CommandListData commands,
        CancellationToken cancellationToken,
        string label,
        IVarStore vars = null)
    {
        if (cancellationToken.IsCancellationRequested)
            return UniTask.CompletedTask;

        if (commands == null || commands.Count == 0)
            return UniTask.CompletedTask;

        if (_commandRunner == null)
            return UniTask.CompletedTask;

        var scope = _blackboardService?.ScopeNode;
        if (scope == null || scope.Resolver == null)
            return UniTask.CompletedTask;

        return ExecuteSettingsCommandsCoreAsync(commands, scope, vars ?? NullVarStore.Instance, cancellationToken, label);
    }

    async UniTask ExecuteSettingsCommandsCoreAsync(
        VNext.CommandListData commands,
        IScopeNode scope,
        IVarStore vars,
        CancellationToken cancellationToken,
        string label)
    {
        var options = VNext.CommandRunOptions.Default;
        var ctx = new VNext.CommandContext(scope, vars ?? NullVarStore.Instance, _commandRunner, scope, options);

        try
        {
            var result = await _commandRunner.ExecuteListAsync(commands, ctx, cancellationToken, options);
            if (result.Status == VNext.CommandRunStatus.Error)
                Debug.LogError($"[PlayerGameActionService] {label} commands failed: {result.Message}");
        }
        catch (OperationCanceledException)
        {
            // キャンセルは正常終了
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    IBaseScalarService ResolveScalarService()
    {
        if (_blackboardService == null || _blackboardService.ScopeNode?.Resolver == null)
            return null;

        _blackboardService.ScopeNode.Resolver.TryResolve<IBaseScalarService>(out var scalarService);
        return scalarService;
    }

    public bool TryGetActionGageSnapshot(GageColor color, out ActionGageSnapshot snapshot)
    {
        if (!TryGetActionGage(color, out var gage))
        {
            snapshot = default;
            return false;
        }

        snapshot = new ActionGageSnapshot(
            gage.GageColor,
            gage.CurrentGageActionPosition,
            gage.CurrentGageActionRange,
            gage.CurrentActionValue);
        return true;
    }

    bool TryGetActionGage(GageColor color, out ActionGage gage)
    {
        switch (color)
        {
            case GageColor.Red:
                gage = _redActionGage;
                return gage != null;
            case GageColor.Blue:
                gage = _blueActionGage;
                return gage != null;
            case GageColor.Green:
                gage = _greenActionGage;
                return gage != null;
        }

        gage = null;
        return false;
    }

    static void ApplyActionGageValues(ActionGage gage, IBaseScalarService scalarService, float defaultRange)
    {
        if (gage == null)
            return;

        var range = scalarService != null ? scalarService.GlobalGet(gage.ActionRangeKey) : defaultRange;
        if (range <= 0f)
            range = defaultRange;

        gage.CurrentGageActionRange = Mathf.Clamp01(range);
        gage.CurrentActionValue = scalarService != null ? scalarService.GlobalGet(gage.ActionValueKey) : 0f;
    }

    static float RandomPosition(float range)
    {
        var half = range * 0.5f;
        var min = half;
        var max = 1f - half;
        if (max <= min)
            return 0.5f;

        return UnityEngine.Random.Range(min, max);
    }

    static float CalculateOverlapCost(
        float redPos,
        float redRange,
        float bluePos,
        float blueRange,
        float greenPos,
        float greenRange)
    {
        var rb = CalculateOverlap(redPos, redRange, bluePos, blueRange);
        var rg = CalculateOverlap(redPos, redRange, greenPos, greenRange);
        var bg = CalculateOverlap(bluePos, blueRange, greenPos, greenRange);
        return rb + rg + bg;
    }

    static float CalculateOverlap(float posA, float rangeA, float posB, float rangeB)
    {
        var halfA = rangeA * 0.5f;
        var halfB = rangeB * 0.5f;
        var minA = posA - halfA;
        var maxA = posA + halfA;
        var minB = posB - halfB;
        var maxB = posB + halfB;
        var overlap = Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB);
        return Mathf.Max(0f, overlap);
    }

    static bool IsPositionInGage(ActionGage gage, float position)
    {
        if (gage == null)
            return false;

        var half = gage.CurrentGageActionRange * 0.5f;
        var min = gage.CurrentGageActionPosition - half;
        var max = gage.CurrentGageActionPosition + half;
        return position >= min && position <= max;
    }

    GageColor DetermineGageColor(float position)
    {
        position = Mathf.Clamp01(position);

        var isRed = IsPositionInGage(_redActionGage, position);
        var isBlue = IsPositionInGage(_blueActionGage, position);
        var isGreen = IsPositionInGage(_greenActionGage, position);

        if (isRed && isBlue && isGreen)
            return GageColor.White;
        if (isRed && isBlue)
            return GageColor.Purple;
        if (isRed && isGreen)
            return GageColor.Yellow;
        if (isBlue && isGreen)
            return GageColor.Cyan;
        if (isRed)
            return GageColor.Red;
        if (isBlue)
            return GageColor.Blue;
        if (isGreen)
            return GageColor.Green;

        return GageColor.Gray;
    }

    static VarStore CreateStopVarStore(GageColor color)
    {
        var vars = new VarStore(1);
        vars.TrySetVariant(
            VarIds.GameLogic.GameProfiles.PlayerGameAction.currentGageColor,
            DynamicVariant.FromInt((int)color));
        return vars;
    }
}
}
*/