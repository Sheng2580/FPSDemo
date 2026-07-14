using System;
using System.Collections.Generic;
using DG.Tweening;
using PlayerData;
using UnityEngine;
using UnityEngine.UI;
using Weapon.Data;

public sealed class HallUpgradePresenter
{
    private const int PistolWeaponId = 1;
    private const int RifleWeaponId = 2;
    private const int ShotgunWeaponId = 3;

    private static readonly string[] WeaponStatLabels =
    {
        "伤害",
        "射速",
        "弹夹",
        "备弹",
        "换弹",
        "后坐",
        "散射"
    };

    private static readonly string[] PlayerStatLabels =
    {
        "最大生命",
        "移动速度",
        "跳跃高度",
        "技能冷却缩减",
        "充能效率"
    };

    private readonly GameObject _playerPanel;
    private readonly GameObject _weaponPanel;
    private readonly Color _selectedColor;
    private readonly Color _normalColor;
    private readonly float _duration;
    private readonly List<PlayerStatRow> _playerStatRows = new List<PlayerStatRow>();
    private readonly List<WeaponListItem> _weaponItems = new List<WeaponListItem>();
    private readonly List<WeaponStatRow> _weaponStatRows = new List<WeaponStatRow>();

    private Button _playerUpgradeButton;
    private Text _playerUpgradeLabel;
    private Text _playerUpgradeCost;
    private Text _weaponTitle;
    private Text _weaponLevel;
    private Button _weaponUpgradeButton;
    private Text _weaponUpgradeLabel;
    private Text _weaponUpgradeCost;
    private RectTransform _weaponDetailCard;
    private int _selectedWeaponId = PistolWeaponId;

    public HallUpgradePresenter(
        GameObject playerPanel,
        GameObject weaponPanel,
        Color selectedColor,
        Color normalColor,
        float duration)
    {
        _playerPanel = playerPanel;
        _weaponPanel = weaponPanel;
        _selectedColor = selectedColor;
        _normalColor = normalColor;
        _duration = Mathf.Max(0.05f, duration);

        CachePlayerPanel();
        CacheWeaponPanel();
        BindButtons();
        RefreshAll(true);
    }

    public void RefreshAll(bool immediate)
    {
        PlayerProgressSaveService.Reload();
        RefreshPlayerPanel(immediate);
        RefreshWeaponPanel(immediate);
    }

    public void Dispose()
    {
        for (int i = 0; i < _playerStatRows.Count; i++)
        {
            _playerStatRows[i]?.KillTweens();
        }

        for (int i = 0; i < _weaponItems.Count; i++)
        {
            _weaponItems[i]?.KillTweens();
        }

        for (int i = 0; i < _weaponStatRows.Count; i++)
        {
            _weaponStatRows[i]?.KillTweens();
        }

        _weaponDetailCard?.DOKill();
        _playerUpgradeButton?.transform.DOKill();
        _weaponUpgradeButton?.transform.DOKill();
    }

    private void CachePlayerPanel()
    {
        if (_playerPanel == null)
        {
            return;
        }

        Transform numericalRoot = FindTransformRecursive(_playerPanel.transform, "PlayeNumerical");
        if (numericalRoot != null)
        {
            for (int i = 0; i < numericalRoot.childCount && i < PlayerStatLabels.Length; i++)
            {
                Transform rowRoot = numericalRoot.GetChild(i);
                Text[] rowTexts = rowRoot.GetComponentsInChildren<Text>(true);
                if (rowTexts.Length < 2)
                {
                    continue;
                }

                _playerStatRows.Add(new PlayerStatRow(rowRoot as RectTransform, rowTexts[0], rowTexts[1]));
            }
        }

        _playerUpgradeButton = FindButtonByText(_playerPanel.transform, "升级");
        CacheUpgradeButtonTexts(
            _playerUpgradeButton,
            out _playerUpgradeLabel,
            out _playerUpgradeCost);
    }

    private void CacheWeaponPanel()
    {
        if (_weaponPanel == null)
        {
            return;
        }

        Transform listRoot = FindTransformRecursive(_weaponPanel.transform, "WeaponUP");
        if (listRoot != null)
        {
            for (int i = 0; i < listRoot.childCount && i < 3; i++)
            {
                RectTransform rect = listRoot.GetChild(i) as RectTransform;
                if (rect != null)
                {
                    _weaponItems.Add(new WeaponListItem(rect, i + 1));
                }
            }
        }

        Transform detailCard = FindTransformRecursive(_weaponPanel.transform, "WeaponDetailCard");
        _weaponDetailCard = detailCard as RectTransform;
        if (detailCard != null)
        {
            Transform titleRoot = FindTransformRecursive(detailCard, "Title");
            Text[] titleTexts = titleRoot != null
                ? titleRoot.GetComponentsInChildren<Text>(true)
                : Array.Empty<Text>();
            for (int i = 0; i < titleTexts.Length; i++)
            {
                Text text = titleTexts[i];
                if (text == null)
                {
                    continue;
                }

                if (text.text.StartsWith("LV", StringComparison.OrdinalIgnoreCase))
                {
                    _weaponLevel = text;
                }
                else
                {
                    _weaponTitle = text;
                }
            }

            Transform centre = FindTransformRecursive(detailCard, "Centre");
            if (centre != null)
            {
                for (int i = 0; i < centre.childCount && i < WeaponStatLabels.Length; i++)
                {
                    Transform rowRoot = centre.GetChild(i);
                    Text[] rowTexts = rowRoot.GetComponentsInChildren<Text>(true);
                    if (rowTexts.Length >= 2)
                    {
                        _weaponStatRows.Add(new WeaponStatRow(rowRoot as RectTransform, rowTexts[0], rowTexts[1]));
                    }
                }
            }
        }

        _weaponUpgradeButton = FindButtonByText(_weaponPanel.transform, "升级");
        CacheUpgradeButtonTexts(
            _weaponUpgradeButton,
            out _weaponUpgradeLabel,
            out _weaponUpgradeCost);
    }

    private void BindButtons()
    {
        if (_playerUpgradeButton != null)
        {
            _playerUpgradeButton.onClick.RemoveAllListeners();
            _playerUpgradeButton.onClick.AddListener(UpgradeAllPlayerStats);
        }

        if (_weaponUpgradeButton != null)
        {
            _weaponUpgradeButton.onClick.RemoveAllListeners();
            _weaponUpgradeButton.onClick.AddListener(UpgradeSelectedWeapon);
        }

        for (int i = 0; i < _weaponItems.Count; i++)
        {
            WeaponListItem item = _weaponItems[i];
            if (item.Button == null)
            {
                continue;
            }

            int weaponId = item.WeaponId;
            item.Button.onClick.RemoveAllListeners();
            item.Button.onClick.AddListener(() => SelectUpgradeWeapon(weaponId));
        }
    }

    private void UpgradeAllPlayerStats()
    {
        if (!PlayerProgressSaveService.TryUpgradeAllPlayerStats())
        {
            PlayDenied(_playerUpgradeButton);
            HallTipNotifier.Show("升级失败", "金币不足或玩家属性已满级", "grenade");
            return;
        }

        PlaySuccess(_playerUpgradeButton);
        RefreshPlayerPanel(false);
        RefreshWeaponPanel(false);
        int level = PlayerProgressSaveService.GetPlayerSharedUpgradeLevel();
        bool saved = PlayerProgressSaveService.CommitCurrentSession(out PlayerSaveSlotSummary _);
        HallTipNotifier.Show(
            saved ? "玩家升级成功" : "升级成功 存档失败",
            saved
                ? $"角色基础属性提升至 LV.{level} 并已保存"
                : $"角色基础属性已提升至 LV.{level} 请再次保存",
            saved ? "heal" : "grenade");
    }

    private void UpgradeSelectedWeapon()
    {
        if (!PlayerProgressSaveService.TryUpgradeWeapon(_selectedWeaponId))
        {
            PlayDenied(_weaponUpgradeButton);
            HallTipNotifier.Show("升级失败", "金币不足或武器已满级", "grenade");
            return;
        }

        PlaySuccess(_weaponUpgradeButton);
        RefreshWeaponPanel(false);
        RefreshPlayerPanel(false);
        int level = PlayerProgressSaveService.GetWeaponUpgradeLevel(_selectedWeaponId);
        bool saved = PlayerProgressSaveService.CommitCurrentSession(out PlayerSaveSlotSummary _);
        HallTipNotifier.Show(
            saved ? "武器升级成功" : "升级成功 存档失败",
            saved
                ? $"{GetWeaponDisplayName(_selectedWeaponId)} 提升至 LV.{level} 并已保存"
                : $"{GetWeaponDisplayName(_selectedWeaponId)} 已升级 请再次保存",
            saved ? "ammo" : "grenade");
    }

    private void SelectUpgradeWeapon(int weaponId)
    {
        _selectedWeaponId = Mathf.Clamp(weaponId, PistolWeaponId, ShotgunWeaponId);
        RefreshWeaponPanel(false);
        _weaponDetailCard?.DOKill();
        _weaponDetailCard?.DOPunchScale(Vector3.one * 0.025f, 0.18f, 5, 0.7f);
    }

    private void RefreshPlayerPanel(bool immediate)
    {
        PlayerSaveData saveData = PlayerProgressSaveService.Load();
        int level = PlayerProgressSaveService.GetPlayerSharedUpgradeLevel();
        PlayerBaseConfig baseConfig = PlayerDefaultConfigAsset.LoadRuntimeConfig();
        PlayerRuntimeData runtimeData = new PlayerRuntimeData();
        runtimeData.InitForNewRun(baseConfig, saveData);

        string[] values =
        {
            PlayerStatsCalculator.GetMaxHp(baseConfig, saveData).ToString(),
            $"{PlayerStatsCalculator.GetWalkSpeed(baseConfig, saveData, runtimeData):0.00} / {PlayerStatsCalculator.GetRunSpeed(baseConfig, saveData, runtimeData):0.00}",
            PlayerStatsCalculator.GetJumpHeight(baseConfig, saveData, runtimeData).ToString("0.00"),
            PermanentUpgradeConfigLoader.GetPlayerSkillCooldownReduction(
                saveData.skillCooldownLevel,
                saveData.skillCooldownLevel * 0.03f).ToString("P0"),
            PermanentUpgradeConfigLoader.GetPlayerEnergyGainBonus(
                level,
                level * 0.1f).ToString("P0")
        };

        for (int i = 0; i < _playerStatRows.Count && i < PlayerStatLabels.Length; i++)
        {
            _playerStatRows[i].SetValue(
                $"{PlayerStatLabels[i]} LV.{level}",
                values[i],
                _duration,
                immediate);
        }

        RefreshUpgradeButton(
            _playerUpgradeButton,
            _playerUpgradeLabel,
            _playerUpgradeCost,
            level,
            PlayerProgressSaveService.GetPlayerSharedMaxUpgradeLevel(),
            PlayerProgressSaveService.GetPlayerSharedUpgradeCost(level));
    }

    private void RefreshWeaponPanel(bool immediate)
    {
        int level = PlayerProgressSaveService.GetWeaponUpgradeLevel(_selectedWeaponId);
        WeaponConfig current = CreateWeaponPreview(_selectedWeaponId, level);

        if (_weaponTitle != null)
        {
            _weaponTitle.text = GetWeaponDisplayName(_selectedWeaponId);
        }

        if (_weaponLevel != null)
        {
            _weaponLevel.text = $"LV.{level}";
        }

        for (int i = 0; i < _weaponItems.Count; i++)
        {
            WeaponListItem item = _weaponItems[i];
            item.SetSelected(
                item.WeaponId == _selectedWeaponId,
                _selectedColor,
                _normalColor,
                _duration,
                immediate);
        }

        for (int i = 0; i < _weaponStatRows.Count; i++)
        {
            string label = i < WeaponStatLabels.Length ? WeaponStatLabels[i] : "属性";
            string value = GetWeaponStatValue(i, current);
            _weaponStatRows[i].SetValue(label, value, _duration, immediate);
        }

        RefreshUpgradeButton(
            _weaponUpgradeButton,
            _weaponUpgradeLabel,
            _weaponUpgradeCost,
            level,
            PlayerProgressSaveService.GetWeaponMaxUpgradeLevel(_selectedWeaponId),
            PlayerProgressSaveService.GetWeaponUpgradeCost(_selectedWeaponId, level));
    }

    private void RefreshUpgradeButton(
        Button button,
        Text label,
        Text costText,
        int level,
        int maxLevel,
        int cost)
    {
        bool reachedMaxLevel = level >= maxLevel;
        if (button != null)
        {
            button.interactable = !reachedMaxLevel;
        }

        if (label != null)
        {
            label.text = reachedMaxLevel ? "已满级" : "升级:";
        }

        if (costText != null)
        {
            costText.text = reachedMaxLevel ? "MAX" : cost.ToString();
        }
    }

    private static WeaponConfig CreateWeaponPreview(int weaponId, int level)
    {
        string path;
        switch (weaponId)
        {
            case RifleWeaponId:
                path = "WeaponConfigs/DefaultAssaultRifleWeaponConfig";
                break;
            case ShotgunWeaponId:
                path = "WeaponConfigs/DefaultShotgunWeaponConfig";
                break;
            default:
                path = "WeaponConfigs/DefaultPistolWeaponConfig";
                break;
        }

        WeaponConfigAsset asset = Resources.Load<WeaponConfigAsset>(path);
        WeaponConfig config = asset != null ? asset.CreateRuntimeConfig() : CreateFallbackWeapon(weaponId);
        PermanentUpgradeRules.ApplyWeaponLevel(config, level);
        return config;
    }

    private static WeaponConfig CreateFallbackWeapon(int weaponId)
    {
        switch (weaponId)
        {
            case RifleWeaponId:
                return WeaponConfig.CreateDefaultAssaultRifle();
            case ShotgunWeaponId:
                return WeaponConfig.CreateDefaultShotgun();
            default:
                return WeaponConfig.CreateDefaultPistol();
        }
    }

    private static string GetWeaponStatValue(int index, WeaponConfig config)
    {
        if (config == null)
        {
            return "0";
        }

        switch (index)
        {
            case 0:
                return config.damage.ToString("0.0");
            case 1:
                return (1f / Mathf.Max(0.01f, config.fireInterval)).ToString("0.00");
            case 2:
                return config.magazineSize.ToString();
            case 3:
                return config.maxReserveAmmo.ToString();
            case 4:
                return ResolveReloadTime(config).ToString("0.00");
            case 5:
                return config.recoilPitch.ToString("0.00");
            case 6:
                return config.spreadAngle.ToString("0.00");
            default:
                return "0";
        }
    }

    private static float ResolveReloadTime(WeaponConfig config)
    {
        return config.reloadMode == WeaponReloadMode.SingleRound
            ? config.reloadSingleRoundTime
            : config.reloadTime;
    }

    private static string GetWeaponDisplayName(int weaponId)
    {
        switch (weaponId)
        {
            case RifleWeaponId:
                return "步枪";
            case ShotgunWeaponId:
                return "霰弹枪";
            default:
                return "手枪";
        }
    }

    private static void CacheUpgradeButtonTexts(Button button, out Text label, out Text cost)
    {
        label = null;
        cost = null;
        if (button == null)
        {
            return;
        }

        Text[] texts = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (text.name == "GoldNum")
            {
                cost = text;
            }
            else if (label == null)
            {
                label = text;
            }
        }
    }

    private static void PlaySuccess(Button button)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect == null)
        {
            return;
        }

        rect.DOKill();
        rect.DOPunchScale(Vector3.one * 0.09f, 0.22f, 7, 0.7f);
    }

    private static void PlayDenied(Button button)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect == null)
        {
            return;
        }

        rect.DOKill();
        rect.DOShakeAnchorPos(0.2f, 9f, 14, 70f);
    }

    private static Button FindButtonByText(Transform root, string label)
    {
        if (root == null)
        {
            return null;
        }

        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text != null && text.text.Trim().StartsWith(label, StringComparison.Ordinal))
            {
                Button button = text.GetComponentInParent<Button>(true);
                if (button != null)
                {
                    return button;
                }
            }
        }

        return null;
    }

    private static Transform FindTransformRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform child = root.Find(childName);
        if (child != null)
        {
            return child;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindTransformRecursive(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private sealed class PlayerStatRow
    {
        private readonly RectTransform _rect;
        private readonly Text _label;
        private readonly Text _value;

        public PlayerStatRow(RectTransform rect, Text label, Text value)
        {
            _rect = rect;
            _label = label;
            _value = value;
        }

        public void SetValue(string label, string value, float duration, bool immediate)
        {
            if (_label != null)
            {
                _label.text = label + ":";
            }

            if (_value != null)
            {
                _value.text = value;
            }

            if (immediate || _rect == null)
            {
                return;
            }

            _rect.DOKill();
            _rect.localScale = Vector3.one * 0.96f;
            _rect.DOScale(1f, duration).SetEase(Ease.OutBack);

            RectTransform valueRect = _value != null ? _value.transform as RectTransform : null;
            valueRect?.DOKill();
            valueRect?.DOPunchScale(Vector3.one * 0.05f, 0.2f, 5, 0.7f);
        }

        public void KillTweens()
        {
            _rect?.DOKill();
            (_value?.transform as RectTransform)?.DOKill();
        }
    }

    private sealed class WeaponListItem
    {
        public readonly RectTransform Rect;
        public readonly int WeaponId;
        public readonly Button Button;
        private readonly Graphic _background;
        private readonly Vector3 _baseScale;

        public WeaponListItem(RectTransform rect, int weaponId)
        {
            Rect = rect;
            WeaponId = weaponId;
            Button = rect != null ? rect.GetComponent<Button>() : null;
            _background = rect != null ? rect.GetComponent<Graphic>() : null;
            _baseScale = rect != null ? rect.localScale : Vector3.one;
        }

        public void SetSelected(
            bool selected,
            Color selectedColor,
            Color normalColor,
            float duration,
            bool immediate)
        {
            if (Rect == null)
            {
                return;
            }

            Color color = selected ? selectedColor : normalColor;
            Vector3 scale = _baseScale * (selected ? 1.035f : 1f);
            Rect.DOKill();
            _background?.DOKill();
            if (immediate)
            {
                Rect.localScale = scale;
                if (_background != null)
                {
                    _background.color = color;
                }

                return;
            }

            Rect.DOScale(scale, duration).SetEase(Ease.OutQuad);
            _background?.DOColor(color, duration);
        }

        public void KillTweens()
        {
            Rect?.DOKill();
            _background?.DOKill();
        }
    }

    private sealed class WeaponStatRow
    {
        private readonly RectTransform _rect;
        private readonly Text _label;
        private readonly Text _value;

        public WeaponStatRow(RectTransform rect, Text label, Text value)
        {
            _rect = rect;
            _label = label;
            _value = value;
        }

        public void SetValue(string label, string value, float duration, bool immediate)
        {
            if (_label != null)
            {
                _label.text = label + ":";
            }

            if (_value != null)
            {
                _value.text = value;
            }

            if (immediate || _rect == null)
            {
                return;
            }

            _rect.DOKill();
            _rect.localScale = Vector3.one * 0.96f;
            _rect.DOScale(1f, duration).SetEase(Ease.OutBack);
        }

        public void KillTweens()
        {
            _rect?.DOKill();
        }
    }
}

public static class HallTipNotifier
{
    private const float DefaultDuration = 1.8f;

    public static void Show(string title, string description, string colorKey)
    {
        PickupTipEventData eventData = new PickupTipEventData(
            title,
            description,
            colorKey,
            DefaultDuration);

        if (UIManager.Instance == null)
        {
            EventCenter.Instance.EventTrigger(GameEvent.PickupTipRequested, eventData);
            return;
        }

        UIManager.Instance.OpenPanelAsy<TipCanvas>(_ =>
        {
            EventCenter.Instance.EventTrigger(GameEvent.PickupTipRequested, eventData);
        });
    }
}
