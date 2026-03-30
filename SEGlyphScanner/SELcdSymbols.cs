using System;
using System.Text;

namespace SEGlyphScanner
{
    /// <summary>
    /// Confirmed Space Engineers LCD glyph constants and helpers.
    /// Copy this file to your Torch plugin / mod project and adjust the namespace.
    ///
    /// FONT CONTEXT — each IMyTextSurface has ONE font; choose deliberately:
    ///   White font    — default LCD; Xbox HUD icons (E001–E030), PS HUD icons (E031–E053)
    ///   Monospace font — colour swatches (E040–E23F), spacers (E070–E078), chevron arrows (E050–E058)
    ///
    /// CRITICAL — E050 / E051 / E052 render DIFFERENTLY depending on font:
    ///   White font    : E050 = PS S1– stick icon   E051 = PS S2– icon   E052 = PS S3– icon
    ///   Monospace font: E050 = left chevron <   E051 = left chevron variant   E052 = left chevron variant
    ///   → DrainIcon / FillIcon MUST be used on a Monospace-font surface to appear as chevrons.
    ///   → On a White-font surface they will render as PlayStation stick deflection icons.
    ///
    /// forcewhite glyphs (all E001–E053) are stored as a white alpha mask and tinted by the
    /// LCD text colour — fully tintable, no baked colour.
    /// Non-forcewhite glyphs (swatches E040–E23F) have baked RGBA art and are NOT tintable.
    ///
    /// Multi-surface blocks (Wide LCD = 3 surfaces, Corner LCD = 3 surfaces) let you mix
    /// font contexts: e.g. surface 0 = White for HUD icons, surface 1 = Monospace for swatches.
    /// </summary>
    internal static class SELcdSymbols
    {
        // ── Confirmed arrows / indicators (White font, forcewhite → tintable) ────
        // In-game confirmed via screenshots:
        public const char DrainIcon = '\uE050';   // <  left chevron
        public const char FillIcon  = '\uE051';   // <  left chevron variant

        // E001–E04F: HUD / chevron icons in White font (forcewhite, aw ≈ 60–64)
        // E050–E058: smaller arrow glyphs also present in Monospace font

        // ── Inline colour tags (all fonts) ───────────────────────────────────────
        // SE LCD renderer supports <color=R,G,B> markup inline in text strings.
        // The tag applies from its position forward until the next tag.
        // Works on both White and Monospace fonts; tintable glyphs pick up the active tag colour.
        // To restore the LCD's FontColor, emit Tag(lcd.FontColor.R, lcd.FontColor.G, lcd.FontColor.B).

        /// <summary>Returns an inline colour tag string: <c>&lt;color=R,G,B&gt;</c>.</summary>
        public static string Tag(byte r, byte g, byte b)
        {
            return "<color=" + r + "," + g + "," + b + ">";
        }

        /// <summary>Resets inline colour to white (255,255,255).</summary>
        public const string TagWhite = "<color=255,255,255>";

        // ── Universal spacers (all fonts) ────────────────────────────────────────
        // U+3000 Ideographic Space — CJK full-width blank, wider than ASCII space.
        // Works in both White and Monospace font; confirmed blank on all font/colour combos.
        public const char IdeographicSpace = '\u3000';

        // ── Precision spacers (Monospace font ONLY) ──────────────────────────────
        // Invisible glyphs with fixed advance widths of 0, 1, 3, 7, 15, 31, 63, 127, 255 px.
        // Combine them (greedy / binary decomposition) for any pixel offset 0–∞.
        public const char Spacer0   = '\uE070';   //   0 px  (zero-width, no-op)
        public const char Spacer1   = '\uE071';   //   1 px
        public const char Spacer3   = '\uE072';   //   3 px
        public const char Spacer7   = '\uE073';   //   7 px
        public const char Spacer15  = '\uE074';   //  15 px
        public const char Spacer31  = '\uE075';   //  31 px
        public const char Spacer63  = '\uE076';   //  63 px
        public const char Spacer127 = '\uE077';   // 127 px
        public const char Spacer255 = '\uE078';   // 255 px

        /// <summary>
        /// Returns a string of spacer glyphs (Monospace font only) that advance
        /// exactly <paramref name="pixels"/> pixels using a greedy decomposition.
        /// </summary>
        public static string Spacer(int pixels)
        {
            if (pixels <= 0) return string.Empty;
            var sb = new StringBuilder(9);
            int rem = pixels;
            // greedy: largest spacer first
            while (rem >= 255) { sb.Append(Spacer255); rem -= 255; }
            if (rem >= 127)    { sb.Append(Spacer127); rem -= 127; }
            if (rem >= 63)     { sb.Append(Spacer63);  rem -= 63; }
            if (rem >= 31)     { sb.Append(Spacer31);  rem -= 31; }
            if (rem >= 15)     { sb.Append(Spacer15);  rem -= 15; }
            if (rem >= 7)      { sb.Append(Spacer7);   rem -= 7; }
            if (rem >= 3)      { sb.Append(Spacer3);   rem -= 3; }
            while (rem >= 1)   { sb.Append(Spacer1);   rem -= 1; }
            return sb.ToString();
        }

        // ── RGB colour swatches (Monospace font ONLY) ────────────────────────────
        // Range: U+E040 – U+E23F  (512 colours, 30×42 px each, aw=24)
        // Formula: codepoint = 0xE040 + (R₃ << 6) + (G₃ << 3) + B₃
        // Each channel is quantised to 3 bits (0–7 steps via nearest rounding).

        /// <summary>
        /// Returns the RGB swatch glyph codepoint for the given 8-bit colour channels
        /// (Monospace font only). Channels are quantised to 3-bit (0–7 steps).
        /// </summary>
        public static char ColorSwatch(byte r, byte g, byte b)
        {
            int r3 = (int)Math.Round(r / (255.0 / 7.0));
            int g3 = (int)Math.Round(g / (255.0 / 7.0));
            int b3 = (int)Math.Round(b / (255.0 / 7.0));
            return (char)(0xE040 + (r3 << 6) + (g3 << 3) + b3);
        }

        /// <summary>
        /// Returns a colour bar string of <paramref name="count"/> swatch glyphs,
        /// all the same colour. Use with Monospace font.
        /// </summary>
        public static string ColorBar(byte r, byte g, byte b, int count)
        {
            return new string(ColorSwatch(r, g, b), Math.Max(0, count));
        }
    }
}
