using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Tests.Determinism;

/// <summary>
/// A construct's OWN attack (damage, interval) is authored in its ability <c>.tres</c> and read from there
/// by both modes (the lingering-kit integration ruling); the rulebook holds only charges and the synced
/// numbers. Tests read the authored numbers here instead of restating them. Needs the Godot runtime.
/// </summary>
public static class ConstructOwnAttack {
    public static string PathFor(int objectTypeID) => objectTypeID switch {
        FighterConstructCharges.CoilObjectTypeID => "res://resources/Abilities/tesla/special_1.tres",
        FighterConstructCharges.TurretObjectTypeID => "res://resources/Abilities/leonardo/special_2.tres",
        _ => "res://resources/Abilities/cleopatra/special_1.tres"
    };

    public static AbilityData Data(int objectTypeID) => AuthoredResources.Load<AbilityData>(PathFor(objectTypeID));

    public static int Damage(int objectTypeID) => (int)System.Math.Round(Data(objectTypeID).BaseDamage);

    public static int IntervalFrames(int objectTypeID) => Data(objectTypeID).DamageTickIntervalFrames;

    /// <summary>Places the construct through the real spawn path with its authored own-attack numbers.</summary>
    public static bool Spawn(FighterSimulation sim, int ownerPlayerID, int objectTypeID, FPVector2 feet) =>
        sim.SpawnPersistentForTest(ownerPlayerID, objectTypeID, feet, Damage(objectTypeID), IntervalFrames(objectTypeID));
}
