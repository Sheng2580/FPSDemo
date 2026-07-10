using UnityEngine.UI;
using UnityEngine;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/UI/Crosshair"), RequireComponent(typeof(CanvasGroup))]

    public class Crosshair : MonoBehaviour
    {
        public float size = 1;
        public float sizeMatchingTime = 0.1f;

        public Color color = Color.white;
        public RectTransform crosshairHolder;

        [ReadOnly] public Firearm firearm;

        private float amount;
        private float sizeMatchingVel;
        private bool useManualState;
        private float manualAimProgress;
        private float manualSprayAmount = 1f;

        private CanvasGroup canvasGroup;
        private Image[] cachedImages;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            CacheImages();

            if (crosshairHolder != null)
            {
                crosshairHolder.sizeDelta = Vector2.zero;
            }
        }

        private void Update()
        {
            float aimProgress;
            float sprayAmount;
            if (firearm != null)
            {
                aimProgress = firearm.aimProgress;
                sprayAmount = firearm.currentSprayAmount;
            }
            else if (useManualState)
            {
                aimProgress = manualAimProgress;
                sprayAmount = manualSprayAmount;
            }
            else
            {
                return;
            }

            if (canvasGroup == null || crosshairHolder == null)
            {
                return;
            }

            ApplyColor();

            float aimScale = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(aimProgress));
            canvasGroup.alpha = Mathf.Clamp01(aimScale);
            amount = Mathf.SmoothDamp(amount, sprayAmount, ref sizeMatchingVel, Mathf.Max(0.001f, sizeMatchingTime));

            // 开镜时向中心缩小并消失 取消开镜时用同一条曲线反播恢复
            crosshairHolder.sizeDelta = Vector2.one * size * amount * aimScale;
        }

        public void SetManualState(float aimProgress, float sprayAmount)
        {
            // 给非 Akila 武器系统使用 只驱动准星表现
            useManualState = true;
            manualAimProgress = Mathf.Clamp01(aimProgress);
            manualSprayAmount = Mathf.Max(0f, sprayAmount);
        }

        public void StopManualState()
        {
            useManualState = false;
        }

        public void RefreshImages()
        {
            CacheImages();
            ApplyColor();
        }

        private void CacheImages()
        {
            cachedImages = crosshairHolder != null
                ? crosshairHolder.GetComponentsInChildren<Image>(true)
                : GetComponentsInChildren<Image>(true);
        }

        private void ApplyColor()
        {
            if (cachedImages == null || cachedImages.Length == 0)
            {
                CacheImages();
            }

            foreach (Image image in cachedImages)
            {
                if (image == null)
                {
                    continue;
                }

                image.color = color;
            }
        }
    }
}
