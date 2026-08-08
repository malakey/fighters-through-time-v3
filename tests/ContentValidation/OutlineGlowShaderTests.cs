using System.Collections.Generic;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 A3: the unified outline/glow shader contract
/// (design-godot.md "Unified Outline &amp; Glow Shader System", parameter table).
/// The uniform names are load-bearing — the arbiter writes them by string and a
/// silent rename would disable every glow state without failing anything else.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OutlineGlowShaderTests {

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void TheShaderResourceExistsAtTheLockedPath() {
        AssertThat(GlowPresentationController.ShaderPath)
            .IsEqual("res://assets/shaders/outline_glow.gdshader");
        AssertThat(ResourceLoader.Exists(GlowPresentationController.ShaderPath)).IsTrue();
        // Shaders are streamed presentation, not AuthoredResources cache entries.
        var shader = ResourceLoader.Load<Shader>(GlowPresentationController.ShaderPath);
        AssertObject(shader).IsNotNull();
    }

    [TestCase]
    public void TheShaderExposesTheFourDesignedUniforms() {
        var shader = ResourceLoader.Load<Shader>(GlowPresentationController.ShaderPath);
        AssertObject(shader).IsNotNull();

        var names = new HashSet<string>();
        using Godot.Collections.Array uniforms = shader.GetShaderUniformList();
        foreach (Variant entry in uniforms) {
            names.Add(entry.AsGodotDictionary()["name"].AsString());
        }

        AssertThat(names.Contains(GlowPresentationController.OutlineColorUniform)).IsTrue();
        AssertThat(names.Contains(GlowPresentationController.OutlineThicknessUniform)).IsTrue();
        AssertThat(names.Contains(GlowPresentationController.GlowIntensityUniform)).IsTrue();
        AssertThat(names.Contains(GlowPresentationController.PulseSpeedUniform)).IsTrue();
    }

    [TestCase]
    public void TheUniformNamesMatchTheDesignedParameterSet() {
        AssertThat(GlowPresentationController.OutlineColorUniform).IsEqual("outline_color");
        AssertThat(GlowPresentationController.OutlineThicknessUniform).IsEqual("outline_thickness");
        AssertThat(GlowPresentationController.GlowIntensityUniform).IsEqual("glow_intensity");
        AssertThat(GlowPresentationController.PulseSpeedUniform).IsEqual("pulse_speed");
    }

    [TestCase]
    public void TheShaderIsACanvasItemShaderWithTheAuthoredRanges() {
        var shader = ResourceLoader.Load<Shader>(GlowPresentationController.ShaderPath);
        AssertObject(shader).IsNotNull();
        AssertThat(shader.GetMode()).IsEqual(Shader.Mode.CanvasItem);

        string code = shader.Code;
        AssertThat(code.Contains("hint_range(0.0, 5.0)")).IsTrue();
        AssertThat(code.Contains("hint_range(0.5, 3.0)")).IsTrue();
        AssertThat(code.Contains("source_color")).IsTrue();
        // gl_compatibility rejects TEXTURE as a sampler2D function argument, so
        // the ring sampling must stay inline. Guard against a refactor into a
        // helper that would only fail at material-assignment time.
        AssertThat(code.Contains("sampler2D tex")).IsFalse();
    }

    [TestCase]
    public void AMaterialBuiltFromTheShaderAcceptsEveryUniform() {
        var shader = ResourceLoader.Load<Shader>(GlowPresentationController.ShaderPath);
        var material = new ShaderMaterial { Shader = shader };

        material.SetShaderParameter(GlowPresentationController.OutlineColorUniform, new Color(1f, 0f, 0f, 1f));
        material.SetShaderParameter(GlowPresentationController.OutlineThicknessUniform, 2.5f);
        material.SetShaderParameter(GlowPresentationController.GlowIntensityUniform, 1.8f);
        material.SetShaderParameter(GlowPresentationController.PulseSpeedUniform, 2f);

        AssertThat(material.GetShaderParameter(GlowPresentationController.OutlineColorUniform)
            .AsColor()).IsEqual(new Color(1f, 0f, 0f, 1f));
        AssertThat(material.GetShaderParameter(GlowPresentationController.OutlineThicknessUniform)
            .AsSingle()).IsEqual(2.5f);
        AssertThat(material.GetShaderParameter(GlowPresentationController.GlowIntensityUniform)
            .AsSingle()).IsEqual(1.8f);
        AssertThat(material.GetShaderParameter(GlowPresentationController.PulseSpeedUniform)
            .AsSingle()).IsEqual(2f);
    }

    [TestCase]
    public void ThePointLightGradientTextureExists() {
        AssertThat(ResourceLoader.Exists(GlowPresentationController.LightTexturePath)).IsTrue();
        var texture = ResourceLoader.Load<Texture2D>(GlowPresentationController.LightTexturePath);
        AssertObject(texture).IsNotNull();
        AssertThat(texture.GetWidth() > 0).IsTrue();
        AssertThat(texture.GetHeight() > 0).IsTrue();
    }
}
