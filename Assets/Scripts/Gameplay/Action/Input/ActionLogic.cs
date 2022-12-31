using System;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// List of all Types of Actions. There is a many-to-one mapping of Actions to ActionLogics.
    /// </summary>
    public enum ActionLogic
    {
        Melee,
        RangedTargeted,
        Chase,
        Revive,
        LaunchProjectile,
        Emote,
        RangedFXTargeted,
        AoE,
        Trample,
        ChargedShield,
        Stunned,
        Target,
        ChargedLaunchProjectile,
        StealthMode,
        DashAttack,
        ImpToss,
        PickUp,
#if !P56
        Drop
#else   // !P56
        Drop,
        LaunchHomingProjectile
#endif  // !P56
    }
}
