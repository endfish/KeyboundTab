using KeyboundTab.Core;
using KeyboundTab.Diagnostics;
using KeyboundTab.Native;

var passed = 0;
void Check(string name, bool condition)
{
    if (!condition)
        throw new InvalidOperationException("FAIL: " + name);
    passed++;
}

WordCandidate[] words = [new("蒂法", "DiFa"), new("神罗", "ShenLuo"), new("神罗战士", "ShenLuoZhanShi")];
var engine = new CompletionEngine();
Check("empty snapshot preserves input", engine.Select("typo", [], "one", new IndexRandom(0)) == null);
Check("empty input random", engine.Select("", words, "one", new IndexRandom(1)) == words[1]);
Check("single prefix match", engine.Select("Di", words, "one", new IndexRandom(2)) == words[0]);
Check("case insensitive prefix", engine.Select("di", words, "one", new IndexRandom(2)) == words[0]);
Check("multiple prefixes random", engine.Select("Shen", words, "one", new IndexRandom(1)) == words[2]);
Check("unmatched replaces", engine.Select("mistake", words, "one", new IndexRandom(2)) == words[2]);
Check("fully manual text preserves prefix", engine.Select("DiFa", words, "one", new IndexRandom(2)) == words[0]);
engine.ConfirmWrite("DiFa", "one");
Check("repeat shortcut rerolls", engine.Select("DiFa", words, "one", new IndexRandom(1)) == words[2]);
engine.Observe("Di", "one");
Check("edited text resumes matching", engine.Select("Di", words, "one", new IndexRandom(2)) == words[0]);
engine.ConfirmWrite("DiFa", "one");
Check("new input addon cannot inherit reroll", engine.Select("DiFa", words, "two", new IndexRandom(2)) == words[0]);
engine.Reset();
Check("reset clears reroll", engine.Select("DiFa", words, "one", new IndexRandom(2)) == words[0]);
Check("stale target cannot be selected", engine.Select("Di", [words[1]], "one", new IndexRandom(0)) == words[1]);
Check("blank candidates rejected", engine.Select("", [new("", ""), new("x", " ")], "one", new IndexRandom(0)) == null);
Check("duplicate strings do not gain extra weight", engine.Select("", [words[0], words[0], words[1]], "one", new IndexRandom(1)) == words[1]);
for (var i = 0; i < words.Length; i++)
    Check("each distinct candidate selectable " + i, engine.Select("", words, "one", new IndexRandom(i)) == words[i]);

var gate = new PressGate();
Check("load while held does not fire", !gate.Update(true, true));
Check("release does not fire", !gate.Update(false, true));
Check("new press fires once", gate.Update(true, true));
Check("held key does not repeat", !gate.Update(true, true));
Check("focus loss does not fire", !gate.Update(true, false));
Check("focus gain while held does not fire", !gate.Update(true, true));
gate.Update(false, false);
Check("ineligible press does not fire", !gate.Update(true, false));
Check("adding modifier while held does not fire", !gate.Update(true, true));
gate.Update(false, true);
Check("fresh eligible press works", gate.Update(true, true));
gate.Reset();
Check("binding change while held does not fire", !gate.Update(true, true));

Check("plain Tab valid", new ShortcutBinding(9, false, false, false).IsValid);
Check("F8 valid", new ShortcutBinding(0x77, false, false, false).IsValid);
Check("Ctrl+Space valid", new ShortcutBinding(0x20, true, false, false).IsValid);
Check("Alt+Tab reserved", !new ShortcutBinding(9, false, true, false).IsValid);
Check("Alt+F4 reserved", !new ShortcutBinding(0x73, false, true, false).IsValid);
Check("Enter cannot be bound", !new ShortcutBinding(13, false, false, false).IsValid);
Check("Escape cannot be bound", !new ShortcutBinding(27, false, false, false).IsValid);
Check("mouse cannot be bound", !new ShortcutBinding(1, false, false, false).IsValid);
Check("extra Ctrl does not match plain Tab", !new ShortcutBinding(9, false, false, false).MatchesModifiers(true, false, false, false));
Check("Windows combinations pass through", !new ShortcutBinding(9, false, false, false).MatchesModifiers(false, false, false, true));

var catalog = new WordCatalog(words);
Check("catalog accepts exact pair", catalog.Match("蒂法", "DIFA") != null);
Check("catalog rejects mismatched pair", catalog.Match("蒂法", "ShenLuo") == null);
Check("catalog does not fuzzy match display", catalog.Match("蒂", "DiFa") == null);
AddonReport Word(bool visible, bool textVisible, string display, string input) => new("KTGEWord", 9, visible, false,
    [new NodeReport(2, "1000", true, null, [new NodeReport(20, "Text", textVisible, display, null), new NodeReport(22, "Text", true, input, null)])]);
ProbeReport Report(params AddonReport[] addons) => new("test", new InputReport(true, true, true, true, "", ""), addons);
Check("only current displayed word included", CandidateReader.Read(Report(Word(true, true, "蒂法", "DiFa")), catalog).Length == 1);
Check("hidden addon rejected", CandidateReader.Read(Report(Word(false, true, "蒂法", "DiFa")), catalog).Length == 0);
Check("hidden word rejected", CandidateReader.Read(Report(Word(true, false, "蒂法", "DiFa")), catalog).Length == 0);
Check("CN mode uses Chinese display, not a hidden reading", CandidateReader.Read(Report(Word(true, true, "蒂法", "ShenLuo")), catalog).Single().Input == "蒂法");
Check("all multiple addon instances read", CandidateReader.Read(Report(Word(true, true, "蒂法", "DiFa"), Word(true, true, "神罗", "ShenLuo")), catalog).Length == 2);
Check("duplicate word instances deduplicated", CandidateReader.Read(Report(Word(true, true, "蒂法", "DiFa"), Word(true, true, "蒂法", "DiFa")), catalog).Length == 1);
Check("closed word snapshot empty", CandidateReader.Read(Report(), catalog).Length == 0);
Check("unknown display is rejected", CandidateReader.Read(Report(Word(true, true, "不在词库", "DiFa")), catalog).Length == 0);
var duplicateComponents = new AddonReport("KTGEWord", 9, true, true,
[
    new NodeReport(2, "1001", false, null, [new NodeReport(20, "Text", true, "蒂法", null)]),
    new NodeReport(20001, "1001", true, null, [new NodeReport(20, "Text", true, "神罗", null)]),
    new NodeReport(20007, "1001", true, null, [new NodeReport(20, "Text", true, "神罗战士", null)]),
]);
Check("all dynamic duplicated components included", CandidateReader.Read(Report(duplicateComponents), catalog).Length == 2);
Check("hidden template does not supply a candidate", CandidateReader.Read(Report(duplicateComponents), catalog).All(w => w.Input != "蒂法"));
var chineseWords = words.Select(w => new WordCandidate(w.Display, w.Display)).ToArray();
Check("committed Chinese prefix matches", engine.Select("神罗", chineseWords, "three", new IndexRandom(1))!.Input == "神罗战士");
Check("misspelled Chinese prefix falls back to random", engine.Select("神螺", chineseWords, "three", new IndexRandom(0))!.Input == "蒂法");
var punctuated = new WordCandidate("肮脏的鼠辈，欢迎你们！", "肮脏的鼠辈，欢迎你们！");
Check("Chinese punctuation preserved", engine.Select("肮脏", [punctuated], "four", new IndexRandom(0))!.Input == punctuated.Input);
Check("runtime zero character limit accepts Chinese", InputLimits.Fits(6, 2, 96, 0));
Check("byte limit is enforced independently", !InputLimits.Fits(97, 33, 96, 0));
Check("nonzero character limit is enforced", !InputLimits.Fits(9, 3, 96, 2));
Check("unknown zero byte limit stays closed", !InputLimits.Fits(6, 2, 0, 0));
var urgent = new WordCandidate("神罗", "神罗", 1);
Check("casting target gets its weighted ticket", engine.Select("", [chineseWords[0], urgent], "priority", new IndexRandom(1)) == urgent);
Check("ordinary word stays selectable beside a sustained cast", engine.Select("", [chineseWords[0], urgent], "priority", new IndexRandom(0)) == chineseWords[0]);
Check("manual prefix keeps user's current target", engine.Select("蒂", [chineseWords[0], urgent], "priority", new IndexRandom(0)) == chineseWords[0]);
Check("unmatched prefix uses weighted full pool", engine.Select("错字", [chineseWords[0], urgent], "priority", new IndexRandom(0)) == chineseWords[0]);
Check("priority ties remain random", engine.Select("", [urgent, urgent with { Display = "蒂法", Input = "蒂法" }], "priority", new IndexRandom(1))!.Input == "蒂法");
Check("duplicate input keeps highest threat", engine.Select("", [chineseWords[0], chineseWords[0] with { Priority = 2 }, urgent], "priority", new IndexRandom(1))!.Input == "蒂法");
var castData = new int[13];
castData[7] = 2000;
var enemy = new EnemyReport(123, "神罗", "ShenLuo", "技能", castData);
var castReport = Report(duplicateComponents) with { Enemies = [enemy] };
Check("shared cast record maps to matching visible word", CandidateReader.Read(castReport, catalog).Single(w => w.Input == "神罗").Priority == 1);
Check("casting priority can be turned off", CandidateReader.Read(castReport, catalog, false).All(w => w.Priority == 0));
Check("cast belonging to an absent word adds no candidate", CandidateReader.Read(castReport with { Enemies = [enemy with { Word = "不存在" }] }, catalog).All(w => w.Priority == 0));
Check("expired cast has no priority", !(enemy with { Data = new int[13] }).IsCasting);
Check("unnamed cast has no priority", !(enemy with { Skill = "" }).IsCasting);
Check("invalid target has no priority", !(enemy with { Id = 0xE0000000 }).IsCasting);
var tickets = Enumerable.Range(0, 3).Select(ticket => new CompletionEngine().Select("", [chineseWords[0], urgent], "lottery", new IndexRandom(ticket))!).ToArray();
Check("ordinary word retains one ticket", tickets.Count(w => w.Priority == 0) == 1);
Check("casting word has two tickets", tickets.Count(w => w.Priority == 1) == 2);
var previews = new CompletionEngine();
var protectedWord = new WordCandidate("绿墙保护词条", "绿墙保护词条");
var bossWord = new WordCandidate("Boss读条词条", "Boss读条词条", 1);
WordCandidate[] encounter = [protectedWord, bossWord, chineseWords[0]];
var shown = new HashSet<string>(StringComparer.Ordinal);
var current = "";
for (var i = 0; i < encounter.Length; i++)
{
    var choice = previews.Select(current, encounter, "wall-encounter", new IndexRandom(1))!;
    Check("each consecutive preview is new " + i, shown.Add(choice.Input));
    previews.ConfirmWrite(choice.Input, "wall-encounter");
    current = choice.Input;
}
Check("green wall word is reached despite sustained boss cast", shown.Contains(protectedWord.Input));
Check("next preview cycle avoids the last word", previews.Select(current, encounter, "wall-encounter", new IndexRandom(0))!.Input != current);
Check("single remaining word may repeat", previews.Select(current, [bossWord], "wall-encounter", new IndexRandom(0)) == bossWord);
previews.Observe("绿", "wall-encounter");
Check("typed wall prefix overrides cast preference", previews.Select("绿", encounter, "wall-encounter", new IndexRandom(0)) == protectedWord);
Check("disappeared wall never selected from stale preview state", previews.Select("绿", [bossWord], "wall-encounter", new IndexRandom(0)) == bossWord);

if (args.Length >= 2)
{
    var gameRows = System.Text.Json.JsonSerializer.Deserialize<GameWordRow[]>(File.ReadAllText(args[1]))!;
    var replayCatalog = new WordCatalog(gameRows.Select(r => new WordCandidate(r.Text, r.Pinyin)));
    var replays = 0;
    foreach (var line in File.ReadLines(args[0]))
    {
        if (string.IsNullOrWhiteSpace(line))
            continue;
        var report = System.Text.Json.JsonSerializer.Deserialize<ProbeEnvelope>(line)!.Report;
        var pool = CandidateReader.Read(report, replayCatalog);
        if (report.Input.Blocked || pool.Length < 2 || !pool.Any(w => w.Priority == 1) || !pool.Any(w => w.Priority == 0))
            continue;
        var replay = new CompletionEngine();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var preview = "";
        for (var i = 0; i < pool.Length; i++)
        {
            var choice = replay.Select(preview, pool, "captured-encounter", new IndexRandom(1))!;
            Check("captured mixed encounter does not starve other words", seen.Add(choice.Input));
            replay.ConfirmWrite(choice.Input, "captured-encounter");
            preview = choice.Input;
        }
        Check("every captured mixed candidate can be previewed", seen.SetEquals(pool.Select(w => w.Input)));
        if (++replays >= 25)
            break;
    }
    Check("real mixed casting encounters were replayed", replays > 0);
    Console.WriteLine($"Replayed {replays} captured mixed casting encounters.");
}
Console.WriteLine($"PASS: {passed} behavioral checks");

sealed class IndexRandom(int index) : Random
{
    public override int Next(int maxValue) => index % maxValue;
}

sealed record GameWordRow(uint Id, string Text, string Pinyin);
sealed record ProbeEnvelope(ProbeReport Report);
