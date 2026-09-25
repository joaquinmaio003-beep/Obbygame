using UnityEngine;

/// <summary>
/// Cualquier enemigo que se pueda stunear con la piedra de Obby.
/// Lo implementan Enemy, WarriorEnemy y EnemigoSierra.
/// </summary>
public interface IStunnable
{
    void Stun();
    /// <summary>Golpe de piedra: flash + knockback chico + stun. fromPos = de donde vino.</summary>
    void HitByRock(Vector2 fromPos);
    /// <summary>True si esta stuneado (se puede rematar con otra piedra).</summary>
    bool IsStunned { get; }
    /// <summary>Lo elimina (aplastado por la roca, un pincho, etc): flash, se aplasta y desaparece.</summary>
    void Defeat();
    /// <summary>Se rompio la plataforma donde estaba: cae atravesando todo y desaparece.</summary>
    void CaerYMorir(float gravedad, float tiempo);
}
