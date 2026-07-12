using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 战斗 HUD 总入口
/// 负责组织伤害数字和能量显示 不处理具体战斗数值
/// </summary>
[UICanvas(UILoadType.AssetBundle, UILayer.HUD)]
[DisallowMultipleComponent]
public class HUDCanvas : BaseCanvas
{
    private const string CombatSceneName = "Combat";

    [Header("中心")]
    [SerializeField] private DamageCentre damageCentre;
    [SerializeField] private EnergyCentre energyCentre;

    public DamageCentre DamageCentre => damageCentre;
    public EnergyCentre EnergyCentre => energyCentre;
    public override bool NeedRaycaster => false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OpenForCombatScene()
    {
        if (SceneManager.GetActiveScene().name != CombatSceneName)
        {
            return;
        }

        UIManager.Instance.OpenPanelAsy<HUDCanvas>();
    }

    public override void Awake()
    {
        base.Awake();
        CacheReferences();
    }

    protected override void Reset()
    {
        base.Reset();
        CacheReferences();
    }

    private void CacheReferences()
    {
        damageCentre ??= GetComponentInChildren<DamageCentre>(true);
        energyCentre ??= GetComponentInChildren<EnergyCentre>(true);
    }
}
