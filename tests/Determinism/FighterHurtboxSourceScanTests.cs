using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Combat and movement update 2026-10-16, decision 8.4 (plan §2.8.3, CM-SIM A0
/// commit 4): one named fighter hurtbox in the sim. The ten identical copies
/// of <c>new(FP64.FromDouble(0.5), FP64.One)</c> became
/// <see cref="FighterHurtbox.HalfExtents"/>, with the orb-pickup and hazard
/// overlap box named separately as <see cref="FighterBodyOverlap.HalfExtents"/>
/// (a forwarder). The size is unchanged and raw-identical. Pure C#: the source
/// is read with <c>System.IO</c> from the path this file was compiled from.
/// </summary>
[TestSuite]
public class FighterHurtboxSourceScanTests {
    private const string Expression = "new(FP64.FromDouble(0.5), FP64.One)";

    [TestCase]
    public void TheSimHasOneNamedHurtboxAndTheBodyOverlapAliasForwardsToIt() {
        string simRoot = Path.Combine(RepositoryRoot(), "scripts", "FighterSim");
        AssertThat(Directory.Exists(simRoot)).OverrideFailureMessage($"Harness: {simRoot} not found.").IsTrue();

        var copies = new List<string>();
        foreach (string file in Directory.GetFiles(simRoot, "*.cs", SearchOption.AllDirectories)) {
            string text = File.ReadAllText(file);
            int count = 0;
            for (int at = text.IndexOf(Expression); at >= 0; at = text.IndexOf(Expression, at + 1)) count++;
            for (int index = 0; index < count; index++) copies.Add(Path.GetFileName(file));
        }
        if (copies.Count != 1 || copies[0] != "FighterHurtbox.cs") {
            AssertThat("hurtbox copies: " + string.Join(" | ", copies)).IsEqual("hurtbox copies: FighterHurtbox.cs");
        }

        string hurtboxSource = File.ReadAllText(Path.Combine(simRoot, "FighterHurtbox.cs"));
        int alias = hurtboxSource.IndexOf("class FighterBodyOverlap");
        AssertThat(alias >= 0).OverrideFailureMessage("FighterBodyOverlap must live beside the hurtbox.").IsTrue();
        AssertThat(hurtboxSource.IndexOf("= FighterHurtbox.HalfExtents;", alias) > alias)
            .OverrideFailureMessage("FighterBodyOverlap must forward to FighterHurtbox.HalfExtents, not hold a copy.")
            .IsTrue();

        long half = FP64.FromDouble(0.5).RawValue;
        AssertThat(FighterHurtbox.HalfExtents.x.RawValue).IsEqual(half);
        AssertThat(FighterHurtbox.HalfExtents.y.RawValue).IsEqual(FP64.One.RawValue);
        AssertThat(FighterBodyOverlap.HalfExtents.x.RawValue).IsEqual(FighterHurtbox.HalfExtents.x.RawValue);
        AssertThat(FighterBodyOverlap.HalfExtents.y.RawValue).IsEqual(FighterHurtbox.HalfExtents.y.RawValue);
        AssertThat(FighterKitGeometry.FighterHalfExtents.x.RawValue).IsEqual(FighterHurtbox.HalfExtents.x.RawValue);
        AssertThat(FighterKitGeometry.FighterHalfExtents.y.RawValue).IsEqual(FighterHurtbox.HalfExtents.y.RawValue);
        AssertThat(FighterCombatSystem.VictimHurtboxHalfWidth.RawValue).IsEqual(FighterHurtbox.HalfExtents.x.RawValue);
    }

    /// <summary>The repository root: two directories above this source file.</summary>
    private static string RepositoryRoot([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here) ?? ".", "..", ".."));
}
