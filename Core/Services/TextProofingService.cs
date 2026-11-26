using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Markup;
using SmartNotes.Core.Models;

namespace SmartNotes.Core.Services;

public record ProofingLanguageOption(string Code, string DisplayName);

public class TextProofingService
{
    private static readonly Lazy<TextProofingService> _instance = new(() => new TextProofingService());
    public static TextProofingService Instance => _instance.Value;

    private static readonly string AppFolder = AppPaths.DataFolder;

    public static readonly string CustomRulesPath = Path.Combine(AppFolder, "autocorrect_rules.json");

    public static readonly IReadOnlyList<ProofingLanguageOption> SupportedLanguages = new List<ProofingLanguageOption>
    {
        new("auto", "Auto-Detect (System Language)"),
        new("de-DE", "German (Deutsch)"),
        new("en-US", "English (United States)"),
        new("en-GB", "English (United Kingdom)"),
        new("es-ES", "Spanish (Español)"),
        new("fr-FR", "French (Français)"),
        new("it-IT", "Italian (Italiano)"),
        new("pt-PT", "Portuguese (Português)"),
        new("nl-NL", "Dutch (Nederlands)"),
        new("pl-PL", "Polish (Polski)"),
        new("sv-SE", "Swedish (Svenska)"),
        new("en-CA", "English (Canada)"),
        new("en-AU", "English (Australia)")
    };

    private readonly Dictionary<string, string> _builtInTypos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _builtInTyposDe = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _smartSymbols = new(StringComparer.Ordinal);
    private readonly List<AutocorrectRule> _customRules = new();

    public TextProofingService()
    {
        InitializeBuiltInDictionaries();
        LoadCustomRules();
    }

    private void LoadCustomRules()
    {
        try
        {
            if (File.Exists(CustomRulesPath))
            {
                string json = File.ReadAllText(CustomRulesPath);
                var loaded = JsonSerializer.Deserialize<List<AutocorrectRule>>(json);
                if (loaded != null)
                {
                    _customRules.Clear();
                    _customRules.AddRange(loaded);
                }
            }
        }
        catch { }
    }

    public void SaveCustomRules(List<AutocorrectRule> rules)
    {
        try
        {
            _customRules.Clear();
            _customRules.AddRange(rules);
            Directory.CreateDirectory(AppFolder);
            string json = JsonSerializer.Serialize(_customRules, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CustomRulesPath, json);
        }
        catch { }
    }

    public IReadOnlyList<AutocorrectRule> GetCustomRules() => _customRules.AsReadOnly();

    private static HashSet<string>? _cachedSupportedLangs;

    public static HashSet<string> GetInstalledSpellCheckLanguages()
    {
        if (_cachedSupportedLangs != null) return _cachedSupportedLangs;

        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var asm = typeof(TextBox).Assembly;
            var factoryType = asm.GetType("System.Windows.Documents.MsSpellCheckLib.SpellCheckerFactory");
            var singletonProp = factoryType?.GetProperty("Singleton", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var singleton = singletonProp?.GetValue(null);
            var comFactoryProp = singleton?.GetType().GetProperty("ComFactory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var comFactory = comFactoryProp?.GetValue(singleton);
            var suppLangProp = comFactory?.GetType().GetProperty("SupportedLanguages");
            var suppLangObj = suppLangProp?.GetValue(comFactory);

            if (suppLangObj is System.Runtime.InteropServices.ComTypes.IEnumString enumString)
            {
                string[] items = new string[1];
                while (enumString.Next(1, items, IntPtr.Zero) == 0)
                {
                    if (!string.IsNullOrEmpty(items[0]))
                    {
                        supported.Add(items[0]);
                    }
                }
            }
        }
        catch { }

        _cachedSupportedLangs = supported;
        return supported;
    }

    public static bool IsLanguageSupported(string langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode) || langCode.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            langCode = System.Globalization.CultureInfo.CurrentUICulture.IetfLanguageTag;
        }

        var installed = GetInstalledSpellCheckLanguages();
        if (installed.Count == 0) return true; // If COM check not available, assume true

        if (installed.Contains(langCode)) return true;

        // Check language prefix (e.g., 'en' for 'en-US' or 'de' for 'de-DE')
        string prefix = langCode.Split('-')[0];
        return installed.Any(l => l.Equals(prefix, StringComparison.OrdinalIgnoreCase) || l.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase));
    }

    // Spell check only uses dictionaries that are installed in Windows. The app used to add the proofing language
    // to the user's Windows language list in the background (PowerShell Set-WinUserLanguageList), which also adds
    // a keyboard layout without asking. Settings > Text Proofing shows whether a dictionary is installed instead.

    /// <summary>The proofing language to use: "auto" (or nothing) means the Windows display language.</summary>
    public static string ResolveLanguage(string? langCode) =>
        string.IsNullOrWhiteSpace(langCode) || langCode.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? System.Globalization.CultureInfo.CurrentUICulture.IetfLanguageTag
            : langCode;

    public void ApplyProofingToTextBox(TextBox textBox, AppSettings settings)
    {
        if (textBox == null) return;

        try
        {
            textBox.SpellCheck.IsEnabled = false;

            string langCode = ResolveLanguage(settings.ProofingLanguage);
            try
            {
                textBox.Language = XmlLanguage.GetLanguage(langCode);
            }
            catch
            {
                textBox.Language = XmlLanguage.GetLanguage("en-US");
            }

            // No SpellCheck.CustomDictionaries (that is why "Add to Dictionary" was removed): WPF registers a temporary
            // copy with the Windows spell checker for every text box and never unregisters it on .NET (no finalizers
            // at exit), so each run would leave registry entries and temp files behind.
            if (settings.EnableSpellCheck)
            {
                textBox.SpellCheck.IsEnabled = true;
                if (langCode.StartsWith("de", StringComparison.OrdinalIgnoreCase))
                {
                    textBox.SpellCheck.SpellingReform = SpellingReform.PreAndPostreform;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Applying spell check settings", ex);
        }
    }

    public bool TryAutocorrect(TextBox textBox, char delimiter, AppSettings settings, out AutocorrectUndoItem? undoItem)
    {
        undoItem = null;
        // A locked note's text boxes are read-only: autocorrect must not rewrite them either
        if (textBox == null || textBox.IsReadOnly || string.IsNullOrEmpty(textBox.Text)) return false;

        int caret = textBox.SelectionStart;
        if (caret <= 0 || caret > textBox.Text.Length) return false;

        string fullText = textBox.Text;

        // Extract the token immediately preceding the caret
        int tokenEnd = caret;
        int tokenStart = tokenEnd - 1;

        // Walk backwards to find the start of the token
        while (tokenStart >= 0 && !char.IsWhiteSpace(fullText[tokenStart]))
        {
            tokenStart--;
        }
        tokenStart++; // Move to first character of token

        if (tokenStart >= tokenEnd) return false;

        string token = fullText.Substring(tokenStart, tokenEnd - tokenStart);
        if (string.IsNullOrEmpty(token)) return false;

        string? replacement = null;

        // 1. Check Smart Symbols
        if (settings.SmartSymbolReplacements)
        {
            if (_smartSymbols.TryGetValue(token, out var sym))
            {
                replacement = sym;
            }
        }

        // 2. Check Custom Autocorrect Rules
        if (replacement == null && settings.CustomReplacementsEnabled && _customRules.Count > 0)
        {
            var rule = _customRules.FirstOrDefault(r => r.IsEnabled && string.Equals(r.Shortcut, token, StringComparison.OrdinalIgnoreCase));
            if (rule != null)
            {
                replacement = MatchCasing(token, rule.Replacement);
            }
        }

        // 3. Check Built-in Common Typos: one list per language, only for text in that language
        //    (the English list turned the everyday German word "im" into "I'm")
        if (replacement == null && settings.EnableAutocorrect)
        {
            string lang = ResolveLanguage(settings.ProofingLanguage);
            var typos = lang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? _builtInTypos
                : lang.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? _builtInTyposDe
                : null;
            if (typos != null && typos.TryGetValue(token, out var typoFix))
            {
                replacement = MatchCasing(token, typoFix);
            }
        }

        // 4. Standalone 'i' to 'I'
        if (replacement == null && settings.AutoCapitalizeSentences)
        {
            if (token == "i")
            {
                replacement = "I";
            }
        }

        // 5. Auto-Capitalize Sentence Start (if first word after start or sentence terminator)
        if (replacement == null && settings.AutoCapitalizeSentences && token.Length > 0 && char.IsLower(token[0]))
        {
            if (IsSentenceStart(fullText, tokenStart))
            {
                replacement = char.ToUpperInvariant(token[0]) + (token.Length > 1 ? token.Substring(1) : "");
            }
        }

        // If a valid replacement was determined
        if (!string.IsNullOrEmpty(replacement) && replacement != token)
        {
            ReplaceRange(textBox, tokenStart, token.Length, replacement);
            undoItem = new AutocorrectUndoItem(tokenStart, token, replacement);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Replaces just that word through the selection, which keeps the text box's undo history
    /// (assigning Text wiped Ctrl+Z after every correction) and leaves the caret right after it.
    /// </summary>
    private static void ReplaceRange(TextBox textBox, int start, int length, string replacement)
    {
        textBox.Select(start, length);
        textBox.SelectedText = replacement;
        textBox.Select(start + replacement.Length, 0);
    }

    private static bool IsSentenceStart(string text, int tokenStart)
    {
        if (tokenStart == 0) return true;

        // Check characters before tokenStart
        int idx = tokenStart - 1;
        while (idx >= 0 && char.IsWhiteSpace(text[idx]))
        {
            if (text[idx] == '\n' || text[idx] == '\r')
            {
                return true; // Start of new line/paragraph
            }
            idx--;
        }

        if (idx < 0) return true;

        char prevChar = text[idx];
        return prevChar == '.' || prevChar == '!' || prevChar == '?';
    }

    public static bool TryUndoAutocorrect(TextBox textBox, AutocorrectUndoItem? lastUndo)
    {
        if (textBox == null || lastUndo == null) return false;

        // Only allow undo if recent (< 8 seconds) and text matches
        if ((DateTime.UtcNow - lastUndo.Timestamp).TotalSeconds > 8.0) return false;

        int start = lastUndo.StartIndex;
        int repLen = lastUndo.Replacement.Length;

        if (start >= 0 && start + repLen <= textBox.Text.Length)
        {
            string currentSegment = textBox.Text.Substring(start, repLen);
            if (currentSegment == lastUndo.Replacement && !textBox.IsReadOnly)
            {
                ReplaceRange(textBox, start, repLen, lastUndo.OriginalWord);
                return true;
            }
        }

        return false;
    }

    public static string MatchCasing(string original, string replacement)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(replacement)) return replacement;

        // All Uppercase (e.g. TEH -> THE)
        if (original.Length > 1 && original.All(char.IsUpper))
        {
            return replacement.ToUpperInvariant();
        }

        // Title Case / Capitalized (e.g. Teh -> The)
        if (char.IsUpper(original[0]))
        {
            return char.ToUpperInvariant(replacement[0]) + (replacement.Length > 1 ? replacement.Substring(1) : "");
        }

        return replacement;
    }

    private void InitializeBuiltInDictionaries()
    {
        // 1. Smart Symbols & Typography
        _smartSymbols["->"] = "→";
        _smartSymbols["<-"] = "←";
        _smartSymbols["=>"] = "⇒";
        _smartSymbols["<="] = "≤";
        _smartSymbols[">="] = "≥";
        _smartSymbols["!="] = "≠";
        _smartSymbols["--"] = "—";
        _smartSymbols["---"] = "—";
        _smartSymbols["(c)"] = "©";
        _smartSymbols["(C)"] = "©";
        _smartSymbols["(r)"] = "®";
        _smartSymbols["(R)"] = "®";
        _smartSymbols["(tm)"] = "™";
        _smartSymbols["(TM)"] = "™";
        _smartSymbols["+-"] = "±";
        _smartSymbols["..."] = "…";
        _smartSymbols["1/2"] = "½";
        _smartSymbols["1/4"] = "¼";
        _smartSymbols["3/4"] = "¾";

        // 2. Common English Typos & Misspellings
        var typos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Frequent typos
            ["teh"] = "the",
            ["adn"] = "and",
            ["taht"] = "that",
            ["waht"] = "what",
            ["wiht"] = "with",
            ["thier"] = "their",
            ["thre"] = "there",
            ["hte"] = "the",
            ["ahve"] = "have",
            ["haev"] = "have",
            ["hvae"] = "have",
            ["recieve"] = "receive",
            ["recieved"] = "received",
            ["recieving"] = "receiving",
            ["seperate"] = "separate",
            ["seperated"] = "separated",
            ["definately"] = "definitely",
            ["definate"] = "definite",
            ["untill"] = "until",
            ["occured"] = "occurred",
            ["occurence"] = "occurrence",
            ["truely"] = "truly",
            ["goverment"] = "government",
            ["acheive"] = "achieve",
            ["acheived"] = "achieved",
            ["acheivement"] = "achievement",
            ["alot"] = "a lot",
            ["beleive"] = "believe",
            ["beleived"] = "believed",
            ["wierd"] = "weird",
            ["embarass"] = "embarrass",
            ["embarassed"] = "embarrassed",
            ["neccessary"] = "necessary",
            ["tommorow"] = "tomorrow",
            ["tommorrow"] = "tomorrow",
            ["calender"] = "calendar",
            ["accomodate"] = "accommodate",
            ["accomodation"] = "accommodation",
            ["becuase"] = "because",
            ["comitted"] = "committed",
            ["experiance"] = "experience",
            ["foriegn"] = "foreign",
            ["gaurantee"] = "guarantee",
            ["happend"] = "happened",
            ["heigt"] = "height",
            ["immediatly"] = "immediately",
            ["judgement"] = "judgment",
            ["liason"] = "liaison",
            ["maintainance"] = "maintenance",
            ["noticable"] = "noticeable",
            ["peice"] = "piece",
            ["possession"] = "possession",
            ["priviledge"] = "privilege",
            ["recommand"] = "recommend",
            ["recomended"] = "recommended",
            ["rythm"] = "rhythm",
            ["schedule"] = "schedule",
            ["successfull"] = "successful",
            ["supercede"] = "supersede",
            ["tendancy"] = "tendency",
            ["threshold"] = "threshold",
            ["twelfth"] = "twelfth",
            ["tyrany"] = "tyranny",
            ["unforseen"] = "unforeseen",
            ["vaccum"] = "vacuum",
            ["veiw"] = "view",
            ["yeild"] = "yield",
            ["realy"] = "really",
            ["allready"] = "already",
            ["allmost"] = "almost",
            ["allways"] = "always",
            ["appartment"] = "apartment",
            ["availible"] = "available",
            ["beggining"] = "beginning",
            ["buisness"] = "business",
            ["collegue"] = "colleague",
            ["concious"] = "conscious",
            ["enviroment"] = "environment",
            ["familar"] = "familiar",
            ["foward"] = "forward",
            ["greatful"] = "grateful",
            ["garantee"] = "guarantee",
            ["interupt"] = "interrupt",
            ["knowlege"] = "knowledge",
            ["millenium"] = "millennium",
            ["neccessity"] = "necessity",
            ["oppurtunity"] = "opportunity",
            ["parallel"] = "parallel",
            ["persue"] = "pursue",
            ["possession"] = "possession",
            ["prefered"] = "preferred",
            ["relevent"] = "relevant",
            ["resistence"] = "resistance",
            ["restaraunt"] = "restaurant",
            ["seige"] = "siege",
            ["similiar"] = "similar",
            ["speach"] = "speech",
            ["suprise"] = "surprise",
            ["tomorow"] = "tomorrow",
            ["tounge"] = "tongue",
            ["unfortunatly"] = "unfortunately",
            ["wierd"] = "weird",
            ["writting"] = "writing",
            ["yesturday"] = "yesterday",

            // Contractions
            ["dont"] = "don't",
            ["cant"] = "can't",
            ["wont"] = "won't",
            ["isnt"] = "isn't",
            ["arent"] = "aren't",
            ["wasnt"] = "wasn't",
            ["werent"] = "weren't",
            ["hasnt"] = "hasn't",
            ["havent"] = "haven't",
            ["hadnt"] = "hadn't",
            ["didnt"] = "didn't",
            ["couldnt"] = "couldn't",
            ["shouldnt"] = "shouldn't",
            ["wouldnt"] = "wouldn't",
            ["thats"] = "that's",
            ["whats"] = "what's",
            ["theres"] = "there's",
            ["heres"] = "here's",
            ["wheres"] = "where's",
            ["hows"] = "how's",
            ["im"] = "I'm",
            ["youre"] = "you're",
            ["theyre"] = "they're",
            ["weve"] = "we've",
            ["youve"] = "you've",
            ["theyve"] = "they've",
            ["youll"] = "you'll",
            ["theyll"] = "they'll",
            ["ill"] = "I'll",
            ["id"] = "I'd"
        };

        foreach (var kvp in typos)
        {
            _builtInTypos[kvp.Key] = kvp.Value;
        }

        // 3. Common German typos. Only words that are not real German or English words, so mixed text stays
        //    untouched. Nouns are written capitalised: MatchCasing never lower-cases a replacement.
        var typosDe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nciht"] = "nicht",
            ["nicth"] = "nicht",
            ["ncith"] = "nicht",
            ["udn"] = "und",
            ["ihc"] = "ich",
            ["cih"] = "ich",
            ["dsa"] = "das",
            ["dei"] = "die",
            ["uach"] = "auch",
            ["acuh"] = "auch",
            ["wiel"] = "weil",
            ["wnen"] = "wenn",
            ["eien"] = "eine",
            ["enie"] = "eine",
            ["eienn"] = "einen",
            ["hbae"] = "habe",
            ["ahbe"] = "habe",
            ["hbaen"] = "haben",
            ["vieleicht"] = "vielleicht",
            ["vielelicht"] = "vielleicht",
            ["warscheinlich"] = "wahrscheinlich",
            ["nähmlich"] = "nämlich",
            ["seperat"] = "separat",
            ["garnicht"] = "gar nicht",
            ["vorraus"] = "voraus",
            ["dannach"] = "danach",
            ["zumindestens"] = "zumindest",
            ["gegebenfalls"] = "gegebenenfalls",
            ["entgültig"] = "endgültig",
            ["agressiv"] = "aggressiv",
            ["interressant"] = "interessant",
            ["intressant"] = "interessant",
            ["reperieren"] = "reparieren",
            ["standart"] = "Standard",
            ["rythmus"] = "Rhythmus",
            ["addresse"] = "Adresse",
            ["maschiene"] = "Maschine",
            ["ergebniss"] = "Ergebnis",
            ["reperatur"] = "Reparatur",
            ["vorraussetzung"] = "Voraussetzung",
            ["rückgrad"] = "Rückgrat",
            ["interresse"] = "Interesse",
            ["fahrad"] = "Fahrrad",
        };
        foreach (var kvp in typosDe)
        {
            _builtInTyposDe[kvp.Key] = kvp.Value;
        }
    }
}
