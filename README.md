# SEGlyphScanner

A .NET Framework 4.8 console tool that scans Space Engineers font XML files and produces a full Unicode codepoint inventory — with a focus on the Private Use Area glyphs used for LCD icons, colour swatches, and spacers.

## What it does

1. **Auto-detects** your SE font directory across all fixed drives, or accepts a path as a command-line argument.
2. **Parses** every `.xml` / `.fnt` font file it finds (`glyph`, `Char`, `char` elements — SE and AngelCode BMFont formats).
3. **Reports** every codepoint found, grouped by Unicode block, with a flagged summary for ranges useful in SE modding (Box Drawing, Block Elements, Geometric Shapes, and the full PUA).
4. **PUA attribute table** — for every glyph in U+E000–U+F8FF it shows advance width, `forcewhite` status, and which font file(s) define it.
5. **Saves** two files into the scanned font directory:
   - `glyph_report.txt` — the full human-readable report
   - `glyph_browser_pb.cs` — a paste-ready Space Engineers Programmable Block script for browsing glyphs in-game

## Key findings this tool reveals

| Range | White font | Monospace font |
|-------|-----------|----------------|
| U+E001–E030 | Xbox HUD icons (forcewhite, tintable) | Compact variants (partial) |
| U+E031–E053 | PlayStation HUD icons (forcewhite, tintable) | Stipple density glyphs E031–E033 (forcewhite, tintable) |
| U+E040–E23F | *(not defined)* | RGB colour swatches (baked RGBA, 512 colours) |
| U+E050–E052 | PS stick deflection icons | `<<<` `<<` `<` chevron arrows |
| U+E070–E078 | *(not defined)* | Invisible advance-width spacers (1–255 px) |

> **Critical:** the same codepoint has completely different art in each font atlas. The font you set on the LCD surface determines which art you get.

## SELcdSymbols.cs

`SEGlyphScanner/SELcdSymbols.cs` is a self-contained constants + helpers file you can copy into your own Torch plugin or mod project (change the namespace). It provides:

- Named constants for confirmed glyphs (`DrainIcon`, `FillIcon`, `Spacer0`–`Spacer255`)
- `Spacer(int pixels)` — greedy decomposition into the minimum number of spacer glyphs
- `ColorSwatch(int r3, int g3, int b3)` — returns the Monospace swatch character for a 3-bit-per-channel RGB value
- `ColorBar(int r3, int g3, int b3, int count)` — returns a solid colour bar string

## In-game glyph browser

The generated `glyph_browser_pb.cs` script runs on a Programmable Block and displays 48 glyphs per page (8 cols × 6 rows at 0.8× font size) on a standard LCD panel.

**Run arguments:**

| Argument | Action |
|----------|--------|
| `+` | Page forward 48 glyphs |
| `-` | Page backward 48 glyphs |
| `E040` *(any hex)* | Jump to codepoint |
| `mono` | Switch to Monospace font |
| `white` | Switch to White font |

Set `LCD_NAME` at the top of the script to match your text panel's block name.

## Requirements

- .NET Framework 4.8
- Space Engineers installed (for the font files to scan)

## Building

Open `SEGlyphScanner.slnx` in Visual Studio and build. No NuGet packages — BCL only.

## License

MIT
