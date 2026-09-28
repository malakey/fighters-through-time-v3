namespace FTT.UI {

    /// <summary>
    /// Package 12 W6 (G15e). The Third-Party Licenses list the main menu's
    /// Extras screen shows — Godot, Klotho and every bundled addon and library
    /// (design Section 7). Product names are proper nouns and are shown as-is;
    /// license names resolve through translation keys.
    ///
    /// <para>Keep this list in step with <c>FightersThroughTime.csproj</c> and
    /// <c>addons/</c>: a new bundled dependency needs a row here. The license
    /// identifications match the shipped license files (<c>addons/klotho/LICENSE</c>
    /// is Apache-2.0, <c>addons/gdUnit4/LICENSE</c> is MIT) and the packages'
    /// published licenses. No font is bundled yet (G05), so there is no font row.</para>
    /// </summary>
    public static class ThirdPartyLicenses {

        /// <summary>One notice row: a product and its license's translation key.</summary>
        public readonly record struct Notice(string Product, string LicenseKey);

        public const string MitKey = "license_name_mit";
        public const string Apache2Key = "license_name_apache2";

        /// <summary>Every bundled third-party component, in display order.</summary>
        public static readonly Notice[] Notices = {
            new("Godot Engine", MitKey),
            new("Klotho", Apache2Key),
            new("K4os.Compression.LZ4", MitKey),
            new("Newtonsoft.Json", MitKey),
            new("LiteNetLib", MitKey),
            new("GdUnit4", MitKey)
        };
    }
}
