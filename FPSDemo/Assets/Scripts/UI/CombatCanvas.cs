using DG.Tweening;
using DG.Tweening.Core;
using Combat;
using PlayerData;
using TMPro;
using UnityEngine;

/// <summary>
/// 战斗信息面板
/// 只负责显示波次 时间 击杀和金币 不处理结算逻辑
/// </summary>
[UICanvas(UILoadType.AssetBundle, UILayer.HUD)]
[DisallowMultipleComponent]
public class CombatCanvas : BaseCanvas
{
    [Header("文本")]
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text killText;
    [SerializeField] private TMP_Text goldText;

    [Header("动画")]
    [SerializeField] private float numberTweenDuration = 0.28f;
    [SerializeField] private Ease numberEase = Ease.OutQuad;
    [SerializeField] private float punchScale = 0.12f;
    [SerializeField] private float punchDuration = 0.16f;

    private Tweener _waveTween;
    private Tweener _killTween;
    private Tweener _goldTween;
    private Tweener _wavePunchTween;
    private Tweener _killPunchTween;
    private Tweener _goldPunchTween;
    private float _battleTime;
    private float _displayWave;
    private float _displayKill;
    private float _displayGold;
    private int _targetWave = 1;
    private int _targetKill;
    private int _targetGold;

    public override bool NeedRaycaster => false;

    public override void Awake()
    {
        base.Awake();
        CacheReferences();
        ResetDisplay();
    }

    private void OnEnable()
    {
        CacheReferences();
        AddListeners();
        SyncInitialWave();
        SyncInitialGold();
        SyncInitialTime();
        RefreshAllText();
    }

    private void OnDisable()
    {
        RemoveListeners();
        KillTweens();
    }

    private void AddListeners()
    {
        EventCenter.Instance.AddEventListener<EnemyWaveEventData>(GameEvent.EnemyWaveStarted, OnEnemyWaveStarted);
        EventCenter.Instance.AddEventListener<EnemyWaveEventData>(GameEvent.EnemyWaveProgressChanged, OnEnemyWaveProgressChanged);
        EventCenter.Instance.AddEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
        EventCenter.Instance.AddEventListener<PlayerBattleGoldChangedEventData>(GameEvent.PlayerBattleGoldChanged, OnPlayerBattleGoldChanged);
        EventCenter.Instance.AddEventListener<CombatTimeChangedEventData>(GameEvent.CombatTimeChanged, OnCombatTimeChanged);
    }

    private void RemoveListeners()
    {
        EventCenter.Instance.RemoveEventListener<EnemyWaveEventData>(GameEvent.EnemyWaveStarted, OnEnemyWaveStarted);
        EventCenter.Instance.RemoveEventListener<EnemyWaveEventData>(GameEvent.EnemyWaveProgressChanged, OnEnemyWaveProgressChanged);
        EventCenter.Instance.RemoveEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
        EventCenter.Instance.RemoveEventListener<PlayerBattleGoldChangedEventData>(GameEvent.PlayerBattleGoldChanged, OnPlayerBattleGoldChanged);
        EventCenter.Instance.RemoveEventListener<CombatTimeChangedEventData>(GameEvent.CombatTimeChanged, OnCombatTimeChanged);
    }

    private void OnEnemyWaveStarted(EnemyWaveEventData eventData)
    {
        SetWave(eventData.waveIndex, true);
    }

    private void OnEnemyWaveProgressChanged(EnemyWaveEventData eventData)
    {
        SetWave(eventData.waveIndex, false);
    }

    private void OnEnemyDied(EnemyDiedEventData eventData)
    {
        CombatRunRecorder recorder = CombatRunRecorder.Active;
        if (recorder != null && !CombatOwnership.IsPlayerOwnedDamage(eventData.damageInfo, recorder.Player))
        {
            return;
        }

        SetKill(_targetKill + 1, true);
    }

    private void OnPlayerBattleGoldChanged(PlayerBattleGoldChangedEventData eventData)
    {
        SetGold(eventData.battleGold, true);
    }

    private void OnCombatTimeChanged(CombatTimeChangedEventData eventData)
    {
        _battleTime = eventData.elapsedSeconds;
        RefreshTimerText();
    }

    private void SetWave(int value, bool playPunch)
    {
        _targetWave = Mathf.Max(1, value);
        TweenNumber(ref _waveTween, () => _displayWave, SetDisplayWave, _targetWave, waveText, ref _wavePunchTween, playPunch);
    }

    private void SetKill(int value, bool playPunch)
    {
        _targetKill = Mathf.Max(0, value);
        TweenNumber(ref _killTween, () => _displayKill, SetDisplayKill, _targetKill, killText, ref _killPunchTween, playPunch);
    }

    private void SetGold(int value, bool playPunch)
    {
        _targetGold = Mathf.Max(0, value);
        TweenNumber(ref _goldTween, () => _displayGold, SetDisplayGold, _targetGold, goldText, ref _goldPunchTween, playPunch);
    }

    private void TweenNumber(
        ref Tweener tweener,
        DOGetter<float> getter,
        DOSetter<float> setter,
        float target,
        TMP_Text text,
        ref Tweener punchTween,
        bool playPunch)
    {
        tweener?.Kill();
        tweener = DOTween
            .To(getter, setter, target, numberTweenDuration)
            .SetEase(numberEase)
            .SetUpdate(false);

        if (playPunch)
        {
            PlayPunch(text, ref punchTween);
        }
    }

    private void SetDisplayWave(float value)
    {
        _displayWave = Mathf.Max(1f, value);
        if (waveText != null)
        {
            waveText.text = Mathf.RoundToInt(_displayWave).ToString();
        }
    }

    private void SetDisplayKill(float value)
    {
        _displayKill = Mathf.Max(0f, value);
        if (killText != null)
        {
            killText.text = Mathf.RoundToInt(_displayKill).ToString();
        }
    }

    private void SetDisplayGold(float value)
    {
        _displayGold = Mathf.Max(0f, value);
        if (goldText != null)
        {
            goldText.text = Mathf.RoundToInt(_displayGold).ToString();
        }
    }

    private void PlayPunch(TMP_Text targetText, ref Tweener punchTween)
    {
        if (targetText == null)
        {
            return;
        }

        Transform target = targetText.transform;
        punchTween?.Kill();
        target.localScale = Vector3.one;
        punchTween = target
            .DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.6f)
            .SetUpdate(false);
    }

    private void SyncInitialGold()
    {
        PlayerInventory inventory = FindObjectOfType<PlayerInventory>();
        if (inventory == null)
        {
            SetGold(0, false);
            return;
        }

        SetGold(inventory.BattleGold, false);
    }

    private void SyncInitialWave()
    {
        Enemy.EnemySpawnManager spawnManager = FindObjectOfType<Enemy.EnemySpawnManager>();
        if (spawnManager == null)
        {
            SetWave(1, false);
            return;
        }

        SetWave(spawnManager.CurrentWaveIndex, false);
    }

    private void SyncInitialTime()
    {
        _battleTime = CombatRunRecorder.Active != null
            ? CombatRunRecorder.Active.SurvivalSeconds
            : 0f;
    }

    private void ResetDisplay()
    {
        _battleTime = 0f;
        _targetWave = 1;
        _targetKill = 0;
        _targetGold = 0;
        _displayWave = _targetWave;
        _displayKill = _targetKill;
        _displayGold = _targetGold;
        RefreshAllText();
    }

    private void RefreshAllText()
    {
        SetDisplayWave(_displayWave);
        SetDisplayKill(_displayKill);
        SetDisplayGold(_displayGold);
        RefreshTimerText();
    }

    private void RefreshTimerText()
    {
        if (timerText != null)
        {
            timerText.text = CombatTimeFormatter.Format(_battleTime);
        }
    }

    private void CacheReferences()
    {
        waveText ??= FindValueText("wave");
        timerText ??= FindTextByName("timer");
        killText ??= FindValueText("Kill");
        goldText ??= FindValueText("gold");
    }

    private TMP_Text FindValueText(string containerName)
    {
        Transform container = FindChildByName(containerName);
        if (container == null)
        {
            return null;
        }

        // 数字文本允许叫 Num 或 num 预制体改名后不用同步改脚本
        TMP_Text numText = FindTextByName(container, "num");
        if (numText != null)
        {
            return numText;
        }

        return container.GetComponentInChildren<TMP_Text>(true);
    }

    private TMP_Text FindTextByName(string targetName)
    {
        return FindTextByName(transform, targetName);
    }

    private TMP_Text FindTextByName(Transform root, string targetName)
    {
        Transform target = FindChildByName(root, targetName);
        if (target == null)
        {
            return null;
        }

        return target.TryGetComponent(out TMP_Text text)
            ? text
            : target.GetComponentInChildren<TMP_Text>(true);
    }

    private Transform FindChildByName(string targetName)
    {
        return FindChildByName(transform, targetName);
    }

    private Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        string normalizedTarget = NormalizeName(targetName);
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && NormalizeName(child.name) == normalizedTarget)
            {
                return child;
            }
        }

        return null;
    }

    private string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private void KillTweens()
    {
        _waveTween?.Kill();
        _waveTween = null;
        _killTween?.Kill();
        _killTween = null;
        _goldTween?.Kill();
        _goldTween = null;
        _wavePunchTween?.Kill();
        _wavePunchTween = null;
        _killPunchTween?.Kill();
        _killPunchTween = null;
        _goldPunchTween?.Kill();
        _goldPunchTween = null;
    }
}
