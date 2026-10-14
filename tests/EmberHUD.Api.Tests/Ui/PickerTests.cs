using System.Drawing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class PickerTests : IDisposable
{
    sealed record Mod(string Text, string Group);

    static readonly Mod[] Mods = [new("+10% to Fire Resistance", "Resist"), new("+12% to Cold Resistance", "Resist"), new("Adds 5 to 9 Fire Damage", "Damage"), new("+40 to maximum Life", "")];

    [Theory]
    [InlineData("", "anything", true)]
    [InlineData("map chaos", "chaos map roll", true)]
    [InlineData("map missing", "chaos map roll", false)]
    [InlineData("fire.*resist", "1% to fire and cold resistance", true)]
    [InlineData("(fire", "a (fire) mod", true)]
    [InlineData("(fire", "a cold mod", false)]
    [InlineData("x", null, false)]
    public void EveryWordHasToLandInAnyOrderRegexWhereItParses(string filter, string? text, bool expected) => Assert.Equal(expected, Picker.Matches(filter, text));

    [Fact]
    public void ATermIsCaseInsensitive() => Assert.True(Picker.TermMatcher("MAP")("chaos map"));

    [Fact]
    public void ATermThatIsNotARegexFallsBackToAPlainContains()
    {
        var term = Picker.TermMatcher("(fire");
        Assert.True(term("A (FIRE) mod"));
        Assert.False(term("fire"));
    }

    [Fact]
    public void ARegexTermTheLinearEngineCannotRunFallsBackToAPlainContains()
    {
        var term = Picker.TermMatcher(@"(a)\1");
        Assert.True(term(@"x(A)\1y"));
        Assert.False(term("aa"));
    }

    [Fact]
    public void ATermThatIsARegexIsUsedAsOne()
    {
        var term = Picker.TermMatcher("^fi.e$");
        Assert.True(term("FIRE"));
        Assert.False(term("a fire"));
    }

    [Fact]
    public void ACatastrophicRegexTermRunsInLinearTimeInsteadOfFreezing()
    {
        var term = Picker.TermMatcher("(a+)+$");
        var text = new string('a', 30) + "b";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var matched = term(text);
        watch.Stop();
        Assert.False(matched);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), watch.Elapsed.ToString());
    }

    [Fact]
    public void ARegexTermStillMatchesWhenTheCpuIsOversubscribed()
    {
        var term = Picker.TermMatcher("fi.e");
        var text = new string('x', 20_000) + "fire";
        var misses = 0;
        var threads = Enumerable.Range(0, Environment.ProcessorCount * 2).Select(_ => new Thread(() =>
        {
            for (var i = 0; i < 30; i++)
                if (!term(text)) Interlocked.Increment(ref misses);
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());
        Assert.Equal(0, misses);
    }

    [Fact]
    public void CategoriesLeadWithAllDedupAndSort() => Assert.Equal(new[] { Picker.AnyCategory, "a", "b" }, Picker.Categories<string>(["b", "a", "b", ""], s => s));

    [Fact]
    public void CategoriesOfNothingIsJustAll() => Assert.Equal(new[] { Picker.AnyCategory }, Picker.Categories(Array.Empty<string>(), s => s));

    [Fact]
    public void CategoriesSortIgnoringCaseAndTreatNullAsNone() => Assert.Equal(new[] { Picker.AnyCategory, "alpha", "Beta" }, Picker.Categories<string?>(["Beta", null, "alpha"], s => s));

    [Fact]
    public void TheSameFilterReusesItsCompiledTerms()
    {
        var first = Picker.Terms("fire cold");
        Assert.Same(first, Picker.Terms("fire cold"));
        Assert.Equal(2, first.Length);
        Assert.Empty(Picker.Terms("   "));
    }

    [Fact]
    public void AnotherFilterDoesNotEvictTheFirst()
    {
        var fire = Picker.Terms("fire");
        Picker.Terms("cold");

        Assert.Same(fire, Picker.Terms("fire"));
        Assert.Empty(Picker.Terms(null));
    }

    [Fact]
    public void TheTermCacheStartsOverPastItsLimit()
    {
        var first = Picker.Terms("limit-first");
        for (var i = 0; i < 80; i++) Picker.Terms("limit-" + i);

        Assert.NotSame(first, Picker.Terms("limit-first"));
    }

    const string Win = "picker";

    static readonly Vector2 PopupAt = new(50, 50);

    readonly record struct Metrics(float Line, float Frame, float PanelFrame, Vector2 Spacing, Vector2 Padding, Dictionary<string, float> PanelWidths);

    static Metrics Measure(params string[] texts) => UiFrame.Measure(() =>
    {
        var style = ImGui.GetStyle();
        var line = ImGui.GetTextLineHeight();
        var frame = ImGui.GetFrameHeight();
        var spacing = style.ItemSpacing;
        var padding = style.WindowPadding;
        using var _ = new Controls.PanelStyleScope();
        var widths = texts.Distinct().ToDictionary(t => t, t => ImGui.CalcTextSize(t).X + style.FramePadding.X * 2);
        return new Metrics(line, frame, ImGui.GetFrameHeight(), spacing, padding, widths);
    });

    static T ScopedValue<T>(Func<T> read)
    {
        var value = read();
        UiFrame.CloseAllPopups();
        return value;
    }

    public void Dispose() => UiFrame.CloseAllPopups();

    sealed class Rig
    {
        public readonly string Id = UiFrame.NextId("pk");
        public readonly bool Multi;
        public readonly PickerOpts<Mod> Opts;
        public Mod[]? Items = Mods;
        public PickerState<Mod>? State;
        public bool UseButton;
        public bool Lazy;
        public string Label = "Pick";
        public string? ButtonId;
        public bool OpenNext;
        public int Fires;
        public Mod? Picked;
        public List<Mod>? Committed;
        public bool Open;
        public readonly List<(Mod Item, Vector2 Pos)> Rows = [];
        public Vector2? FirstCategory;
        public Vector2 ButtonPos;
        public readonly List<uint> RowScopes = [];

        public Rig(bool multi = false, Action<PickerOpts<Mod>>? configure = null, bool defaultKey = false)
        {
            Multi = multi;
            Opts = new PickerOpts<Mod> { Primary = m => m.Text, Category = m => m.Group, Key = defaultKey ? null : m => m.Text };
            configure?.Invoke(Opts);
            var primary = Opts.Primary;
            if (primary is not null)
                Opts.Primary = m =>
                {
                    var at = ImGui.GetCursorScreenPos();
                    if (!ImGuiP.GetCurrentTable().IsNull && !(Rows.Count > 0 && ReferenceEquals(Rows[^1].Item, m) && Rows[^1].Pos == at))
                    {
                        Rows.Add((m, at));
                        RowScopes.Add(ImGui.GetID("probe"));
                    }
                    return primary(m);
                };
            var category = Opts.Category;
            if (category is not null)
                Opts.Category = m =>
                {
                    FirstCategory ??= ImGui.GetCursorScreenPos();
                    return category(m);
                };
        }

        public string PopupId => UseButton ? ButtonId ?? Label : Id;

        public IEnumerable<string> Texts => Rows.Select(r => r.Item.Text);

        public void Body()
        {
            if (OpenNext)
            {
                ImGui.OpenPopup(Id);
                OpenNext = false;
            }
            ImGui.SetNextWindowPos(PopupAt);
            ButtonPos = ImGui.GetCursorScreenPos();
            bool fired;
            Mod? picked = null;
            List<Mod>? committed = null;
            IEnumerable<Mod>? source = Lazy ? Items?.Select(m => m) : Items;
            if (UseButton) fired = Multi ? Picker.ButtonMulti(Label, source, ref State, out committed, Opts, ButtonId) : Picker.Button(Label, source, ref State, out picked, Opts, ButtonId);
            else fired = Multi ? Picker.PopupMulti(Id, source, ref State, out committed, Opts) : Picker.Popup(Id, source, ref State, out picked, Opts);
            if (fired)
            {
                Fires++;
                Picked = picked;
                Committed = committed;
            }
            Open = ImGui.IsPopupOpen(PopupId);
        }

        public void Tick()
        {
            Rows.Clear();
            RowScopes.Clear();
            FirstCategory = null;
            Body();
        }

        public void Frame() => UiFrame.Run(Win, Tick);

        public void Frames(int count = 3)
        {
            for (var i = 0; i < count; i++) Frame();
        }

        public Rig Opened()
        {
            OpenNext = true;
            Frames();
            return this;
        }

        public Rig Typed(string text)
        {
            Opened();
            UiFrame.Io().AddInputCharactersUTF8(text);
            Frames();
            return this;
        }

        public void Click(Vector2 at) => UiFrame.Click(Win, at, Tick);

        public void ClickRow(int index, float down = 2f) => Click(Rows[index].Pos + new Vector2(10f, down));

        public void Close()
        {
            UiFrame.CloseAllPopups();
            Frames(2);
        }
    }

    static List<ImDrawVert> AllVerts()
    {
        List<ImDrawVert> verts = [];
        var windows = ImGui.GetCurrentContext().Windows;
        for (var i = 0; i < windows.Size; i++)
            if (windows[i].Active) verts.AddRange(UiFrame.Verts(windows[i].DrawList));
        return verts;
    }

    static int VertCount() => AllVerts().Count;

    static Vector2 FooterButton(Metrics m, PickerOpts<Mod> opts, int selected, int which)
    {
        string[] labels = [opts.CommitLabel + " (" + selected + ")", "Clear", "Close"];
        var x = PopupAt.X + m.Padding.X;
        for (var i = 0; i < which; i++) x += m.PanelWidths[labels[i]] + m.Spacing.X;
        return new Vector2(x + m.PanelWidths[labels[which]] / 2f, PopupAt.Y + m.Padding.Y + opts.Size.Y - m.PanelFrame - m.Spacing.Y + m.PanelFrame / 2f);
    }

    static Vector2 ComboEntry(Metrics m, int index)
    {
        var popup = UiFrame.ActiveWindow(ImGuiWindowFlags.Popup, w => w.Pos != PopupAt);
        if (popup.IsNull) throw new InvalidOperationException("no combo popup is open");
        return popup.Pos + new Vector2(30f, m.Padding.Y + index * (m.Line + m.Spacing.Y) + m.Line / 2f);
    }

    static Vector2 ComboAt(Rig rig, Metrics m) => rig.FirstCategory!.Value + new Vector2(20f, m.PanelFrame / 2f);

    static bool ComboOpen() => !UiFrame.ActiveWindow(ImGuiWindowFlags.Popup, w => w.Pos != PopupAt).IsNull;

    static float FirstRowY(Action<PickerOpts<Mod>> configure, Mod[]? items = null) => ScopedValue(() => new Rig(configure: configure) { Items = items ?? Mods }.Opened().Rows[0].Pos.Y);

    static int TextVertsAt(float y, float height) => AllVerts().Count(v => v.Pos.Y >= y && v.Pos.Y <= y + height && v.Pos.X > PopupAt.X && v.Pos.X < PopupAt.X + 500f);

    static Vector2 OnButton(Rig rig, Metrics m) => rig.ButtonPos + new Vector2(10f, m.Frame / 2f);

    static List<string> Filtered(string typed, Action<PickerOpts<Mod>>? configure = null) => ScopedValue(() => new Rig(configure: configure).Typed(typed).Texts.ToList());

    static Rig FiveCategories(string odd) => new(configure: o => o.Category = m => m.Group == "Resist" ? odd : m.Group == "Damage" ? "Damage" : m.Text.Contains("Life") ? "Life" : "Other") { Items = [new("a", "Resist"), new("b", "Damage"), new("c Life", ""), new("d", "x"), new("e", "y")] };

    [Fact]
    public void ASinglePickerDrawsOneBalancedFrameOpen()
    {
        PickerState<Mod>? state = null;
        Mod? picked = null;
        var acted = true;
        var opts = new PickerOpts<Mod> { Primary = m => m.Text, Category = m => m.Group, Secondary = m => "100% " + m.Group };
        UiFrame.Run("test", () =>
        {
            ImGui.OpenPopup("mods");
            acted = Picker.Popup("mods", Mods, ref state, out picked, opts);
        });
        Assert.False(acted);
        Assert.Null(picked);
        Assert.NotNull(state);
    }

    [Fact]
    public void AMultiPickerDrawsOneBalancedFrameOpen()
    {
        PickerState<Mod>? state = null;
        List<Mod>? committed = [];
        var acted = true;
        UiFrame.Run("test", () =>
        {
            ImGui.OpenPopup("multi");
            acted = Picker.PopupMulti("multi", Mods, ref state, out committed, new PickerOpts<Mod> { Primary = m => m.Text, Category = m => m.Group, GroupByCategory = false });
            Picker.Button("Pick...", Mods, ref state, out _);
        });
        Assert.False(acted);
        Assert.Null(committed);
    }

    [Fact]
    public void AClosedPickerDrawsNothingAndDoesNotTouchItsItems()
    {
        var rig = new Rig();
        var touched = false;
        rig.Opts.Primary = _ =>
        {
            touched = true;
            return "";
        };
        rig.Frames();
        Assert.False(rig.Open);
        Assert.False(touched);
        Assert.Empty(rig.Rows);
        Assert.NotNull(rig.State);
    }

    [Fact]
    public void AllRowsDrawGroupedByCategoryThenText()
    {
        var rig = new Rig().Opened();
        Assert.True(rig.Open);
        Assert.Equal(["+40 to maximum Life", "Adds 5 to 9 Fire Damage", "+10% to Fire Resistance", "+12% to Cold Resistance"], rig.Texts);
    }

    [Fact]
    public void WithoutGroupingTheRowsKeepTheCallersOrder()
    {
        var rig = new Rig(configure: o => o.GroupByCategory = false).Opened();
        Assert.Equal(Mods.Select(m => m.Text), rig.Texts);
    }

    [Fact]
    public void AGroupRunIsHeadedByItsName()
    {
        var rig = new Rig().Opened();
        var gaps = rig.Rows.Zip(rig.Rows.Skip(1), (a, b) => b.Pos.Y - a.Pos.Y).ToArray();
        Assert.True(gaps[1] > gaps[2]);
    }

    [Fact]
    public void UngroupedRowsHaveNoHeaders()
    {
        var rig = new Rig(configure: o => o.GroupByCategory = false).Opened();
        var gaps = rig.Rows.Zip(rig.Rows.Skip(1), (a, b) => b.Pos.Y - a.Pos.Y).Distinct().ToArray();
        Assert.Single(gaps);
    }

    [Fact]
    public void ClickingARowPicksItOnceAndClosesThePopup()
    {
        var rig = new Rig().Opened();
        rig.ClickRow(1);
        Assert.Equal(1, rig.Fires);
        Assert.Same(Mods[2], rig.Picked);
        Assert.False(rig.Open);
        rig.Frames();
        Assert.Equal(1, rig.Fires);
        Assert.Empty(rig.State!.Selected);
    }

    [Fact]
    public void ClickingOutsideClosesWithoutPicking()
    {
        var rig = new Rig().Opened();
        rig.Click(UiFrame.WindowSize - new Vector2(10f, 10f));
        Assert.False(rig.Open);
        Assert.Equal(0, rig.Fires);
    }

    [Fact]
    public void TypingFiltersTheRowsInAnyOrder()
    {
        var rig = new Rig().Opened();
        UiFrame.Io().AddInputCharactersUTF8("fire");
        rig.Frames();
        Assert.Equal("fire", rig.State!.Filter);
        Assert.Equal(["Adds 5 to 9 Fire Damage", "+10% to Fire Resistance"], rig.Texts);
    }

    [Fact]
    public void ARegexTermFiltersAndAHalfTypedOneStillFilters()
    {
        Assert.Equal(["+10% to Fire Resistance"], Filtered("fire.*resist"));
        Assert.Empty(Filtered("(fire"));
        Assert.Empty(Filtered("("));
        Assert.Equal(["Adds 5 to 9 Fire Damage"], Filtered("dam.ge"));
    }

    [Fact]
    public void ASearchMatchingNothingKeepsThePopupOpenAndPicksNothing()
    {
        var rig = new Rig().Typed("zzz");
        Assert.Equal("zzz", rig.State!.Filter);
        Assert.True(rig.Open);
        Assert.Empty(rig.Rows);
        Assert.Equal(0, rig.Fires);
    }

    [Fact]
    public void AReopenedPickerStartsClean()
    {
        var rig = new Rig(true).Opened();
        rig.State!.Filter = "fire";
        rig.State.Category = "Resist";
        rig.State.Selected.Add(Mods[0]);
        rig.Close();
        Assert.False(rig.Open);
        rig.Opened();
        Assert.Equal("", rig.State.Filter);
        Assert.Equal(Picker.AnyCategory, rig.State.Category);
        Assert.Empty(rig.State.Selected);
        Assert.Equal(4, rig.Rows.Count);
    }

    [Fact]
    public void ACallerSuppliedStateIsKept()
    {
        var state = new PickerState<Mod>();
        var rig = new Rig { State = state };
        rig.Opened();
        Assert.Same(state, rig.State);
    }

    [Fact]
    public void TheCategoryFilterPicksOneGroupAndDropsItsHeaders()
    {
        var rig = new Rig().Opened();
        rig.State!.Category = "Resist";
        rig.Frame();
        Assert.Equal(["+10% to Fire Resistance", "+12% to Cold Resistance"], rig.Texts);
        rig.State.Category = Picker.AnyCategory;
        rig.Frame();
        Assert.Equal(4, rig.Rows.Count);
    }

    [Fact]
    public void ClickingACategoryButtonFiltersTheRows()
    {
        var m = Measure("All", "Damage");
        var rig = new Rig().Opened();
        rig.Click(rig.FirstCategory!.Value + new Vector2(m.PanelWidths["All"] + 1f + m.PanelWidths["Damage"] / 2f, m.PanelFrame / 2f));
        Assert.Equal("Damage", rig.State!.Category);
        Assert.Equal(["Adds 5 to 9 Fire Damage"], rig.Texts);
        Assert.True(rig.Open);
    }

    [Fact]
    public void ManyCategoriesGoInAComboThatSelectsOne()
    {
        var m = Measure();
        var rig = FiveCategories("Resist").Opened();
        rig.Click(ComboAt(rig, m));
        rig.Click(ComboEntry(m, 1));
        Assert.Equal("Damage", rig.State!.Category);
        Assert.Equal(["b"], rig.Texts);
        Assert.True(rig.Open);
    }

    [Fact]
    public void ACategoryLabelInTheComboIsNotPrintfEscaped()
    {
        var m = Measure();
        int Count(string odd) => ScopedValue(() =>
        {
            var rig = FiveCategories(odd).Opened();
            rig.Click(ComboAt(rig, m));
            rig.Frames();
            return VertCount();
        });
        Assert.Equal(Count("5#d"), Count("5%d"));
    }

    [Fact]
    public void WithoutACategoryThereIsNoPillColumnAndNoFilterRow()
    {
        var rig = new Rig(configure: o => o.Category = null).Opened();
        Assert.Null(rig.FirstCategory);
        Assert.Equal(Mods.Select(m => m.Text), rig.Texts);
    }

    [Fact]
    public void ShowCategoryFilterOffHidesTheFilterRowButKeepsThePills()
    {
        var m = Measure();
        var pill = Color.FromArgb(255, 1, 2, 3);
        var withRow = FirstRowY(o => o.GroupByCategory = false);
        var rig = new Rig(configure: o =>
        {
            o.ShowCategoryFilter = false;
            o.GroupByCategory = false;
            o.CategoryColor = _ => pill;
        }).Opened();
        Assert.True(withRow - rig.Rows[0].Pos.Y >= m.PanelFrame);
        Assert.Contains(AllVerts(), v => v.Col == EColor.U32(pill));
    }

    [Fact]
    public void AListWithOneCategoryHasNoFilterRow()
    {
        var m = Measure();
        var withRow = FirstRowY(o => o.GroupByCategory = false);
        var single = FirstRowY(o => o.GroupByCategory = false, [new("a", "Resist"), new("b", "Resist")]);
        Assert.True(withRow - single >= m.PanelFrame);
    }

    [Fact]
    public void FourShortCategoriesStayButtonsAndALongNameGoesToTheCombo()
    {
        var m = Measure();
        bool ComboOpens(Mod[] items) => ScopedValue(() =>
        {
            var rig = new Rig { Items = items }.Opened();
            rig.Click(ComboAt(rig, m));
            return ComboOpen();
        });
        Assert.False(ComboOpens([new("a", "x"), new("b", "y"), new("c", "z")]));
        Assert.True(ComboOpens(FiveCategories("Resist").Items!));
        Assert.True(ComboOpens([new("a", "Short"), new("b", "A very long category name")]));
    }

    [Fact]
    public void ThePillTakesTheCallersColour()
    {
        var pill = Color.FromArgb(255, 1, 2, 3);
        new Rig(configure: o => o.CategoryColor = _ => pill).Opened();
        Assert.Contains(AllVerts(), v => v.Col == EColor.U32(pill));
    }

    [Fact]
    public void WithoutAColourFuncThePillHashesTheCategoryName()
    {
        new Rig().Opened();
        Assert.Contains(AllVerts(), v => v.Col == EColor.U32(EColor.CategoryColor("Resist")));
    }

    [Fact]
    public void ShowSearchOffDrawsNoSearchBoxSoTypingGoesNowhere()
    {
        var rig = new Rig(configure: o => o.ShowSearch = false).Opened();
        UiFrame.Io().AddInputCharactersUTF8("fire");
        rig.Frames();
        Assert.Equal("", rig.State!.Filter);
        Assert.Equal(4, rig.Rows.Count);
    }

    [Fact]
    public void TheSearchFuncReplacesTheRowTextForMatching()
    {
        Assert.Equal(["Adds 5 to 9 Fire Damage"], Filtered("damage", o => o.Search = m => m.Group));
        Assert.Empty(Filtered("life", o => o.Search = m => m.Group));
    }

    [Fact]
    public void ATermMayMatchPrimarySecondaryOrCategoryAndEveryTermMustMatchSomewhere()
    {
        void Secondary(PickerOpts<Mod> o) => o.Secondary = m => "tier " + m.Text.Length;
        Assert.Equal(["Adds 5 to 9 Fire Damage"], Filtered("damage", Secondary));
        Assert.Equal(["+40 to maximum Life"], Filtered("tier 19", Secondary));
        Assert.Equal(["+10% to Fire Resistance"], Filtered("resist fire", Secondary));
        Assert.Empty(Filtered("resist zzz", Secondary));
    }

    [Fact]
    public void MaxRowsCapsTheDrawnRows()
    {
        var rig = new Rig(configure: o => o.MaxRows = 2).Opened();
        Assert.Equal(2, rig.Rows.Count);
    }

    [Fact]
    public void TheCapAppliesAfterTheSearchFilter()
    {
        Assert.Equal(["+40 to maximum Life"], Filtered("life", o => o.MaxRows = 1));
    }

    [Fact]
    public void TheSecondLineIsPartOfTheClickableRow()
    {
        var line = UiFrame.Measure(ImGui.GetTextLineHeight);
        var rig = new Rig(configure: o =>
        {
            o.Secondary = _ => "second line";
            o.GroupByCategory = false;
        }).Opened();
        rig.ClickRow(0, line * 1.5f);
        Assert.Equal(1, rig.Fires);
        Assert.Same(Mods[0], rig.Picked);
    }

    [Fact]
    public void AWrappedRowIsClickableOnItsLastLine()
    {
        var line = UiFrame.Measure(ImGui.GetTextLineHeight);
        var wrapped = string.Concat(Enumerable.Repeat("word ", 80));
        var rig = new Rig(configure: o => o.GroupByCategory = false) { Items = [new(wrapped, ""), new("short", "")] }.Opened();
        Assert.True(rig.Rows[1].Pos.Y - rig.Rows[0].Pos.Y > line * 3f);
        rig.ClickRow(0, line * 2.5f);
        Assert.Equal(1, rig.Fires);
        Assert.Equal(wrapped, rig.Picked?.Text);
    }

    [Fact]
    public void ASecondaryLineDrawsTallerRows()
    {
        var line = UiFrame.Measure(ImGui.GetTextLineHeight);
        float Gap(Action<PickerOpts<Mod>> configure)
        {
            var rig = new Rig(configure: o =>
            {
                o.GroupByCategory = false;
                configure(o);
            }).Opened();
            var gap = rig.Rows[1].Pos.Y - rig.Rows[0].Pos.Y;
            rig.Close();
            return gap;
        }
        Assert.True(Gap(o => o.Secondary = _ => "x") - Gap(_ => { }) >= line - 0.5f);
    }

    [Fact]
    public void ANullItemListIsTreatedAsEmpty()
    {
        var rig = new Rig { Items = null }.Opened();
        Assert.True(rig.Open);
        Assert.Empty(rig.Rows);
        Assert.Null(rig.FirstCategory);
        Assert.Equal(0, rig.Fires);
    }

    [Fact]
    public void AnEmptyListDrawsNoControlsAndKeepsThePopupOpen()
    {
        var rig = new Rig { Items = [] }.Opened();
        Assert.True(rig.Open);
        Assert.Empty(rig.Rows);
        Assert.Null(rig.FirstCategory);
    }

    [Fact]
    public void TheEmptyTextIsDrawn()
    {
        int Count(string text) => ScopedValue(() =>
        {
            new Rig(configure: o => o.EmptyText = text) { Items = [] }.Opened();
            return VertCount();
        });
        Assert.True(Count("none") < Count("nothing to see here at all"));
    }

    [Fact]
    public void TheEmptyTextIsNotPrintfInterpreted()
    {
        int Count(string token) => ScopedValue(() =>
        {
            new Rig(configure: o => o.EmptyText = "none " + token) { Items = [] }.Opened();
            return VertCount();
        });
        Assert.Equal(Count("#d"), Count("%d"));
    }

    [Fact]
    public void ARowTextIsNotPrintfInterpreted()
    {
        int Count(string token) => ScopedValue(() =>
        {
            new Rig(configure: o => o.GroupByCategory = false) { Items = [new("row " + token, "")] }.Opened();
            return VertCount();
        });
        Assert.Equal(Count("#d"), Count("%d"));
    }

    [Fact]
    public void ASecondaryTextIsNotPrintfInterpreted()
    {
        int Count(string token) => ScopedValue(() =>
        {
            new Rig(configure: o => o.Secondary = _ => "sub " + token).Opened();
            return VertCount();
        });
        Assert.Equal(Count("#d"), Count("%d"));
    }

    [Fact]
    public void ThePickedSummaryIsNotPrintfInterpreted()
    {
        int Count(string token) => ScopedValue(() =>
        {
            var rig = new Rig(true, o => o.Key = _ => "key " + token) { Items = [new("row", "")] }.Opened();
            rig.ClickRow(0);
            rig.Frames();
            return VertCount();
        });
        Assert.Equal(Count("#d"), Count("%d"));
    }

    [Fact]
    public void ThePickedSummaryListsTheSelectedKeys()
    {
        int Count(string key) => ScopedValue(() =>
        {
            var rig = new Rig(true, o => o.Key = _ => key) { Items = [new("row", "")] }.Opened();
            rig.ClickRow(0);
            rig.Frames();
            return VertCount();
        });
        Assert.True(Count("k") < Count("kkkkkkkkkk"));
    }

    [Fact]
    public void TheSearchHintIsDrawnWhileTheBoxIsEmpty()
    {
        int Count(string hint) => ScopedValue(() =>
        {
            new Rig(configure: o => o.Hint = hint).Opened();
            return VertCount();
        });
        Assert.True(Count("a") < Count("aaaaaaaaaa"));
    }

    [Fact]
    public void TheTipShowsOnHoveringTheQuestionMark()
    {
        var m = Measure();
        int TipVerts(string? tip)
        {
            var rig = new Rig(configure: o => o.Tip = tip).Opened();
            var at = new Vector2(PopupAt.X + m.Padding.X + rig.Opts.Size.X - 90f + m.Spacing.X + 5f, PopupAt.Y + m.Padding.Y + m.PanelFrame / 2f);
            UiFrame.Hover(Win, at, rig.Tick);
            var uvs = UiFrame.ActiveTooltipUvs();
            rig.Close();
            return uvs?.Count ?? 0;
        }
        var custom = TipVerts("short");
        Assert.True(custom > 0);
        Assert.True(TipVerts(null) > custom);
    }

    [Fact]
    public void TheSecondLineIsDrawnInTheDisabledTextColour()
    {
        var disabled = new Vector4(0.2f, 0.9f, 0.3f, 1f);
        using var colours = UiFrame.WithColors((ImGuiCol.Text, new Vector4(0.9f, 0.2f, 0.7f, 1f)), (ImGuiCol.TextDisabled, disabled));
        var colour = ImGui.GetColorU32(disabled);
        int Disabled(Action<PickerOpts<Mod>> configure)
        {
            new Rig(configure: configure).Opened();
            return AllVerts().Count(v => v.Col == colour);
        }
        Assert.True(Disabled(o => o.Secondary = _ => "second line of text") > Disabled(_ => { }));
    }

    static bool DrawsText(List<Vector2> reference)
    {
        var drawn = AllVerts().Select(v => v.Uv).ToList();
        for (var at = 0; at + reference.Count <= drawn.Count; at++)
            if (drawn.Skip(at).Take(reference.Count).SequenceEqual(reference)) return true;
        return false;
    }

    [Theory]
    [InlineData("", "4 of 4")]
    [InlineData("fire", "2 of 4")]
    public void TheHeaderCountsTheMatchesOutOfEverything(string typed, string count)
    {
        var reference = UiFrame.GlyphUvs("count reference", count);
        var rig = new Rig();
        if (typed.Length > 0) rig.Typed(typed);
        else rig.Opened();
        Assert.True(DrawsText(reference));
    }

    [Fact]
    public void ThePillIsRightAlignedInItsColumn()
    {
        var pill = Color.FromArgb(255, 1, 2, 3);
        float DotX(string category)
        {
            var rig = new Rig(configure: o => o.CategoryColor = _ => pill) { Items = [new("row", category)] }.Opened();
            var xs = AllVerts().Where(v => v.Col == EColor.U32(pill)).Select(v => v.Pos.X).ToArray();
            rig.Close();
            return (xs.Min() + xs.Max()) / 2f;
        }
        Assert.True(DotX("ab") - DotX("abcdefgh") > 20f);
    }

    [Fact]
    public void TheCategoryWidthNarrowsTheTextColumn()
    {
        var line = Measure().Line;
        var wrapped = string.Concat(Enumerable.Repeat("word ", 40));
        float Gap(float width)
        {
            var rig = new Rig(configure: o =>
            {
                o.GroupByCategory = false;
                o.CategoryWidth = width;
            }) { Items = [new(wrapped, "x"), new("short", "x")] }.Opened();
            var gap = rig.Rows[1].Pos.Y - rig.Rows[0].Pos.Y;
            rig.Close();
            return gap;
        }
        Assert.True(Gap(430f) > Gap(60f) + line - 0.5f);
    }

    [Fact]
    public void TheSizeOptionSizesThePopupBodyAndMovesTheFooter()
    {
        var m = Measure("Add (1)", "Clear");
        var rig = new Rig(true, o => o.Size = new Vector2(420f, 300f)).Opened();
        rig.ClickRow(0);
        rig.Click(FooterButton(m, rig.Opts, 1, 1));
        Assert.Empty(rig.State!.Selected);
        Assert.True(rig.Open);
    }

    [Fact]
    public void TheNothingMatchesNoteTakesTheFirstRowSlot()
    {
        var line = Measure().Line;
        var slot = FirstRowY(o => o.GroupByCategory = false);
        new Rig(configure: o => o.GroupByCategory = false).Typed("zzz");
        Assert.True(TextVertsAt(slot, line) > 40);
    }

    [Fact]
    public void RowsPastTheCapAreCountedInANote()
    {
        var line = Measure().Line;
        var slot = ScopedValue(() => new Rig(configure: o => o.GroupByCategory = false).Opened().Rows[1].Pos.Y);
        new Rig(configure: o =>
        {
            o.GroupByCategory = false;
            o.MaxRows = 1;
        }).Opened();
        Assert.True(TextVertsAt(slot, line) > 40);
    }

    [Fact]
    public void ANullCategoryFromTheCallerCountsAsNone()
    {
        var rig = new Rig(configure: o => o.Category = m => m.Group.Length == 0 ? null! : m.Group).Opened();
        Assert.Equal(["+40 to maximum Life", "Adds 5 to 9 Fire Damage", "+10% to Fire Resistance", "+12% to Cold Resistance"], rig.Texts);
        rig.State!.Category = "Resist";
        rig.Frame();
        Assert.Equal(2, rig.Rows.Count);
    }

    [Fact]
    public void ANullTextFromTheCallerIsDrawnAsEmptyAndMatchesNothing()
    {
        Mod[] items = [new(null!, "g"), new("b", "g")];
        var rig = new Rig { Items = items }.Opened();
        Assert.Equal(2, rig.Rows.Count);
        Assert.Equal(["b"], ScopedValue(() => new Rig(configure: o => o.Secondary = _ => null!) { Items = items }.Typed("b").Texts.ToList()));
    }

    [Fact]
    public void ACategoryAloneCanSatisfyATerm()
    {
        Mod[] items = [new("alpha", "zzgrp"), new("beta", "other"), new("gamma", "third")];
        Assert.Equal(["alpha"], ScopedValue(() => new Rig { Items = items }.Typed("zzgrp").Texts.ToList()));
    }

    [Fact]
    public void RowsWithTheSameKeyShareOneIdScopeSoNoRowLeaksItsId()
    {
        var rig = new Rig(configure: o => o.GroupByCategory = false) { Items = [new("same", ""), new("same", ""), new("same", "")] }.Opened();
        Assert.Equal(3, rig.RowScopes.Count);
        Assert.Single(rig.RowScopes.Distinct());
    }

    [Fact]
    public void TheListIsDrawnInPanelStyleAndTheStyleIsRestored()
    {
        (Vector2, float, float, float, float) Read() => (ImGui.GetStyle().FramePadding, ImGui.GetStyle().FrameRounding, ImGui.GetStyle().GrabRounding, ImGui.GetStyle().ChildRounding, ImGui.GetStyle().PopupRounding);
        var before = UiFrame.Measure(Read);
        var panel = UiFrame.Measure(() =>
        {
            using var _ = new Controls.PanelStyleScope();
            return Read();
        });
        Assert.NotEqual(before, panel);
        (Vector2, float, float, float, float)? inside = null;
        var rig = new Rig(configure: o => o.Secondary = _ =>
        {
            if (!ImGuiP.GetCurrentTable().IsNull) inside ??= Read();
            return "";
        }).Opened();
        Assert.Equal(panel, inside);
        Assert.Equal(before, UiFrame.Measure(Read));
    }

    [Fact]
    public void ANullSearchKeyMatchesNothing()
    {
        Assert.Empty(Filtered("x", o => o.Search = _ => null!));
    }

    [Fact]
    public void MultiPickToggleCommitAndClose()
    {
        var m = Measure("Add (2)");
        var rig = new Rig(true, o => o.GroupByCategory = false).Opened();
        rig.ClickRow(0);
        rig.ClickRow(2);
        Assert.Equal([Mods[0], Mods[2]], rig.State!.Selected);
        Assert.True(rig.Open);
        Assert.Equal(0, rig.Fires);
        rig.ClickRow(0);
        Assert.Equal([Mods[2]], rig.State.Selected);
        rig.ClickRow(1);
        Assert.Equal([Mods[2], Mods[1]], rig.State.Selected);

        rig.Click(FooterButton(m, rig.Opts, 2, 0));
        Assert.Equal(1, rig.Fires);
        Assert.Equal([Mods[2], Mods[1]], rig.Committed);
        Assert.NotSame(rig.State.Selected, rig.Committed);
        Assert.False(rig.Open);
        Assert.Null(rig.Picked);
    }

    [Fact]
    public void ClearEmptiesTheSelectionAndKeepsThePopupOpen()
    {
        var m = Measure("Add (2)", "Clear");
        var rig = new Rig(true).Opened();
        rig.ClickRow(0);
        rig.ClickRow(1);
        Assert.Equal(2, rig.State!.Selected.Count);
        rig.Click(FooterButton(m, rig.Opts, 2, 1));
        Assert.Empty(rig.State.Selected);
        Assert.True(rig.Open);
        Assert.Equal(0, rig.Fires);
    }

    [Fact]
    public void CloseClosesWithoutCommitting()
    {
        var m = Measure("Add (1)", "Clear", "Close");
        var rig = new Rig(true).Opened();
        rig.ClickRow(0);
        rig.Click(FooterButton(m, rig.Opts, 1, 2));
        Assert.False(rig.Open);
        Assert.Equal(0, rig.Fires);
        Assert.Null(rig.Committed);
    }

    [Fact]
    public void TheCommitButtonDoesNothingWithNothingSelected()
    {
        var m = Measure("Add (0)");
        var rig = new Rig(true).Opened();
        rig.Click(FooterButton(m, rig.Opts, 0, 0));
        Assert.True(rig.Open);
        Assert.Equal(0, rig.Fires);
        Assert.Null(rig.Committed);
    }

    [Fact]
    public void TheCommitLabelAndCountSizeTheCommitButton()
    {
        var m = Measure("Confirm all (1)", "Clear");
        var rig = new Rig(true, o => o.CommitLabel = "Confirm all").Opened();
        rig.ClickRow(0);
        rig.Click(FooterButton(m, rig.Opts, 1, 1));
        Assert.Empty(rig.State!.Selected);
        Assert.True(rig.Open);
    }

    [Fact]
    public void RowsWithTheSameTextNeedTheirOwnKeyToBeToggledApart()
    {
        Mod[] twins = [new("same", ""), new("same", "")];
        var rig = new Rig(true, o =>
        {
            o.GroupByCategory = false;
            o.Key = m => ReferenceEquals(m, twins[0]) ? "first" : "second";
        }) { Items = twins }.Opened();
        var rows = rig.Rows.Select(r => r.Pos).ToArray();
        rig.Click(rows[0] + new Vector2(10f, 2f));
        rig.Click(rows[1] + new Vector2(10f, 2f));
        Assert.Equal(2, rig.State!.Selected.Count);
    }

    [Fact]
    public void WithoutAKeyTheRowsAreToggledByTheirPrimaryText()
    {
        var rig = new Rig(true, o => o.GroupByCategory = false, true).Opened();
        var rows = rig.Rows.Select(r => r.Pos).ToArray();
        rig.Click(rows[0] + new Vector2(10f, 2f));
        rig.Click(rows[1] + new Vector2(10f, 2f));
        Assert.Equal([Mods[0], Mods[1]], rig.State!.Selected);
        rig.Click(rows[0] + new Vector2(10f, 2f));
        Assert.Equal([Mods[1]], rig.State.Selected);
    }

    [Fact]
    public void RowsWithinAGroupSortByText()
    {
        var rig = new Rig { Items = [new("b", "g"), new("c", "g"), new("a", "g")] }.Opened();
        Assert.Equal(["a", "b", "c"], rig.Texts);
    }

    [Fact]
    public void AnyEnumerableOfItemsWorksNotJustAList()
    {
        var rig = new Rig { Lazy = true }.Opened();
        Assert.Equal(4, rig.Rows.Count);
        rig.ClickRow(0);
        Assert.Same(Mods[3], rig.Picked);
    }

    [Fact]
    public void ASelectedRowIsHighlighted()
    {
        using var colours = UiFrame.WithColors((ImGuiCol.Header, new Vector4(0.1f, 0.8f, 0.2f, 1f)));
        var colour = ImGui.GetColorU32(ImGuiCol.Header);
        var rig = new Rig(true, o => o.GroupByCategory = false).Opened();
        var before = AllVerts().Count(v => v.Col == colour);
        rig.ClickRow(0);
        rig.Frames();
        Assert.True(AllVerts().Count(v => v.Col == colour) > before);
    }

    sealed class Named(string name)
    {
        public readonly List<Vector2> Seen = [];

        public override string ToString()
        {
            if (!ImGuiP.GetCurrentTable().IsNull) Seen.Add(ImGui.GetCursorScreenPos());
            return name;
        }
    }

    [Fact]
    public void WithNoOptionsTheRowsShowToStringAndSearchesItToo()
    {
        Named[] items = [new("alpha"), new("beta"), new("gamma")];
        PickerState<Named>? state = null;
        Named? picked = null;
        void Body()
        {
            ImGui.SetNextWindowPos(PopupAt);
            Picker.Popup("defaults", items, ref state, out var p);
            picked ??= p;
        }
        UiFrame.Run(Win, () => ImGui.OpenPopup("defaults"));
        for (var i = 0; i < 3; i++) UiFrame.Run(Win, Body);
        UiFrame.Io().AddInputCharactersUTF8("bet");
        for (var i = 0; i < 3; i++)
        {
            foreach (var item in items) item.Seen.Clear();
            UiFrame.Run(Win, Body);
        }
        Assert.Empty(items[0].Seen);
        Assert.Empty(items[2].Seen);
        Assert.NotEmpty(items[1].Seen);
        UiFrame.Click(Win, items[1].Seen[^1] + new Vector2(10f, 2f), Body);
        Assert.Same(items[1], picked);
    }

    [Fact]
    public void TheSingleButtonHonoursItsId()
    {
        var m = Measure();
        var rig = new Rig { UseButton = true, Label = "Add", ButtonId = "add-popup" };
        rig.Frames();
        rig.Click(OnButton(rig, m));
        Assert.True(rig.Open);
        Assert.Equal(4, rig.Rows.Count);
    }

    [Fact]
    public void TwoButtonsWithTheSameLabelNeedDistinctIdsInOneScope()
    {
        var a = new Rig { UseButton = true, Label = "Pick", ButtonId = "first" };
        var b = new Rig { UseButton = true, Label = "Pick", ButtonId = "second" };
        void Both()
        {
            a.Rows.Clear();
            b.Rows.Clear();
            a.Body();
            b.Body();
        }
        var m = Measure();
        UiFrame.Run(Win, Both);
        UiFrame.Click(Win, OnButton(b, m), Both);
        Assert.False(a.Open);
        Assert.True(b.Open);
        Assert.Equal(4, b.Rows.Count);
    }

    [Fact]
    public void ASingleRowPickLeavesTheSelectionAndCommitOutputAlone()
    {
        var rig = new Rig().Opened();
        rig.ClickRow(0);
        Assert.Empty(rig.State!.Selected);
        Assert.Null(rig.Committed);
    }

    [Fact]
    public void ThePickerButtonOpensItsPopupUnderItsOwnLabel()
    {
        var m = Measure();
        var rig = new Rig { UseButton = true, Label = "Add mod" };
        rig.Frames();
        Assert.False(rig.Open);
        rig.Click(OnButton(rig, m));
        Assert.True(rig.Open);
        Assert.Equal(4, rig.Rows.Count);
        rig.ClickRow(1);
        Assert.Equal(1, rig.Fires);
        Assert.Same(Mods[2], rig.Picked);
    }

    [Fact]
    public void TheMultiPickerButtonOpensAMultiPopupUnderItsId()
    {
        var m = Measure();
        var rig = new Rig(true) { UseButton = true, Label = "Add mods", ButtonId = "mods-popup" };
        rig.Frames();
        rig.Click(OnButton(rig, m));
        Assert.True(rig.Open);
        rig.ClickRow(0);
        Assert.Single(rig.State!.Selected);
    }

    [Fact]
    public void TwoButtonsWithTheSameLabelOpenOnlyTheirOwnPopupUnderSeparateIdScopes()
    {
        var a = new Rig { UseButton = true, Label = "Pick" };
        var b = new Rig { UseButton = true, Label = "Pick" };
        void Both()
        {
            a.Rows.Clear();
            b.Rows.Clear();
            ImGui.PushID("a");
            a.Body();
            ImGui.PopID();
            ImGui.PushID("b");
            b.Body();
            ImGui.PopID();
        }
        var m = Measure();
        UiFrame.Run(Win, Both);
        UiFrame.Click(Win, OnButton(b, m), Both);
        Assert.False(a.Open);
        Assert.True(b.Open);
        Assert.Empty(a.Rows);
        Assert.Equal(4, b.Rows.Count);
    }
}
