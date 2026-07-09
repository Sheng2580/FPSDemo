using UnityEngine;

namespace Weapon
{
    public class WeaponAnimationEventReceiver : MonoBehaviour
    {
        [SerializeField] private float cameraShakeScale = 1f;

        public void ShakeCameras(float strength)
        {
            // Akila 动画里有这个事件 先接住避免动画播放时报错
            _ = strength * cameraShakeScale;
        }
    }
}
