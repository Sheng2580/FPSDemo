using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 战斗时间统一格式化入口
    /// </summary>
    public static class CombatTimeFormatter
    {
        public static string Format(float elapsedSeconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
