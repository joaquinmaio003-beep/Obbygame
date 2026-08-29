using UnityEngine;

/// <summary>
/// Cualquier enemigo que se pueda stunear con la piedra de Obby.
/// Lo implementan Enemy y WarriorEnemy.
/// </summary>
public interface IStunnable
{
    void Stun();
    /// <summary>Golpe de piedra: flash + knockback chico + stun. fromPos = de donde vino.</summary>
    void HitByRock(Vector2 fromPos);
    /// <summary>True si esta stuneado (se puede rematar con otra piedra).</summary>
    bool IsStunned { get; }
    /// <summary>Lo elimina (rematado con piedra estando stuneado).</summary>
    void Defeat();
}
