using System;
using UnityEngine;
using UnityEngine.UI;

public enum UILoadType
{
    Resources,
    AssetBundle
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class UICanvasAttribute : Attribute
{
    public UILayer Layer { get; }
    public UILoadType LoadType { get; }
    public string AssetBundleName { get; set; } = "uipanel";
    public string AssetName { get; set; } = "";
    public string ResourcesPath { get; set; } = "";
    public bool UseSafeArea { get; set; } = true;

    public UICanvasAttribute() : this(UILoadType.Resources, UILayer.Normal)
    {
    }

    public UICanvasAttribute(UILayer layer) : this(UILoadType.Resources, layer)
    {
    }

    public UICanvasAttribute(UILoadType loadType) : this(loadType, UILayer.Normal)
    {
    }

    public UICanvasAttribute(UILoadType loadType, UILayer layer)
    {
        LoadType = loadType;
        Layer = layer;
    }
}

public class BaseCanvas : MonoBehaviour
{
    [SerializeField] private bool deactivateOnHide = false;

    protected Canvas canvas;
    protected CanvasGroup canvasGroup;
    protected GraphicRaycaster graphicRaycaster;
    protected RectTransform rectTransform;
    protected bool isShown;

    public virtual UILayer Layer => UILayer.Normal;
    public virtual UILoadType LoadType => UILoadType.Resources;
    public virtual string AssetBundleName => "uipanel";
    public virtual string AssetName => GetType().Name;
    public virtual string ResourcesPath => "UI/" + AssetName;
    public virtual bool UseSafeArea => true;
    public virtual bool NeedRaycaster => true;
    public bool IsShown => isShown;
    public RectTransform RectTransform => rectTransform;
    public Canvas Canvas => canvas;

    public virtual void Awake()
    {
        CacheComponents();
    }

    protected virtual void Reset()
    {
        CacheComponents();
    }

    public virtual void Show()
    {
        CacheComponents();
        gameObject.SetActive(true);

        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.overrideSorting = true;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = NeedRaycaster;
        }

        if (graphicRaycaster != null)
        {
            graphicRaycaster.enabled = NeedRaycaster;
        }

        isShown = true;
    }

    public virtual void Hide()
    {
        CacheComponents();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (graphicRaycaster != null)
        {
            graphicRaycaster.enabled = false;
        }

        if (canvas != null)
        {
            canvas.enabled = false;
        }

        isShown = false;

        if (deactivateOnHide)
        {
            gameObject.SetActive(false);
        }
    }

    public virtual void SetSortingOrder(int order)
    {
        CacheComponents();
        if (canvas == null)
        {
            return;
        }

        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
    }

    public void StretchFullScreen()
    {
        CacheComponents();
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    protected void CacheComponents()
    {
        rectTransform ??= transform as RectTransform;
        canvas ??= GetComponent<Canvas>();
        canvasGroup ??= GetComponent<CanvasGroup>();
        graphicRaycaster ??= GetComponent<GraphicRaycaster>();

        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
}
