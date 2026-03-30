using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace SEGlyphScanner
{
    internal class Program
    {
        // (rangeStart, rangeEnd, blockName, flagged)
        // Flagged = worth calling out for SE modding / LCD custom icons
        private static readonly (int Start, int End, string Name, bool Flagged)[] Blocks =
        {
            (0x0000, 0x001F, "C0 Controls",                     false),
            (0x0020, 0x007E, "Basic Latin",                      false),
            (0x007F, 0x009F, "C1 Controls",                      false),
            (0x00A0, 0x00FF, "Latin-1 Supplement",               false),
            (0x0100, 0x017F, "Latin Extended-A",                  false),
            (0x0180, 0x024F, "Latin Extended-B",                  false),
            (0x0250, 0x02AF, "IPA Extensions",                    false),
            (0x02B0, 0x02FF, "Spacing Modifier Letters",          false),
            (0x0300, 0x036F, "Combining Diacritical Marks",       false),
            (0x0370, 0x03FF, "Greek and Coptic",                  false),
            (0x0400, 0x04FF, "Cyrillic",                          false),
            (0x0500, 0x052F, "Cyrillic Supplement",               false),
            (0x1E00, 0x1EFF, "Latin Extended Additional",         false),
            (0x2000, 0x206F, "General Punctuation",               false),
            (0x2070, 0x209F, "Superscripts and Subscripts",       false),
            (0x20A0, 0x20CF, "Currency Symbols",                  false),
            (0x2100, 0x214F, "Letterlike Symbols",                false),
            (0x2190, 0x21FF, "Arrows",                            false),
            (0x2200, 0x22FF, "Mathematical Operators",            false),
            (0x2300, 0x23FF, "Miscellaneous Technical",           false),
            (0x2460, 0x24FF, "Enclosed Alphanumerics",            false),
            (0x2500, 0x257F, "Box Drawing",                       true),  // <<<
            (0x2580, 0x259F, "Block Elements",                    true),  // <<<
            (0x25A0, 0x25FF, "Geometric Shapes",                  true),  // <<<
            (0x2600, 0x26FF, "Miscellaneous Symbols",             false),
            (0x2700, 0x27BF, "Dingbats",                          false),
            (0x3000, 0x303F, "CJK Symbols and Punctuation",       false),
            (0x3040, 0x309F, "Hiragana",                          false),
            (0x30A0, 0x30FF, "Katakana",                          false),
            (0x4E00, 0x9FFF, "CJK Unified Ideographs",            false),
            (0xAC00, 0xD7AF, "Hangul Syllables",                  false),
            (0xE000, 0xE0FF, "Private Use [E000-E0FF]",           true),  // <<<
            (0xE100, 0xE2FF, "Private Use [E100-E2FF]",           true),  // <<<
            (0xE300, 0xF8FF, "Private Use [E300-F8FF]",           true),  // <<<
            (0xFB00, 0xFB4F, "Alphabetic Presentation Forms",     false),
            (0xFF00, 0xFFEF, "Halfwidth and Fullwidth Forms",      false),
        };

        private struct GlyphInfo
        {
            public int    Codepoint;
            public bool   ForceWhite;
            public int    AdvanceWidth;   // -1 = not specified in XML
            public string SourceFile;     // filename only
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string dir = ResolveDirectory(args);
            if (dir == null) return;

            Console.WriteLine();
            Console.WriteLine("Scanning: " + dir);
            Console.WriteLine();

            var fontFiles = FindFontFiles(dir);
            if (fontFiles.Count == 0)
            {
                Console.WriteLine("No .xml or .fnt font files found.");
                return;
            }

            var allGlyphs = new List<GlyphInfo>();
            foreach (var file in fontFiles)
            {
                var found = ParseFontFile(file);
                Console.WriteLine(string.Format("  {0,-38} {1,5} glyphs", Path.GetFileName(file), found.Count));
                allGlyphs.AddRange(found);
            }

            int uniqueCount = allGlyphs.Select(g => g.Codepoint).Distinct().Count();
            Console.WriteLine();
            Console.WriteLine("Total unique codepoints: " + uniqueCount);

            string report = BuildReport(allGlyphs);
            Console.WriteLine(report);

            string outPath = Path.Combine(dir, "glyph_report.txt");
            try
            {
                File.WriteAllText(outPath, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Console.WriteLine("Report saved: " + outPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not save report: " + ex.Message);
            }

            string pbPath = Path.Combine(dir, "glyph_browser_pb.cs");
            try
            {
                File.WriteAllText(pbPath, BuildPbBrowserScript(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Console.WriteLine("PB script saved: " + pbPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not save PB script: " + ex.Message);
            }

            Console.WriteLine();
            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }

        // ── Directory resolution ────────────────────────────────────────────

        private static string ResolveDirectory(string[] args)
        {
            if (args.Length > 0)
            {
                string path = args[0].Trim('"', ' ');
                if (Directory.Exists(path))
                    return path;
                Console.WriteLine("Path not found: " + path);
            }

            string detected = FindSEFontsDirectory();
            if (detected != null)
            {
                Console.WriteLine("Auto-detected SE font dir: " + detected);
                return detected;
            }

            Console.Write("Enter path to SE font directory: ");
            string input = Console.ReadLine()?.Trim('"', ' ');
            if (!string.IsNullOrEmpty(input) && Directory.Exists(input))
                return input;

            Console.WriteLine("Directory not found. Exiting.");
            return null;
        }

        private static string FindSEFontsDirectory()
        {
            var candidates = new List<string>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    string r = drive.RootDirectory.FullName;
                    candidates.Add(Path.Combine(r, @"Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Content\Fonts\white"));
                    candidates.Add(Path.Combine(r, @"Program Files\Steam\steamapps\common\SpaceEngineers\Content\Fonts\white"));
                    candidates.Add(Path.Combine(r, @"Steam\steamapps\common\SpaceEngineers\Content\Fonts\white"));
                    candidates.Add(Path.Combine(r, @"SteamLibrary\steamapps\common\SpaceEngineers\Content\Fonts\white"));
                    candidates.Add(Path.Combine(r, @"Games\Steam\steamapps\common\SpaceEngineers\Content\Fonts\white"));
                    candidates.Add(Path.Combine(r, @"Games\SteamLibrary\steamapps\common\SpaceEngineers\Content\Fonts\white"));
                }
            }
            catch { /* ignore drive enumeration errors */ }

            return candidates.FirstOrDefault(p => Directory.Exists(p));
        }

        // ── File discovery ──────────────────────────────────────────────────

        private static List<string> FindFontFiles(string dir)
        {
            return Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".xml" || ext == ".fnt";
                })
                .OrderBy(f => f)
                .ToList();
        }

        // ── Parsing ─────────────────────────────────────────────────────────

        private static List<GlyphInfo> ParseFontFile(string path)
        {
            var result = new List<GlyphInfo>();
            try
            {
                var doc = new XmlDocument();
                doc.Load(path);

                string fileName = Path.GetFileName(path);

                // SE main format:   <glyph code="e001" … />  (bare hex, no 0x prefix)
                // SE alt format:    <Char  code="0xE100" … /> (0x-prefixed hex)
                // AngelCode BMFont: <char  id="57600" … />    (decimal)
                XmlNodeList nodes = doc.GetElementsByTagName("glyph");
                if (nodes.Count == 0) nodes = doc.GetElementsByTagName("Char");
                if (nodes.Count == 0) nodes = doc.GetElementsByTagName("char");

                foreach (XmlNode node in nodes)
                {
                    string codeVal = node.Attributes?["code"]?.Value;
                    string idVal   = node.Attributes?["id"]?.Value;

                    int cp;
                    if (codeVal != null)
                    {
                        // code is always hex in SE format (bare or 0x-prefixed)
                        if (!TryParseHex(codeVal, out cp) || cp < 0) continue;
                    }
                    else if (idVal != null)
                    {
                        // id is decimal in BMFont format
                        if (!int.TryParse(idVal.Trim(), out cp) || cp < 0) continue;
                    }
                    else continue;

                    int aw = -1;
                    string awVal = node.Attributes?["aw"]?.Value;
                    if (awVal != null) int.TryParse(awVal, out aw);

                    bool forceWhite = false;
                    string fwVal = node.Attributes?["forcewhite"]?.Value;
                    if (fwVal != null)
                        forceWhite = string.Equals(fwVal.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                                  || fwVal.Trim() == "1";

                    result.Add(new GlyphInfo
                    {
                        Codepoint    = cp,
                        ForceWhite   = forceWhite,
                        AdvanceWidth = aw,
                        SourceFile   = fileName
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [WARN] " + Path.GetFileName(path) + ": " + ex.Message);
            }
            return result;
        }

        private static bool TryParseHex(string s, out int value)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            return int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        // ── Report building ─────────────────────────────────────────────────

        private static string BuildReport(List<GlyphInfo> allGlyphs)
        {
            var sorted = allGlyphs.Select(g => g.Codepoint).Distinct().OrderBy(c => c).ToList();

            var sb = new StringBuilder();
            const string sep  = "====================================================================";
            const string thin = "--------------------------------------------------------------------";

            sb.AppendLine(sep);
            sb.AppendLine("  SE GLYPH SCANNER  --  UNICODE RANGE REPORT");
            sb.AppendLine(string.Format("  Total: {0} unique codepoints   ({1:yyyy-MM-dd HH:mm})",
                sorted.Count, DateTime.Now));
            sb.AppendLine(sep);
            sb.AppendLine();

            // ── Summary table ──────────────────────────────────────────────
            sb.AppendLine("BLOCK SUMMARY");
            sb.AppendLine(thin);
            sb.AppendLine(string.Format("  {0,-42} {1,-13} {2,5}  {3}", "Block", "Range", "Count", "Flag"));
            sb.AppendLine(thin);

            foreach (var b in Blocks)
            {
                int count = sorted.Count(c => c >= b.Start && c <= b.End);
                if (count == 0) continue;
                string flag = b.Flagged ? "<<<" : "";
                sb.AppendLine(string.Format("  {0,-42} U+{1:X4}-{2:X4}  {3,5}  {4}",
                    b.Name, b.Start, b.End, count, flag));
            }

            var unclassified = sorted
                .Where(c => Blocks.All(b => c < b.Start || c > b.End))
                .ToList();

            if (unclassified.Count > 0)
                sb.AppendLine(string.Format("  {0,-42} {1,-13} {2,5}",
                    "(Unclassified)", "", unclassified.Count));

            sb.AppendLine(thin);
            sb.AppendLine();

            // ── PUA attribute table ────────────────────────────────────────
            var puaGlyphs = allGlyphs
                .Where(g => g.Codepoint >= 0xE000 && g.Codepoint <= 0xF8FF)
                .GroupBy(g => g.Codepoint)
                .OrderBy(gr => gr.Key)
                .ToList();

            if (puaGlyphs.Count > 0)
            {
                sb.AppendLine("PUA GLYPH ATTRIBUTES  (U+E000 \u2013 U+F8FF)");
                sb.AppendLine(thin);
                sb.AppendLine(string.Format("  {0,-10} {1,4}  {2,-10}  {3}",
                    "Codepoint", " aw", "ForceWhite", "Source(s)"));
                sb.AppendLine(thin);

                foreach (var gr in puaGlyphs)
                {
                    var items = gr.ToList();
                    int aw       = items[0].AdvanceWidth;
                    bool fw      = items.Any(x => x.ForceWhite);
                    string awStr = aw >= 0 ? aw.ToString() : "?";
                    string fwStr = fw ? "yes" : "";
                    string src   = string.Join(", ", items.Select(x => x.SourceFile).Distinct());
                    sb.AppendLine(string.Format("  U+{0:X4}    {1,4}  {2,-10}  {3}",
                        gr.Key, awStr, fwStr, src));
                }

                sb.AppendLine(thin);
                sb.AppendLine();
            }

            // ── Flagged range detail ───────────────────────────────────────
            sb.AppendLine("FLAGGED RANGE DETAIL  (interesting for SE modding / LCD icons)");
            sb.AppendLine(thin);

            bool anyFlagged = false;
            foreach (var b in Blocks.Where(x => x.Flagged))
            {
                var inRange = sorted.Where(c => c >= b.Start && c <= b.End).ToList();
                if (inRange.Count == 0) continue;
                anyFlagged = true;

                sb.AppendLine();
                sb.AppendLine(string.Format("  {0}  (U+{1:X4} - U+{2:X4})  -- {3} glyphs",
                    b.Name, b.Start, b.End, inRange.Count));
                AppendGrid(sb, inRange);
            }

            if (!anyFlagged)
                sb.AppendLine("  (no glyphs in flagged ranges)");

            if (unclassified.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"  Unclassified  -- {unclassified.Count} codepoints");
                AppendGrid(sb, unclassified);
            }

            sb.AppendLine();
            sb.AppendLine(thin);
            sb.AppendLine();

            // ── Full sorted dump ───────────────────────────────────────────
            sb.AppendLine("ALL CODEPOINTS (sorted)");
            sb.AppendLine(thin);
            AppendGrid(sb, sorted);

            sb.AppendLine();
            sb.AppendLine(sep);
            sb.AppendLine("  END OF REPORT");
            sb.AppendLine(sep);

            return sb.ToString();
        }

        private static void AppendGrid(StringBuilder sb, List<int> cps)
        {
            const int cols = 12;
            for (int i = 0; i < cps.Count; i += cols)
            {
                sb.Append("  ");
                sb.AppendLine(string.Join("  ",
                    cps.Skip(i).Take(cols).Select(c => string.Format("U+{0:X4}", c))));
            }
        }

        // ── PB Browser Script ────────────────────────────────────────────────

        private static string BuildPbBrowserScript()
        {
            var sb = new StringBuilder();
            sb.AppendLine("// SE Glyph Browser  \u2014  generated by SEGlyphScanner");
            sb.AppendLine("// Paste into a Space Engineers Programmable Block.");
            sb.AppendLine("// Set LCD_NAME to your text panel block name, then run.");
            sb.AppendLine("//");
            sb.AppendLine("// Run arguments:");
            sb.AppendLine("//   <hex>    jump to codepoint  (e.g. \"E001\")");
            sb.AppendLine("//   +        page forward 32 glyphs");
            sb.AppendLine("//   -        page backward 32 glyphs");
            sb.AppendLine("//   mono     switch to Monospace font (swatches, spacers, E055+)");
            sb.AppendLine("//   white    switch to White font (default LCD, E001-E053)");
            sb.AppendLine();
            sb.AppendLine("const string LCD_NAME = \"LCD Panel\";");
            sb.AppendLine("const int COLS = 8;");
            sb.AppendLine("const int ROWS = 6;");
            sb.AppendLine();
            sb.AppendLine("int _startCp = 0xE001;");
            sb.AppendLine("string _font  = \"White\";");
            sb.AppendLine();
            sb.AppendLine("public void Main(string argument, UpdateType updateSource)");
            sb.AppendLine("{");
            sb.AppendLine("    if (argument == \"+\")");
            sb.AppendLine("        _startCp = Math.Min(_startCp + COLS * ROWS, 0xFFFF);");
            sb.AppendLine("    else if (argument == \"-\")");
            sb.AppendLine("        _startCp = Math.Max(_startCp - COLS * ROWS, 0);");
            sb.AppendLine("    else if (argument == \"mono\")  _font = \"Monospace\";");
            sb.AppendLine("    else if (argument == \"white\") _font = \"White\";");
            sb.AppendLine("    else if (!string.IsNullOrEmpty(argument))");
            sb.AppendLine("    {");
            sb.AppendLine("        int parsed;");
            sb.AppendLine("        if (int.TryParse(argument, System.Globalization.NumberStyles.HexNumber, null, out parsed))");
            sb.AppendLine("            _startCp = parsed;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    var lcd = GridTerminalSystem.GetBlockWithName(LCD_NAME) as IMyTextPanel;");
            sb.AppendLine("    if (lcd == null) { Echo(\"LCD '\" + LCD_NAME + \"' not found.\"); return; }");
            sb.AppendLine();
            sb.AppendLine("    lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;");
            sb.AppendLine("    lcd.Font = _font;");
            sb.AppendLine("    lcd.FontSize = 0.8f;");
            sb.AppendLine();
            sb.AppendLine("    var text = new System.Text.StringBuilder();");
            sb.AppendLine("    text.AppendLine(\"[\" + _font + \"]  U+\" + _startCp.ToString(\"X4\")");
            sb.AppendLine("                  + \" .. U+\" + (_startCp + COLS * ROWS - 1).ToString(\"X4\"));");
            sb.AppendLine("    text.AppendLine();");
            sb.AppendLine();
            sb.AppendLine("    for (int row = 0; row < ROWS; row++)");
            sb.AppendLine("    {");
            sb.AppendLine("        // glyph row");
            sb.AppendLine("        for (int col = 0; col < COLS; col++)");
            sb.AppendLine("            text.Append((char)(_startCp + row * COLS + col) + (col < COLS - 1 ? \" \" : \"\"));");
            sb.AppendLine("        text.AppendLine();");
            sb.AppendLine("        // codepoint row");
            sb.AppendLine("        for (int col = 0; col < COLS; col++)");
            sb.AppendLine("            text.Append((_startCp + row * COLS + col).ToString(\"X4\") + (col < COLS - 1 ? \" \" : \"\"));");
            sb.AppendLine("        text.AppendLine();");
            sb.AppendLine("        text.AppendLine();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    text.AppendLine(\"Args: <hex>=jump  +=fwd  -=back  mono/white=font\");");
            sb.AppendLine("    lcd.WriteText(text.ToString());");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
