using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W8 (M10): Orléans volley cover. A <see cref="VolleyCover"/> child of a
/// <see cref="StoryCyclicHazard"/> shelters a player whose feet lie inside its
/// rectangle from that hazard's Active phase — deterministically, with no raycast —
/// and from no other hazard. Level 2 authors non-solid cover in every volley lane.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class VolleyCoverTests {
    private const string Level02ScenePath = "res://scenes/campaign/Level_02_Orleans.tscn";
    private static readonly Vector2 HazardOrigin = new(0f, 900f);

    private static StoryCyclicHazard AddHazard(string name, bool withCover) {
        var hazard = new StoryCyclicHazard { Name = name, HazardID = "test." + name, Damage = 18 };
        hazard.Position = HazardOrigin;
        if (withCover) hazard.AddChild(new VolleyCover { Name = "Mantlet", Position = new Vector2(-24f, 0f) });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hazard);
        return hazard;
    }

    private static PlayerController AddPlayer(Vector2 position) {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        player.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        return player;
    }

    private static void Release(PlayerController player, params Node[] others) {
        if (player != null) {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
        foreach (Node node in others) node?.Free();
    }

    [TestCase]
    public void APlayerBehindTheMantletTakesNoVolleyDamage() {
        StoryCyclicHazard hazard = AddHazard("CoveredVolley", withCover: true);
        PlayerController player = AddPlayer(HazardOrigin + new Vector2(-24f, 0f));
        try {
            AssertThat(hazard.Covers.Count).IsEqual(1);
            int hp = player.CurrentHP;
            hazard.ForcePhase(HazardPhase.Active, 5f);
            AssertThat(hazard.ApplyToPlayer(player)).IsFalse();
            AssertThat(player.CurrentHP).IsEqual(hp);
        } finally {
            Release(player, hazard);
        }
    }

    [TestCase]
    public void AnUncoveredPlayerInTheSameLaneIsHit() {
        StoryCyclicHazard hazard = AddHazard("OpenVolley", withCover: true);
        PlayerController player = AddPlayer(HazardOrigin + new Vector2(40f, 0f));
        try {
            int hp = player.CurrentHP;
            hazard.ForcePhase(HazardPhase.Active, 5f);
            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(player.CurrentHP).IsLess(hp);
        } finally {
            Release(player, hazard);
        }
    }

    [TestCase]
    public void JumpingAboveTheMantletLeavesThePlayerExposed() {
        StoryCyclicHazard hazard = AddHazard("HighVolley", withCover: true);
        PlayerController player = AddPlayer(HazardOrigin + new Vector2(-24f, -160f));
        try {
            hazard.ForcePhase(HazardPhase.Active, 5f);
            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
        } finally {
            Release(player, hazard);
        }
    }

    [TestCase]
    public void SteppingOutOfCoverMidVolleyIsStillAHit() {
        // Shelter does not spend the cycle's one hit.
        StoryCyclicHazard hazard = AddHazard("LeavingCover", withCover: true);
        PlayerController player = AddPlayer(HazardOrigin + new Vector2(-24f, 0f));
        try {
            hazard.ForcePhase(HazardPhase.Active, 5f);
            AssertThat(hazard.ApplyToPlayer(player)).IsFalse();
            player.Position = HazardOrigin + new Vector2(45f, 0f);
            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(hazard.ApplyToPlayer(player)).IsFalse();
        } finally {
            Release(player, hazard);
        }
    }

    [TestCase]
    public void CoverOnlySheltersFromItsOwnHazard() {
        StoryCyclicHazard covered = AddHazard("OwnCover", withCover: true);
        StoryCyclicHazard neighbour = AddHazard("NoCover", withCover: false);
        PlayerController player = AddPlayer(HazardOrigin + new Vector2(-24f, 0f));
        try {
            covered.ForcePhase(HazardPhase.Active, 5f);
            neighbour.ForcePhase(HazardPhase.Active, 5f);
            AssertThat(covered.ApplyToPlayer(player)).IsFalse();
            AssertThat(neighbour.ApplyToPlayer(player)).IsTrue();
        } finally {
            Release(player, covered, neighbour);
        }
    }

    [TestCase]
    public void RemovedCoverStopsSheltering() {
        StoryCyclicHazard hazard = AddHazard("RemovedCover", withCover: true);
        PlayerController player = AddPlayer(HazardOrigin + new Vector2(-24f, 0f));
        try {
            Node mantlet = hazard.GetNode("Mantlet");
            hazard.RemoveChild(mantlet);
            mantlet.Free();
            AssertThat(hazard.Covers.Count).IsEqual(0);
            hazard.ForcePhase(HazardPhase.Active, 5f);
            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
        } finally {
            Release(player, hazard);
        }
    }

    [TestCase]
    public void OrleansAuthorsNonSolidGroundCoverInsideEveryVolleyLane() {
        Node level = ResourceLoader.Load<PackedScene>(Level02ScenePath).Instantiate();
        var issues = new List<string>();
        int lanes = 0;
        try {
            foreach (Node child in level.GetChildren()) {
                if (child is not StoryCyclicHazard hazard) continue;
                lanes++;
                var shape = hazard.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                if (shape?.Shape is not RectangleShape2D rect) {
                    issues.Add($"{hazard.Name}: no rectangular volley area");
                    continue;
                }
                float laneLeft = shape.Position.X - rect.Size.X * 0.5f;
                float laneRight = shape.Position.X + rect.Size.X * 0.5f;
                int covers = 0;
                foreach (Node sub in hazard.GetChildren()) {
                    if (sub is not VolleyCover cover) continue;
                    covers++;
                    Rect2 shelter = new(cover.Position + cover.LocalShelterRect.Position, cover.LocalShelterRect.Size);
                    if (shelter.Position.X < laneLeft || shelter.End.X > laneRight) {
                        issues.Add($"{hazard.Name}/{cover.Name}: shelter spills outside the volley lane");
                    }
                    // Ground cover: its base is the lane's ground line, so a walking
                    // player of any kit reaches it with no movement ability.
                    if (!Mathf.IsEqualApprox(cover.Position.Y, 0f)) {
                        issues.Add($"{hazard.Name}/{cover.Name}: cover is not on the ground line");
                    }
                    if (cover.ShelterSize.X < 48f) {
                        issues.Add($"{hazard.Name}/{cover.Name}: shelter narrower than a standing body");
                    }
                    foreach (Node part in cover.GetChildren()) {
                        if (part is CollisionObject2D) issues.Add($"{hazard.Name}/{cover.Name}: cover must be non-solid");
                    }
                }
                if (covers == 0) issues.Add($"{hazard.Name}: volley lane has no cover");
            }
            AssertThat(lanes).IsGreaterEqual(4);
        } finally {
            level.Free();
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }
}
