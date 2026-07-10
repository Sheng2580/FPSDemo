using UnityEngine;
using Weapon.Data;

namespace Combat
{
    // 挂在墙体或道具碰撞体上 用来区分命中特效
    public class HitSurface : MonoBehaviour
    {
        [SerializeField] private HitSurfaceType surfaceType = HitSurfaceType.Stone;

        public HitSurfaceType SurfaceType => surfaceType;
    }
}
