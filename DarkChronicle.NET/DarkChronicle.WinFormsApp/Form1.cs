namespace DarkChronicle.WinFormsApp;

using System;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();

        foreach (var item in Enum.GetValues<ComboI>())
        {
            comboBox1.Items.Add(item);
        }

        this.Load += (_, __) =>
        {
            if (CodePagesEncodingProvider.Instance.GetEncoding(932) is null)
            {
                MessageBox.Show("The encoding 'Shift_JIS' 932 is required to read text files.\nIt appears your system does not support the 'Shift_JIS' encoding.", "Unsupported Shift_JIS Encoding", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            LoadSettings();
            if (File.Exists(settings.LastFile))
            {
                LoadFile(settings.LastFile);
            }
        };

        Filter();
    }

    Settings settings = new();
    private void LoadSettings()
    {
        try
        {
            if (File.Exists("settings.json"))
            {
                settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText("settings.json")) ?? new();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettings()
    {
        try
        {
            File.WriteAllText("settings.json", JsonSerializer.Serialize(settings, new JsonSerializerOptions()
            {
                IgnoreReadOnlyFields = true,
                WriteIndented = true
            }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    DarkChronicleReader? darkChronicleReader;
    private void LoadFile(string file)
    {
        try
        {
            if (darkChronicleReader is not null)
            {
                darkChronicleReader.Dispose();
                darkChronicleReader = null;
            }

            darkChronicleReader = DarkChronicleReader.OpenISOFile(file);

            // var f0 = darkChronicleReader.GetPIC(DarkChronicleLang.IT);
            // var f1 = darkChronicleReader.LoadSCOOP(DarkChronicleLang.IT);
            // var f2 = darkChronicleReader.LoadITEM(DarkChronicleLang.IT);

            Filter();

            settings.LastFile = file;
            SaveSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "File not supported", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Filter()
    {
        var filter = FilterTextBox.Text?.Replace(" ", "");

        if (string.IsNullOrEmpty(filter))
        {
            DataListBox.BeginUpdate();
            DataListBox.Items.Clear();
            if (darkChronicleReader is not null)
            {
                foreach (var data in darkChronicleReader.Files.Values)
                {
                    DataListBox.Items.Add(data);
                }
            }
            DataListBox.EndUpdate();
        }
        else
        {
            DataListBox.BeginUpdate();
            DataListBox.Items.Clear();
            if (darkChronicleReader is not null)
            {
                foreach (var data in darkChronicleReader.Files.Values)
                {
                    if (FuzzyMatcher.FuzzyMatch(data.FileName, filter))
                    {
                        DataListBox.Items.Add(data);
                    }
                }
            }
            DataListBox.EndUpdate();
        }
    }

    private void FiltetTextBox_TextChanged(object sender, EventArgs e)
    {
        Filter();
    }

    private void DataListBox_SelectedValueChanged(object sender, EventArgs e)
    {
        var item = DataListBox.SelectedItem;

        string fileName;
        if (item is string itemString)
        {
            fileName = itemString;
        }
        else if (item is DataInfo data)
        {
            fileName = data.FileName;
        }
        else
        {
            return;
        }

        if (fileName.EndsWith(".lst") || fileName.EndsWith(".cfg"))
        {
            textBox1.Text = string.Empty;

            using (var sr = new StreamReader(darkChronicleReader?.OpenFile(fileName), CodePagesEncodingProvider.Instance.GetEncoding(932)))
            {
                // TODO: normalize text for printing, \r\n are ok but \n must be converted to \r\n or space
                textBox1.Text = StringUtils.Unescape(sr.ReadToEnd());
            }
        }
        else
        {
            textBox1.Text = string.Empty;

            using (var sr = new StreamReader(darkChronicleReader?.OpenFile(fileName), CodePagesEncodingProvider.Instance.GetEncoding(932)))
            {
                // TODO: normalize text for printing, \r\n are ok but \n must be converted to \r\n or space
                textBox1.Text = sr.ReadToEnd();
            }
        }
    }

    private void Form1_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data is not null && e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Link;
        else
            e.Effect = DragDropEffects.None;
    }

    private void Form1_DragDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
        {
            var file = files.FirstOrDefault();
            if (file is not null)
            {
                LoadFile(file);
            }
        }
    }

    private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
    {
        var val = (ComboI)comboBox1.SelectedItem;
        switch (val)
        {
            case ComboI.All:
                Filter();
                break;

            case ComboI.Pics:
                {
                    string[] keys = new[] { // lang
                        @"menu\0\neta2.lst",
                        @"menu\1\neta2.lst",
                        @"menu\2\neta2.lst",
                        @"menu\3\neta2.lst",
                        @"menu\4\neta2.lst",
                        @"menu\5\neta2.lst",
                    };

                    DataListBox.BeginUpdate();
                    DataListBox.Items.Clear();
                    DataListBox.Items.AddRange(keys);
                    DataListBox.EndUpdate();
                }
                break;

            case ComboI.Items:
                {
                    string[] keys = new[] { // lang, 6 is some lookup value
                        @"menu\cfg7\comdatmes0.cfg",
                        @"menu\cfg7\comdatmes1.cfg",
                        @"menu\cfg7\comdatmes2.cfg",
                        @"menu\cfg7\comdatmes3.cfg",
                        @"menu\cfg7\comdatmes4.cfg",
                        @"menu\cfg7\comdatmes5.cfg",
                        @"menu\cfg7\comdatmes6.cfg",
                    };

                    DataListBox.BeginUpdate();
                    DataListBox.Items.Clear();
                    DataListBox.Items.AddRange(keys);
                    DataListBox.EndUpdate();
                }
                break;

            case ComboI.ScoopHelp:
                {
                    string[] keys = new[] { // lang
                        @"menu\0\scoop.cfg",
                        @"menu\1\scoop.cfg",
                        @"menu\2\scoop.cfg",
                        @"menu\3\scoop.cfg",
                        @"menu\4\scoop.cfg",
                        @"menu\5\scoop.cfg",
                    };

                    DataListBox.BeginUpdate();
                    DataListBox.Items.Clear();
                    DataListBox.Items.AddRange(keys);
                    DataListBox.EndUpdate();
                }
                break;

            case ComboI.MapLocations:
                {
                    string[] keys = new[] { // lang, 6 is some lookup value
                        @"map\map0.cfg",
                        @"map\map1.cfg",
                        @"map\map2.cfg",
                        @"map\map3.cfg",
                        @"map\map4.cfg",
                        @"map\map5.cfg",
                        @"map\map6.cfg",
                    };

                    DataListBox.BeginUpdate();
                    DataListBox.Items.Clear();
                    DataListBox.Items.AddRange(keys);
                    DataListBox.EndUpdate();
                }
                break;

            case ComboI.ShopItems:
                {
                    string[] keys = new[] { // each num is a chapter
                        @"menu\shoplst1.cfg",
                        @"menu\shoplst2.cfg",
                        @"menu\shoplst3.cfg",
                        @"menu\shoplst4.cfg",
                        @"menu\shoplst5.cfg",
                        @"menu\shoplst6.cfg",
                        @"menu\shoplst7.cfg",
                        @"menu\shoplst8.cfg",
                    };

                    DataListBox.BeginUpdate();
                    DataListBox.Items.Clear();
                    DataListBox.Items.AddRange(keys);
                    DataListBox.EndUpdate();
                }
                break;

            case ComboI.GeoramaConditions:
                {
                    string[] keys = new[] { // lang, 6 is some lookup value
                        @"geo0.cfg",
                        @"geo1.cfg",
                        @"geo2.cfg",
                        @"geo3.cfg",
                        @"geo4.cfg",
                        @"geo5.cfg",
                        @"geo6.cfg",
                    };

                    DataListBox.BeginUpdate();
                    DataListBox.Items.Clear();
                    DataListBox.Items.AddRange(keys);
                    DataListBox.EndUpdate();
                }
                break;

            // DATASET {ITEM ID},
            // PIC 1,
            // PIC 2,
            // PIC 3
            // ITEM ID, QUANTITY,
            // ITEM ID, QUANTITY,
            // ITEM ID, QUANTITY,

            // an example from the game files
            // DATASET 309, // MES_SYS 309,"Aquarium";
            // 4,       // PIC_NAME 4,"Fountain",526;
            // 11,      // PIC_NAME 11,"Window",2218;
            // 12,      // PIC_NAME 12,"Wooden Box",2221;
            // 210,     // MES_SYS 210,"Rolling Log";
            // 5,       // 5 items are required to build
            // 216,     // MES_SYS 216,"Glass Material";
            // 4,       // 4 items are required to build
            // 231,     // MES_SYS 231,"Water Element";
            // 15,      // 15 items are required to build
            // 0,0.99,-10.4,0.72,0;

            // DATASET 309,4,11,12,210,5,216,4,231,15,0,0.99,-10.4,0.72,0;

            case ComboI.Inventions:
                {
                    string[] keys = new[] { // lang, 6 is some lookup value
                        @"menu\inv6.lst",
                        @"menu\0\inv6.lst", // duplicates
                        @"menu\1\inv6.lst", // duplicates
                        @"menu\2\inv6.lst", // duplicates
                        @"menu\3\inv6.lst", // duplicates
                        @"menu\4\inv6.lst", // duplicates
                        @"menu\5\inv6.lst", // duplicates
                        @"menu\6\inv6.lst", // duplicates
                    };

                    DataListBox.BeginUpdate();
                    DataListBox.Items.Clear();
                    DataListBox.Items.AddRange(keys);
                    DataListBox.EndUpdate();
                }
                break;

        }
    }

    public enum ComboI
    {
        All,
        Pics,
        Items,
        ScoopHelp,
        MapLocations,
        ShopItems,
        GeoramaConditions,
        Inventions,
    }
}

public class Settings
{
    public string? LastFile { get; set; }
}

internal static class FuzzyMatcher
{
    /// <summary>
    /// Does a fuzzy search for a pattern within a string.
    /// </summary>
    /// <param name="stringToSearch">The string to search for the pattern in.</param>
    /// <param name="pattern">The pattern to search for in the string.</param>
    /// <returns>true if each character in pattern is found sequentially within stringToSearch; otherwise, false.</returns>
    public static bool FuzzyMatch(string stringToSearch, string pattern)
    {
        var patternIdx = 0;
        var strIdx = 0;
        var patternLength = pattern.Length;
        var strLength = stringToSearch.Length;

        while (patternIdx != patternLength && strIdx != strLength)
        {
            if (char.ToLower(pattern[patternIdx]) == char.ToLower(stringToSearch[strIdx]))
                ++patternIdx;
            ++strIdx;
        }

        return patternLength != 0 && strLength != 0 && patternIdx == patternLength;
    }

    /// <summary>
    /// Does a fuzzy search for a pattern within a string, and gives the search a score on how well it matched.
    /// </summary>
    /// <param name="stringToSearch">The string to search for the pattern in.</param>
    /// <param name="pattern">The pattern to search for in the string.</param>
    /// <param name="outScore">The score which this search received, if a match was found.</param>
    /// <returns>true if each character in pattern is found sequentially within stringToSearch; otherwise, false.</returns>
    public static bool FuzzyMatch(string stringToSearch, string pattern, out int outScore)
    {
        // Score consts
        const int adjacencyBonus = 5;               // bonus for adjacent matches
        const int separatorBonus = 10;              // bonus if match occurs after a separator
        const int camelBonus = 10;                  // bonus if match is uppercase and prev is lower

        const int leadingLetterPenalty = -3;        // penalty applied for every letter in stringToSearch before the first match
        const int maxLeadingLetterPenalty = -9;     // maximum penalty for leading letters
        const int unmatchedLetterPenalty = -1;      // penalty for every letter that doesn't matter


        // Loop variables
        var score = 0;
        var patternIdx = 0;
        var patternLength = pattern.Length;
        var strIdx = 0;
        var strLength = stringToSearch.Length;
        var prevMatched = false;
        var prevLower = false;
        var prevSeparator = true;                   // true if first letter match gets separator bonus

        // Use "best" matched letter if multiple string letters match the pattern
        char? bestLetter = null;
        char? bestLower = null;
        int? bestLetterIdx = null;
        var bestLetterScore = 0;

        var matchedIndices = new List<int>();

        // Loop over strings
        while (strIdx != strLength)
        {
            var patternChar = patternIdx != patternLength ? pattern[patternIdx] as char? : null;
            var strChar = stringToSearch[strIdx];

            var patternLower = patternChar != null ? char.ToLower((char)patternChar) as char? : null;
            var strLower = char.ToLower(strChar);
            var strUpper = char.ToUpper(strChar);

            var nextMatch = patternChar != null && patternLower == strLower;
            var rematch = bestLetter != null && bestLower == strLower;

            var advanced = nextMatch && bestLetter != null;
            var patternRepeat = bestLetter != null && patternChar != null && bestLower == patternLower;
            if (advanced || patternRepeat)
            {
                score += bestLetterScore;
                matchedIndices.Add((int)bestLetterIdx);
                bestLetter = null;
                bestLower = null;
                bestLetterIdx = null;
                bestLetterScore = 0;
            }

            if (nextMatch || rematch)
            {
                var newScore = 0;

                // Apply penalty for each letter before the first pattern match
                // Note: Math.Max because penalties are negative values. So max is smallest penalty.
                if (patternIdx == 0)
                {
                    var penalty = Math.Max(strIdx * leadingLetterPenalty, maxLeadingLetterPenalty);
                    score += penalty;
                }

                // Apply bonus for consecutive bonuses
                if (prevMatched)
                    newScore += adjacencyBonus;

                // Apply bonus for matches after a separator
                if (prevSeparator)
                    newScore += separatorBonus;

                // Apply bonus across camel case boundaries. Includes "clever" isLetter check.
                if (prevLower && strChar == strUpper && strLower != strUpper)
                    newScore += camelBonus;

                // Update pattern index IF the next pattern letter was matched
                if (nextMatch)
                    ++patternIdx;

                // Update best letter in stringToSearch which may be for a "next" letter or a "rematch"
                if (newScore >= bestLetterScore)
                {
                    // Apply penalty for now skipped letter
                    if (bestLetter != null)
                        score += unmatchedLetterPenalty;

                    bestLetter = strChar;
                    bestLower = char.ToLower((char)bestLetter);
                    bestLetterIdx = strIdx;
                    bestLetterScore = newScore;
                }

                prevMatched = true;
            }
            else
            {
                score += unmatchedLetterPenalty;
                prevMatched = false;
            }

            // Includes "clever" isLetter check.
            prevLower = strChar == strLower && strLower != strUpper;
            prevSeparator = strChar == '_' || strChar == ' ';

            ++strIdx;
        }

        // Apply score for last match
        if (bestLetter != null)
        {
            score += bestLetterScore;
            matchedIndices.Add((int)bestLetterIdx);
        }

        outScore = score;
        return patternIdx == patternLength;
    }
}
