[UICanvas(UILoadType.AssetBundle, UILayer.Touch)]
public class TounchControllerCanvas : BaseCanvas
{
    private AkilaCrosshairDriver _crosshairDriver;

    public override void Awake()
    {
        base.Awake();
        EnsureAkilaCrosshair();
    }

    private void OnEnable()
    {
        EnsureAkilaCrosshair();
    }

    private void EnsureAkilaCrosshair()
    {
        _crosshairDriver ??= GetComponentInChildren<AkilaCrosshairDriver>(true);
        if (_crosshairDriver == null)
        {
            _crosshairDriver = gameObject.AddComponent<AkilaCrosshairDriver>();
        }

        _crosshairDriver.EnsureBuilt();
    }
}
