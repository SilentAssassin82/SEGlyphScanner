# SEGlyphScanner

A .NET Framework 4.8 console tool that scans Space Engineers font XML files and produces a full Unicode codepoint inventory — with a focus on the Private Use Area glyphs used for LCD icons, colour swatches, and spacers.

## What it does

1. **Auto-detects** your SE font directory across all fixed drives, or accepts a path as a command-line argument.
2. **Parses** every `.xml` / `.fnt` font file it finds (`glyph`, `Char`, `char` elements — SE and AngelCode BMFont formats).
3. **Reports** every codepoint found, grouped by Unicode block, with a flagged summary for ranges useful in SE modding (Box Drawing, Block Elements, Geometric Shapes, and the full PUA).
4. **PUA attribute table** — for every glyph in U+E000–U+F8FF it shows advance width, `forcewhite` status, and which font file(s) define it.
5. **Saves** two files into the scanned font directory:
   - `glyph_report.txt` — the full human-readable report
   - `glyph_browser_pb.cs` — a paste-ready Space Engineers Programmable Block script for browsing glyphs across four panels simultaneously

## Key findings this tool reveals

| Range | White font | Monospace font |
|-------|-----------|----------------|
| U+E001–E030 | Xbox HUD icons (forcewhite, tintable) | Compact variants (partial) |
| U+E031–E033 | PlayStation HUD icons | Stipple density glyphs — E033 confirmed tintable |
| U+E034–E048 | PlayStation HUD icons | Pre-coloured status arrows (baked RGBA, fixed green/cyan/blue/purple) |
| U+E049–E052 | PlayStation HUD icons | Left chevron arrows — E049, E051, E052 confirmed tintable |
| U+E050 | PS stick deflection icon | Single left chevron `<` |
| U+E040–E23F | *(not defined)* | RGB colour swatches (baked RGBA, 512 colours) |
| U+E070–E078 | *(not defined)* | Invisible advance-width spacers (1–255 px) |

> **Critical:** the same codepoint has completely different art in each font atlas. The font you set on the LCD surface determines which art you get.

## Tintability — XML is not the truth

The `forcewhite` attribute in SE's font XML files **does not reliably predict** whether a glyph is tintable at runtime. The game does not parse that attribute (confirmed by Digi). The only reliable test is in-game visual comparison:

1. Set one LCD `FontColor` to a non-white colour (e.g. cyan) and a second to white — same codepoint range, same font on both.
2. If the **glyph pixels** change colour between the two panels → tintable (white alpha mask in DDS).
3. If the glyph stays the same colour on both panels → baked RGBA, not tintable.

> The codepoint label text always renders in the current FontColor regardless — only the glyph pixel colour matters for this test.

The four-panel browser script is designed specifically for this: two tint + two white panels, one pair per font, all updating simultaneously.

### Real-world example

Systematic in-game browsing revealed that several glyphs in the Monospace font atlas carry useful greyscale brightness levels that are not obvious from the XML at all. This led to implementing **11 distinct brightness levels** for a CCTV greyscale rendering plugin. The E034–E048 range contains pre-coloured status arrows that work as fixed-colour indicators without needing to change `FontColor`. There are likely more useful glyphs still to be found by continued browsing.

## SELcdSymbols.cs

`SEGlyphScanner/SELcdSymbols.cs` is a self-contained constants + helpers file you can copy into your own Torch plugin or mod project (adjust the namespace). It provides:

- Named constants for confirmed glyphs (`DrainIcon`, `FillIcon`, `IdeographicSpace`, `Spacer0`–`Spacer255`)
- `Spacer(int pixels)` — greedy decomposition into the minimum number of spacer glyphs for an exact pixel offset
- `ColorSwatch(byte r, byte g, byte b)` — returns the Monospace swatch character for quantised 8-bit RGB
- `ColorBar(byte r, byte g, byte b, int count)` — returns a solid colour bar string
- `Tag(byte r, byte g, byte b)` — returns an inline `<color=R,G,B>` tag for SE LCD text
- `TagWhite` — inline colour reset constant (255,255,255)
- `IdeographicSpace` (U+3000) — full-width CJK space that works in both White and Monospace fonts

> **Note on colour tags:** `<color=R,G,B>` inline tags are **not supported by vanilla SE text panels**. They appeared to work in earlier testing only because AutoLCD 2 was running and processing them in its own pipeline. In vanilla SE (PB or Torch plugin writing directly to `IMyTextPanel`), colour control is limited to `lcd.FontColor` (whole panel, single colour), baked-RGBA swatch glyphs, or `ContentType.SCRIPT` sprite drawing.

## In-game glyph browser

The generated `glyph_browser_pb.cs` script runs on a Programmable Block and displays 48 glyphs per page (8 cols × 6 rows at 0.8× font size) across **four panels simultaneously**. Font and FontColor are set by the script automatically — no manual panel setup needed.

| Panel name | Font | FontColor | Purpose |
|------------|------|-----------|---------|
| `LCD Mono Tint` | Monospace | Cyan | Tintable glyphs change colour |
| `LCD Mono White` | Monospace | White | Baked colours shown as-is |
| `LCD WF Tint` | White font | Cyan | Tintable glyphs change colour |
| `LCD WF White` | White font | White | Baseline / baked as-is |

**Run arguments:**

| Argument | Action |
|----------|--------|
| `+` | Page forward 48 glyphs |
| `-` | Page backward 48 glyphs |
| `E040` *(any hex)* | Jump to that codepoint |

## Requirements

- .NET Framework 4.8
- Space Engineers installed (for the font files to scan)

## Building

Open `SEGlyphScanner.slnx` in Visual Studio and build. No NuGet packages — BCL only.

## License

MIT

