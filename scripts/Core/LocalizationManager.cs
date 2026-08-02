using Godot;
using System.Collections.Generic;

namespace FTT.Core {

    public partial class LocalizationManager : Node {
        public static LocalizationManager Instance { get; private set; }

        private string _currentLocale = "en";
        private Dictionary<string, Dictionary<string, string>> _translations = new();

        public override void _Ready() {
            Instance = this;
            TranslationServer.SetLocale(_currentLocale);
            LoadTranslations(_currentLocale);
        }

        public void SetLocale(string locale) {
            _currentLocale = locale;
            TranslationServer.SetLocale(locale);
            LoadTranslations(locale);
        }

        private void LoadTranslations(string locale) {
            string path = $"res://localization/{locale}.csv";
            if (!FileAccess.FileExists(path)) return;

            var dict = new Dictionary<string, string>();
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            while (!file.EofReached()) {
                var line = file.GetCsvLine();
                if (line.Length >= 2) {
                    dict[line[0]] = line[1];
                }
            }
            _translations[locale] = dict;
        }

        public string Tr(string key) {
            if (_translations.TryGetValue(_currentLocale, out var dict) && dict.TryGetValue(key, out var val)) {
                return val;
            }
            return TranslationServer.Translate(key);
        }

        public string CurrentLocale => _currentLocale;
    }
}
