using UnityEngine;
using Weapon.Data;

namespace Combat
{
    /// <summary>
    /// 战斗层级名称统一入口
    /// </summary>
    public static class CombatLayerNames
    {
        public const string Enemy = "Enemy";
        public const string SurfaceStone = "Surface_Stone";
        public const string SurfaceMetal = "Surface_Metal";
        public const string SurfaceWood = "Surface_Wood";
        public const string SurfaceGlass = "Surface_Glass";

        private static int enemyLayer = int.MinValue;
        private static int surfaceStoneLayer = int.MinValue;
        private static int surfaceMetalLayer = int.MinValue;
        private static int surfaceWoodLayer = int.MinValue;
        private static int surfaceGlassLayer = int.MinValue;

        public static int EnemyLayer => GetCachedLayer(ref enemyLayer, Enemy);

        public static bool IsEnemyLayer(int layer)
        {
            int targetLayer = EnemyLayer;
            return targetLayer >= 0 && layer == targetLayer;
        }

        public static HitSurfaceType ResolveSurfaceType(int layer)
        {
            if (LayerEquals(layer, ref surfaceMetalLayer, SurfaceMetal))
            {
                return HitSurfaceType.Metal;
            }

            if (LayerEquals(layer, ref surfaceWoodLayer, SurfaceWood))
            {
                return HitSurfaceType.Wood;
            }

            if (LayerEquals(layer, ref surfaceGlassLayer, SurfaceGlass))
            {
                return HitSurfaceType.Glass;
            }

            if (LayerEquals(layer, ref surfaceStoneLayer, SurfaceStone))
            {
                return HitSurfaceType.Stone;
            }

            return HitSurfaceType.Default;
        }

        private static bool LayerEquals(int layer, ref int cachedLayer, string layerName)
        {
            int targetLayer = GetCachedLayer(ref cachedLayer, layerName);
            return targetLayer >= 0 && layer == targetLayer;
        }

        private static int GetCachedLayer(ref int cachedLayer, string layerName)
        {
            if (cachedLayer == int.MinValue)
            {
                cachedLayer = LayerMask.NameToLayer(layerName);
            }

            return cachedLayer;
        }
    }
}
