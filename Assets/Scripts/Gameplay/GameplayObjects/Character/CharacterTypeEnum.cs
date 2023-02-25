using System;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// This corresponds to a CharacterClass ScriptableObject data object, containing the core gameplay data for
    /// a given class.
    /// </summary>
    public enum CharacterTypeEnum
    {
        //heroes
        Tank,
        Archer,
        Mage,
        Rogue,

#if P56
        Pako,
        /*
        // custom player characters
        Custom1,
        Custom2,
        Custom3,
        Custom4,
        Custom5,
        Custom6,
        Custom7,
        Custom8,
        */
#endif  // P56

        //monsters
        Imp,
        ImpBoss,
        VandalImp
    }
}
