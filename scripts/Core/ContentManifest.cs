using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FTT.Core {

    public enum ContentCategory {
        StoryLevel,
        FighterStage,
        Character,
        Ability,
        ResonanceGrid,
        Enemy,
        Boss,
        DialogueSet,
        UIScreen,
        Pool,
        AudioSet,
        VisualSet,
        Template
    }

    public enum ContentImplementationState {
        Planned,
        Prototype,
        Implemented,
        Production
    }

    public enum ContentAssetStatus {
        Placeholder,
        ReadyForReplacement,
        Final,
        NotRequired
    }

    public enum ContentValidationState {
        Pending,
        Valid,
        Blocked
    }

    public sealed class ContentManifestEntry {
        public ContentCategory Category { get; init; }
        public string ContentID { get; init; } = "";
        public string SourceSection { get; init; } = "";
        public string ResourcePath { get; init; } = "";
        public string OwnerSystem { get; init; } = "";
        public ContentImplementationState ImplementationState { get; init; }
        public ContentAssetStatus AssetStatus { get; init; }
        public ContentValidationState ValidationState { get; init; }
        public bool ResourceRequired { get; init; }
    }

    public sealed class ContentManifest {
        public const string DefaultPath = "res://resources/Content/content_manifest.csv";
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; private init; }
        public IReadOnlyList<ContentManifestEntry> Entries { get; private init; } = Array.Empty<ContentManifestEntry>();

        public static ContentManifest LoadDefault() => Load(DefaultPath);

        public static ContentManifest Load(string path) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Manifest path is required.", nameof(path));
            if (!FileAccess.FileExists(path)) throw new InvalidOperationException($"Content manifest was not found at '{path}'.");

            string[] lines = FileAccess.GetFileAsString(path)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length < 3 || !lines[0].StartsWith("schema_version,", StringComparison.Ordinal)) {
                throw new InvalidOperationException("Content manifest must begin with a schema_version row.");
            }

            int schemaVersion = int.Parse(lines[0].Split(',')[1], CultureInfo.InvariantCulture);
            var entries = new List<ContentManifestEntry>(lines.Length - 2);

            for (int index = 2; index < lines.Length; index++) {
                string line = lines[index];
                if (line.Length == 0 || line.StartsWith('#')) continue;

                string[] columns = line.Split(',', StringSplitOptions.None);
                if (columns.Length != 9) {
                    throw new InvalidOperationException($"Content manifest line {index + 1} has {columns.Length} columns; expected 9.");
                }

                entries.Add(new ContentManifestEntry {
                    Category = ParseEnum<ContentCategory>(columns[0], index),
                    ContentID = columns[1].Trim(),
                    SourceSection = columns[2].Trim(),
                    ResourcePath = columns[3].Trim(),
                    OwnerSystem = columns[4].Trim(),
                    ImplementationState = ParseEnum<ContentImplementationState>(columns[5], index),
                    AssetStatus = ParseEnum<ContentAssetStatus>(columns[6], index),
                    ValidationState = ParseEnum<ContentValidationState>(columns[7], index),
                    ResourceRequired = bool.Parse(columns[8].Trim())
                });
            }

            return new ContentManifest {
                SchemaVersion = schemaVersion,
                Entries = entries
            };
        }

        public IEnumerable<ContentManifestEntry> ForCategory(ContentCategory category) {
            foreach (ContentManifestEntry entry in Entries) {
                if (entry.Category == category) yield return entry;
            }
        }

        private static T ParseEnum<T>(string value, int zeroBasedLineIndex) where T : struct, Enum {
            if (Enum.TryParse(value.Trim(), true, out T parsed)) return parsed;
            throw new InvalidOperationException($"Content manifest line {zeroBasedLineIndex + 1} has invalid {typeof(T).Name} value '{value}'.");
        }
    }

    public enum ManifestIssueSeverity {
        Warning,
        Error
    }

    public readonly record struct ManifestValidationIssue(
        ManifestIssueSeverity Severity,
        string ContentID,
        string Message);

    public static class ContentManifestValidator {
        /// <summary>
        /// Package 11 A12 (V7.6): the campaign grew a seventeenth slot — Level 4A, the
        /// per-character Legacy Level. A12 ships the Einstein exemplar, so StoryLevel is
        /// 17 and DialogueSet 18; all nine variants share one AudioSet, so that moved by
        /// one. <b>B1-B3 raise StoryLevel to 25 and DialogueSet to 26</b> with the other
        /// eight heroes; AudioSet stays at 28.
        /// </summary>
        private static readonly Dictionary<ContentCategory, int> ExactRequiredCounts = new() {
            [ContentCategory.StoryLevel] = 17,
            [ContentCategory.FighterStage] = 10,
            [ContentCategory.Character] = 9,
            [ContentCategory.Ability] = 36,
            [ContentCategory.ResonanceGrid] = 9,
            [ContentCategory.Boss] = 15,
            [ContentCategory.DialogueSet] = 18,
            [ContentCategory.UIScreen] = 22,
            [ContentCategory.Pool] = 7,
            [ContentCategory.AudioSet] = 28,
            [ContentCategory.VisualSet] = 41,
            [ContentCategory.Template] = 14
        };

        public static IReadOnlyList<ManifestValidationIssue> Validate(
            ContentManifest manifest,
            bool requireAllResources = false) {
            var issues = new List<ManifestValidationIssue>();
            if (manifest == null) {
                issues.Add(new ManifestValidationIssue(ManifestIssueSeverity.Error, "manifest", "Manifest is null."));
                return issues;
            }
            if (manifest.SchemaVersion != ContentManifest.CurrentSchemaVersion) {
                issues.Add(new ManifestValidationIssue(
                    ManifestIssueSeverity.Error,
                    "manifest",
                    $"Schema {manifest.SchemaVersion} is unsupported; expected {ContentManifest.CurrentSchemaVersion}."));
            }

            var stableIDs = new HashSet<string>(StringComparer.Ordinal);
            var categoryCounts = new Dictionary<ContentCategory, int>();
            var uniquePaths = new Dictionary<ContentCategory, HashSet<string>>();

            foreach (ContentManifestEntry entry in manifest.Entries) {
                string qualifiedID = $"{entry.Category}:{entry.ContentID}";
                if (string.IsNullOrWhiteSpace(entry.ContentID)) {
                    issues.Add(Error(qualifiedID, "Stable content ID is empty."));
                } else if (!stableIDs.Add(qualifiedID)) {
                    issues.Add(Error(qualifiedID, "Stable content ID is duplicated within its category."));
                }
                if (string.IsNullOrWhiteSpace(entry.SourceSection)) issues.Add(Error(qualifiedID, "Source design section is empty."));
                if (string.IsNullOrWhiteSpace(entry.OwnerSystem)) issues.Add(Error(qualifiedID, "Owner system is empty."));

                categoryCounts[entry.Category] = categoryCounts.GetValueOrDefault(entry.Category) + 1;

                if (entry.ResourceRequired) {
                    if (string.IsNullOrWhiteSpace(entry.ResourcePath) || !entry.ResourcePath.StartsWith("res://", StringComparison.Ordinal)) {
                        issues.Add(Error(qualifiedID, "Required resource path must use res://."));
                    } else {
                        bool exists = ResourceLoader.Exists(entry.ResourcePath) || FileAccess.FileExists(entry.ResourcePath);
                        bool mustExist = requireAllResources || entry.ValidationState == ContentValidationState.Valid ||
                                         entry.ImplementationState is ContentImplementationState.Implemented or ContentImplementationState.Production;
                        if (mustExist && !exists) issues.Add(Error(qualifiedID, $"Required resource does not exist: {entry.ResourcePath}"));
                        else if (!exists) issues.Add(Warning(qualifiedID, $"Planned resource is not authored yet: {entry.ResourcePath}"));
                    }
                }

                if (RequiresUniquePath(entry.Category) && !string.IsNullOrWhiteSpace(entry.ResourcePath)) {
                    if (!uniquePaths.TryGetValue(entry.Category, out HashSet<string> paths)) {
                        paths = new HashSet<string>(StringComparer.Ordinal);
                        uniquePaths[entry.Category] = paths;
                    }
                    if (!paths.Add(entry.ResourcePath)) issues.Add(Error(qualifiedID, $"Resource path is duplicated in {entry.Category}: {entry.ResourcePath}"));
                }

                if (entry.AssetStatus is ContentAssetStatus.ReadyForReplacement or ContentAssetStatus.Final &&
                    entry.ResourceRequired &&
                    !(ResourceLoader.Exists(entry.ResourcePath) || FileAccess.FileExists(entry.ResourcePath))) {
                    issues.Add(Error(qualifiedID, $"{entry.AssetStatus} entries must reference an existing resource."));
                }
            }

            foreach ((ContentCategory category, int expectedCount) in ExactRequiredCounts) {
                int actual = categoryCounts.GetValueOrDefault(category);
                if (actual != expectedCount) issues.Add(Error(category.ToString(), $"Expected {expectedCount} entries but found {actual}."));
            }
            int enemyCount = categoryCounts.GetValueOrDefault(ContentCategory.Enemy);
            if (enemyCount < 26) issues.Add(Error(ContentCategory.Enemy.ToString(), $"Expected at least 26 entries but found {enemyCount}."));

            return issues;
        }

        public static bool HasErrors(IReadOnlyList<ManifestValidationIssue> issues) {
            foreach (ManifestValidationIssue issue in issues) {
                if (issue.Severity == ManifestIssueSeverity.Error) return true;
            }
            return false;
        }

        private static bool RequiresUniquePath(ContentCategory category) => category is
            ContentCategory.StoryLevel or ContentCategory.FighterStage or ContentCategory.Character or
            ContentCategory.Ability or ContentCategory.ResonanceGrid or ContentCategory.Enemy or
            ContentCategory.Boss or ContentCategory.Template;

        private static ManifestValidationIssue Error(string id, string message) =>
            new(ManifestIssueSeverity.Error, id, message);

        private static ManifestValidationIssue Warning(string id, string message) =>
            new(ManifestIssueSeverity.Warning, id, message);
    }
}
