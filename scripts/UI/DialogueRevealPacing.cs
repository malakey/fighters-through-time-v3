using System;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B3. The typewriter reveal, extracted from
    /// <see cref="DialogueManager"/> as a pure model with no Godot dependency.
    ///
    /// <para>Three behaviours live here, and all three are things the old
    /// "add <c>CharactersPerSecond * delta</c> to a float" reveal could not
    /// express:</para>
    /// <list type="number">
    ///   <item><b>Punctuation pacing.</b> A sentence that ends mid-line used to
    ///   run straight into the next one at a flat 30 cps. The reveal now holds
    ///   briefly after <c>. ! ?</c> and more briefly after <c>, ; :</c>, which
    ///   is what makes read-along text scan as speech rather than as a
    ///   ticker.</item>
    ///   <item><b>Chirp cadence.</b> One chirp per revealed character at 30 cps
    ///   is thirty voices a second — it machine-guns the pooled voices A2 warned
    ///   about and reads as noise. Chirps fire on every
    ///   <see cref="ChirpEveryNCharacters"/>th <i>non-whitespace</i> character,
    ///   which lands at a steady ten per second while text is moving and stops
    ///   dead during a punctuation hold.</item>
    ///   <item><b>Frame-hitch containment.</b> A long frame (a scene load, a
    ///   pooled warm-up) used to dump an entire line instantly and, with chirps
    ///   attached, fire a burst of them in one frame. <see cref="Advance"/>
    ///   clamps its own step to <see cref="MaxAdvanceSeconds"/>.</item>
    /// </list>
    ///
    /// <para>The model is deliberately pumped rather than tween-driven: the
    /// caller hands it a delta and reads the results back, so a test can step it
    /// exactly the way <c>DialogueRevealPacingTests</c> does without waiting on
    /// engine frames. Same reasoning A2 recorded for its crossfades.</para>
    /// </summary>
    public sealed class DialogueRevealModel {

        /// <summary>
        /// The design-default reveal rate — Text Speed <b>Normal</b> (design-godot.md
        /// Section 12). Package 12 W6 (G10) made the rate a player setting, so this
        /// is no longer the only rate: it is the one every other number here is
        /// authored against. The rates themselves live in
        /// <see cref="FTT.Core.TextSpeedRules"/>.
        /// </summary>
        public const float CharactersPerSecond = FTT.Core.TextSpeedRules.NormalCharactersPerSecond;

        /// <summary>Hold after a sentence-ending mark at Normal speed: <c>. ! ?</c></summary>
        public const float SentencePauseSeconds = 0.26f;

        /// <summary>Hold after a clause mark at Normal speed: <c>, ; :</c></summary>
        public const float ClausePauseSeconds = 0.12f;

        /// <summary>A chirp every Nth revealed non-whitespace character at Normal speed.</summary>
        public const int ChirpEveryNCharacters = 3;

        /// <summary>The Text Speed this model reveals at. Set before <see cref="Begin"/>.</summary>
        public FTT.Core.TextSpeed Speed { get; private set; } = FTT.Core.TextSpeed.Normal;

        /// <summary>The active rate in characters per second (∞ for Instant).</summary>
        public float Rate => FTT.Core.TextSpeedRules.CharactersPerSecond(Speed);

        /// <summary>
        /// Punctuation holds scale with the rate (G10: "punctuation holds scale
        /// sensibly"): Slow holds 1.5×, Fast 0.5×, so a beat stays the same length
        /// in <em>characters</em> at every speed. Instant has no holds at all.
        /// </summary>
        public float PauseScale => float.IsInfinity(Rate) ? 0f : CharactersPerSecond / Rate;

        /// <summary>
        /// Chirp cadence scales with the rate so the voice stays at a steady ~10
        /// chirps a second (Slow every 2nd character, Fast every 6th) instead of
        /// machine-gunning at Fast.
        /// </summary>
        public int ChirpInterval =>
            float.IsInfinity(Rate) ? int.MaxValue : Math.Max(1, (int)MathF.Round(ChirpEveryNCharacters * Rate / CharactersPerSecond));

        /// <summary>Selects the reveal rate for the next <see cref="Begin"/>.</summary>
        public void SetSpeed(FTT.Core.TextSpeed speed) => Speed = FTT.Core.TextSpeedRules.Sanitize(speed);

        /// <summary>
        /// Largest slice of time one <see cref="Advance"/> may consume. Anything
        /// beyond this is dropped rather than banked, so a hitch cannot flush a
        /// line and its chirps in a single frame.
        /// </summary>
        public const float MaxAdvanceSeconds = 0.25f;

        private float SecondsPerCharacter => 1f / Rate;

        private string _text = "";
        private float _budgetSeconds;
        private float _pauseRemaining;
        private int _sinceLastChirp;

        /// <summary>Characters revealed so far. Feeds RichTextLabel.VisibleCharacters.</summary>
        public int VisibleCharacters { get; private set; }

        /// <summary>Total characters in the current line.</summary>
        public int TotalCharacters => _text.Length;

        /// <summary>True once every character is on screen (or the line was empty).</summary>
        public bool IsComplete => VisibleCharacters >= _text.Length;

        /// <summary>True while the reveal is holding on a punctuation mark.</summary>
        public bool IsPausedOnPunctuation => _pauseRemaining > 0f && !IsComplete;

        /// <summary>Chirps emitted across the whole line so far; diagnostic/test surface.</summary>
        public int TotalChirps { get; private set; }

        /// <summary>Starts a new line. A null or empty line begins complete.</summary>
        public void Begin(string plainText) {
            _text = plainText ?? "";
            _budgetSeconds = 0f;
            _pauseRemaining = 0f;
            _sinceLastChirp = 0;
            VisibleCharacters = 0;
            TotalChirps = 0;
            // G10 Instant: the whole line lands at once, silently.
            if (Speed == FTT.Core.TextSpeed.Instant) VisibleCharacters = _text.Length;
        }

        /// <summary>
        /// Advances the reveal and returns how many chirps the caller should
        /// play this step (0 while paused on punctuation, or when the line is
        /// already complete).
        /// </summary>
        public int Advance(float deltaSeconds) {
            if (IsComplete || deltaSeconds <= 0f) return 0;
            _budgetSeconds += Math.Min(deltaSeconds, MaxAdvanceSeconds);

            int chirps = 0;
            while (!IsComplete) {
                if (_pauseRemaining > 0f) {
                    float consumed = Math.Min(_pauseRemaining, _budgetSeconds);
                    _pauseRemaining -= consumed;
                    _budgetSeconds -= consumed;
                    if (_pauseRemaining > 0f) break;
                }
                if (_budgetSeconds < SecondsPerCharacter) break;
                _budgetSeconds -= SecondsPerCharacter;

                char revealed = _text[VisibleCharacters];
                VisibleCharacters++;

                if (!char.IsWhiteSpace(revealed)) {
                    _sinceLastChirp++;
                    if (_sinceLastChirp >= ChirpInterval) {
                        _sinceLastChirp = 0;
                        chirps++;
                    }
                }

                // A hold on the final character would only delay the continue
                // hint; there is nothing left to pace. A run of marks holds once,
                // at its end - twenty-two authored lines use "...", and three
                // stacked sentence pauses there would read as a stall, not a beat.
                _pauseRemaining = IsComplete || PauseAfter(_text[VisibleCharacters]) > 0f
                    ? 0f
                    : PauseAfter(revealed) * PauseScale;
            }

            TotalChirps += chirps;
            return chirps;
        }

        /// <summary>
        /// Confirm-to-complete: the whole line lands at once, silently. No
        /// chirps — the player asked to stop listening to the reveal.
        /// </summary>
        public void CompleteImmediately() {
            VisibleCharacters = _text.Length;
            _pauseRemaining = 0f;
            _budgetSeconds = 0f;
            _sinceLastChirp = 0;
        }

        /// <summary>How long the reveal holds after <paramref name="character"/>.</summary>
        public static float PauseAfter(char character) => character switch {
            '.' or '!' or '?' => SentencePauseSeconds,
            ',' or ';' or ':' => ClausePauseSeconds,
            _ => 0f
        };
    }
}
