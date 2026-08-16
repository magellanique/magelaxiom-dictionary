using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal static class OfflineDataBuilder
{
    private static readonly Regex ExampleRegex = new Regex("\"([^\"]+)\"", RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        try
        {
            var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
            var raw = Path.Combine(root, "data", "raw");
            var generated = Path.Combine(root, "data", "generated");

            Directory.CreateDirectory(generated);

            var synsets = new Dictionary<string, Synset>(StringComparer.OrdinalIgnoreCase);
            LoadWordNet(Path.Combine(raw, "oewn", "oewn2025", "data.noun"), "n", synsets);
            LoadWordNet(Path.Combine(raw, "oewn", "oewn2025", "data.verb"), "v", synsets);
            LoadWordNet(Path.Combine(raw, "oewn", "oewn2025", "data.adj"), "a", synsets);
            LoadWordNet(Path.Combine(raw, "oewn", "oewn2025", "data.adv"), "r", synsets);
            ResolveAntonyms(synsets);

            var dictionaryRows = BuildDictionaryRows(synsets);
            AddManualDictionaryRows(dictionaryRows);
            dictionaryRows.Sort(DictionaryRow.Compare);
            WriteDictionary(Path.Combine(generated, "dictionary.tsv"), dictionaryRows);
            WriteDictionaryBinary(Path.Combine(generated, "dictionary.bin"), dictionaryRows);

            Console.WriteLine("Wrote " + dictionaryRows.Count.ToString(CultureInfo.InvariantCulture) + " dictionary rows.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static void LoadWordNet(string path, string expectedPos, Dictionary<string, Synset> synsets)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Missing Open English WordNet WNDB file.", path);
        }

        foreach (var rawLine in File.ReadLines(path, Encoding.UTF8))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0 || line.StartsWith("  ", StringComparison.Ordinal))
            {
                continue;
            }

            var pipe = line.IndexOf('|');
            if (pipe < 0)
            {
                continue;
            }

            var dataPart = line.Substring(0, pipe).Trim();
            var gloss = line.Substring(pipe + 1).Trim();
            var tokens = SplitFields(dataPart);
            if (tokens.Length < 5)
            {
                continue;
            }

            var offset = tokens[0];
            var pos = tokens[2];
            if (!StringComparer.OrdinalIgnoreCase.Equals(pos, expectedPos) && !(expectedPos == "a" && pos == "s"))
            {
                continue;
            }

            int wordCount;
            if (!int.TryParse(tokens[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out wordCount))
            {
                continue;
            }

            var index = 4;
            var words = new List<string>();
            for (var i = 0; i < wordCount && index + 1 < tokens.Length; i++)
            {
                words.Add(CleanTerm(tokens[index]));
                index += 2;
            }

            if (words.Count == 0 || index >= tokens.Length)
            {
                continue;
            }

            int pointerCount;
            if (!int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out pointerCount))
            {
                continue;
            }

            index++;
            var antonymTargets = new List<string>();
            for (var i = 0; i < pointerCount && index + 3 < tokens.Length; i++)
            {
                var symbol = tokens[index];
                var targetOffset = tokens[index + 1];
                var targetPos = tokens[index + 2];
                index += 4;

                if (symbol == "!")
                {
                    antonymTargets.Add(MakeSynsetId(targetOffset, targetPos));
                }
            }

            var synset = new Synset();
            synset.Id = MakeSynsetId(offset, pos);
            synset.Pos = PosLabel(pos);
            synset.Definition = ExtractDefinition(gloss);
            synset.Words.AddRange(words);
            synset.AntonymTargetIds.AddRange(antonymTargets);
            synset.Examples.AddRange(ExtractExamples(gloss));
            synsets[synset.Id] = synset;
        }
    }

    private static string[] SplitFields(string value)
    {
        return value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string MakeSynsetId(string offset, string pos)
    {
        return offset + "-" + pos;
    }

    private static string PosLabel(string pos)
    {
        if (pos == "n") return "noun";
        if (pos == "v") return "verb";
        if (pos == "a" || pos == "s") return "adjective";
        if (pos == "r") return "adverb";
        return pos;
    }

    private static string CleanTerm(string value)
    {
        return Normalize(value.Replace('_', ' '));
    }

    private static string ExtractDefinition(string gloss)
    {
        var quote = gloss.IndexOf('"');
        var definition = quote >= 0 ? gloss.Substring(0, quote) : gloss;
        definition = definition.Trim();
        while (definition.EndsWith(";", StringComparison.Ordinal))
        {
            definition = definition.Substring(0, definition.Length - 1).Trim();
        }

        return definition;
    }

    private static List<string> ExtractExamples(string gloss)
    {
        var result = new List<string>();
        foreach (Match match in ExampleRegex.Matches(gloss))
        {
            AddUnique(result, match.Groups[1].Value.Trim());
        }

        return result;
    }

    private static void ResolveAntonyms(Dictionary<string, Synset> synsets)
    {
        foreach (var pair in synsets)
        {
            var synset = pair.Value;
            foreach (var targetId in synset.AntonymTargetIds)
            {
                Synset target;
                if (!synsets.TryGetValue(targetId, out target))
                {
                    continue;
                }

                foreach (var word in target.Words)
                {
                    AddUnique(synset.Antonyms, word);
                }
            }
        }
    }

    private static List<DictionaryRow> BuildDictionaryRows(Dictionary<string, Synset> synsets)
    {
        var rows = new List<DictionaryRow>();
        foreach (var pair in synsets)
        {
            var synset = pair.Value;
            foreach (var word in synset.Words)
            {
                var synonyms = new List<string>();
                foreach (var other in synset.Words)
                {
                    if (!StringComparer.OrdinalIgnoreCase.Equals(word, other))
                    {
                        AddUnique(synonyms, other);
                    }
                }

                rows.Add(new DictionaryRow(
                    word,
                    synset.Pos,
                    synset.Definition,
                    synonyms,
                    synset.Antonyms,
                    synset.Examples));
            }
        }

        return rows;
    }

    private static void AddManualDictionaryRows(List<DictionaryRow> rows)
    {
        rows.Add(new DictionaryRow(
            "the buck stops here",
            "phrase",
            "A statement that no excuses will be made and that responsibility rests with the person in charge.",
            new List<string>(),
            new List<string>(),
            new List<string> { "In this office, the buck stops here." }));

        rows.Add(new DictionaryRow(
            "ex ante",
            "adjective",
            "Predicted, forecast, or evaluated before the event.",
            new List<string> { "beforehand", "prospective" },
            new List<string> { "ex post" },
            new List<string>()));

        rows.Add(new DictionaryRow(
            "ex post",
            "adjective",
            "Based on knowledge, analysis, or evaluation after the event.",
            new List<string> { "retrospective", "after the fact" },
            new List<string> { "ex ante" },
            new List<string>()));

        rows.Add(new DictionaryRow(
            "a priori",
            "adjective",
            "Known, assumed, or reasoned from theory before observation or experience.",
            new List<string> { "deductive", "theoretical" },
            new List<string> { "a posteriori" },
            new List<string>()));

        rows.Add(new DictionaryRow(
            "a posteriori",
            "adjective",
            "Based on observation, evidence, or experience rather than prior theory.",
            new List<string> { "empirical", "inductive" },
            new List<string> { "a priori" },
            new List<string>()));

        rows.Add(new DictionaryRow(
            "de facto",
            "adjective",
            "Existing in fact, whether or not officially recognized by law.",
            new List<string> { "actual", "in fact" },
            new List<string> { "de jure" },
            new List<string>()));

        rows.Add(new DictionaryRow(
            "de jure",
            "adjective",
            "Existing by law, official right, or legal recognition.",
            new List<string> { "lawful", "official" },
            new List<string> { "de facto" },
            new List<string>()));

        rows.Add(new DictionaryRow(
            "ad hoc",
            "adjective",
            "Created or done for a particular purpose as needed.",
            new List<string> { "improvised", "purpose-built" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "prima facie",
            "adjective",
            "Accepted as correct on first appearance until disproved.",
            new List<string> { "at first sight", "apparent" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "quid pro quo",
            "noun",
            "A thing given, done, or expected in exchange for something else.",
            new List<string> { "exchange", "trade-off" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "status quo",
            "noun",
            "The existing state of affairs.",
            new List<string> { "current state", "existing order" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "per se",
            "adverb",
            "By itself; in itself; intrinsically.",
            new List<string> { "intrinsically", "as such" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "vice versa",
            "adverb",
            "With the order or relation reversed; the other way around.",
            new List<string> { "conversely", "the other way around" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "sine qua non",
            "noun",
            "An indispensable condition or essential requirement.",
            new List<string> { "prerequisite", "essential condition" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "in situ",
            "adverb",
            "In the original, natural, or proper place.",
            new List<string> { "on site", "in place" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "bona fide",
            "adjective",
            "Genuine, authentic, or made in good faith.",
            new List<string> { "genuine", "authentic" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "ipso facto",
            "adverb",
            "By that very fact or action.",
            new List<string> { "thereby", "by that fact" },
            new List<string>(),
            new List<string>()));

        rows.Add(new DictionaryRow(
            "ceteris paribus",
            "adverb",
            "All other things being equal; with other relevant factors held constant.",
            new List<string> { "all else equal" },
            new List<string>(),
            new List<string>()));
    }

    private static void WriteDictionary(string path, List<DictionaryRow> rows)
    {
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("# term\tpos\tdefinition\tsynonyms\tantonyms\texamples");
            foreach (var row in rows)
            {
                writer.WriteLine(ToDictionaryLine(row));
            }
        }
    }

    private static void WriteDictionaryBinary(string path, List<DictionaryRow> rows)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            AddBinaryLine(map, row.Term, ToDictionaryLine(row));
        }

        WriteBinaryLexicon(path, map);
    }

    private static void AddBinaryLine(Dictionary<string, List<string>> map, string term, string line)
    {
        term = Normalize(term);
        if (term.Length == 0)
        {
            return;
        }

        List<string> lines;
        if (!map.TryGetValue(term, out lines))
        {
            lines = new List<string>();
            map.Add(term, lines);
        }

        lines.Add(line);
    }

    private static string ToDictionaryLine(DictionaryRow row)
    {
        return Escape(row.Term) + "\t" +
            Escape(row.Pos) + "\t" +
            Escape(row.Definition) + "\t" +
            Escape(string.Join(", ", row.Synonyms.ToArray())) + "\t" +
            Escape(string.Join(", ", row.Antonyms.ToArray())) + "\t" +
            Escape(string.Join(" | ", row.Examples.ToArray()));
    }

    private static void WriteBinaryLexicon(string path, Dictionary<string, List<string>> map)
    {
        var records = new List<BinaryRecord>();
        foreach (var pair in map)
        {
            var record = new BinaryRecord();
            record.Key = Normalize(pair.Key);
            record.Payload = string.Join("\n", pair.Value.ToArray());
            records.Add(record);
        }

        records.Sort(BinaryRecord.CompareByKey);

        var looseMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < records.Count; index++)
        {
            var loose = LooseKey(records[index].Key);
            if (loose.Length > 0 && !looseMap.ContainsKey(loose))
            {
                looseMap.Add(loose, index);
            }
        }

        var looseRecords = new List<LooseRecord>();
        foreach (var pair in looseMap)
        {
            var record = new LooseRecord();
            record.Key = pair.Key;
            record.RecordIndex = pair.Value;
            looseRecords.Add(record);
        }

        looseRecords.Sort(LooseRecord.CompareByKey);

        using (var blob = new MemoryStream())
        {
            foreach (var record in records)
            {
                record.KeyOffset = AddBlob(blob, record.Key);
                record.KeyLength = Encoding.UTF8.GetByteCount(record.Key);
                record.PayloadOffset = AddBlob(blob, record.Payload);
                record.PayloadLength = Encoding.UTF8.GetByteCount(record.Payload);
            }

            foreach (var record in looseRecords)
            {
                record.KeyOffset = AddBlob(blob, record.Key);
                record.KeyLength = Encoding.UTF8.GetByteCount(record.Key);
            }

            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(output, Encoding.UTF8))
            {
                writer.Write(0x58444c4f);
                writer.Write(1);
                writer.Write(records.Count);
                writer.Write(looseRecords.Count);

                foreach (var record in records)
                {
                    writer.Write(record.KeyOffset);
                    writer.Write(record.KeyLength);
                    writer.Write(record.PayloadOffset);
                    writer.Write(record.PayloadLength);
                }

                foreach (var record in looseRecords)
                {
                    writer.Write(record.KeyOffset);
                    writer.Write(record.KeyLength);
                    writer.Write(record.RecordIndex);
                }

                writer.Write(blob.ToArray());
            }
        }
    }

    private static int AddBlob(MemoryStream blob, string value)
    {
        var offset = checked((int)blob.Position);
        var bytes = Encoding.UTF8.GetBytes(value);
        blob.Write(bytes, 0, bytes.Length);
        return offset;
    }

    private static string Normalize(string value)
    {
        if (value == null)
        {
            return "";
        }

        value = value.Trim().ToLowerInvariant();
        value = value.Replace('_', ' ');
        value = value.Replace('\u2010', '-')
            .Replace('\u2011', '-')
            .Replace('\u2012', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-');
        value = Regex.Replace(value, "\\s+", " ");
        return value;
    }

    private static string LooseKey(string value)
    {
        value = Normalize(value);
        var builder = new StringBuilder();
        foreach (var ch in value)
        {
            if (ch == '-' || ch == ' ' || ch == '\'' || ch == '.' || ch == '/')
            {
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (value == null)
        {
            return "";
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\t", "\\t")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static void AddUnique(List<string> target, string value)
    {
        value = value == null ? "" : value.Trim();
        if (value.Length == 0)
        {
            return;
        }

        foreach (var existing in target)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(existing, value))
            {
                return;
            }
        }

        target.Add(value);
    }

    private sealed class Synset
    {
        public Synset()
        {
            Words = new List<string>();
            AntonymTargetIds = new List<string>();
            Antonyms = new List<string>();
            Examples = new List<string>();
        }

        public string Id;
        public string Pos;
        public string Definition;
        public List<string> Words;
        public List<string> AntonymTargetIds;
        public List<string> Antonyms;
        public List<string> Examples;
    }

    private sealed class DictionaryRow
    {
        public DictionaryRow(string term, string pos, string definition, List<string> synonyms, List<string> antonyms, List<string> examples)
        {
            Term = term;
            Pos = pos;
            Definition = definition;
            Synonyms = new List<string>(synonyms);
            Antonyms = new List<string>(antonyms);
            Examples = new List<string>(examples);
        }

        public string Term;
        public string Pos;
        public string Definition;
        public List<string> Synonyms;
        public List<string> Antonyms;
        public List<string> Examples;

        public static int Compare(DictionaryRow left, DictionaryRow right)
        {
            var result = StringComparer.OrdinalIgnoreCase.Compare(left.Term, right.Term);
            if (result != 0) return result;
            result = StringComparer.OrdinalIgnoreCase.Compare(left.Pos, right.Pos);
            if (result != 0) return result;
            return StringComparer.OrdinalIgnoreCase.Compare(left.Definition, right.Definition);
        }
    }

    private sealed class BinaryRecord
    {
        public string Key;
        public string Payload;
        public int KeyOffset;
        public int KeyLength;
        public int PayloadOffset;
        public int PayloadLength;

        public static int CompareByKey(BinaryRecord left, BinaryRecord right)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key);
        }
    }

    private sealed class LooseRecord
    {
        public string Key;
        public int KeyOffset;
        public int KeyLength;
        public int RecordIndex;

        public static int CompareByKey(LooseRecord left, LooseRecord right)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key);
        }
    }
}
