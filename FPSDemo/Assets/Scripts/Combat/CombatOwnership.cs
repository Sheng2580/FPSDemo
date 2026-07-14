using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 战斗归属判断
    /// </summary>
    public static class CombatOwnership
    {
        public static bool IsPlayerOwnedDamage(DamageInfo damageInfo, PlayerController player)
        {
            if (player == null || damageInfo.attacker == null)
            {
                return false;
            }

            if (damageInfo.attacker == player.gameObject)
            {
                return true;
            }

            PlayerController attackerPlayer = damageInfo.attacker.GetComponentInParent<PlayerController>();
            return attackerPlayer == player;
        }
    }
}
