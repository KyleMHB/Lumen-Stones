using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

// The settings tabs, in window order. The rows each tab holds are declared by StoneSettings.DeclareAll.
public static class StoneTabs
{
    // Raw settings outside any row list: owned by their tab, read directly where the behaviour lives.
    public static readonly Tunable Interruptions = new("Interruptions.enabled", 1, Limit.Toggle);
    public static readonly Tunable DarklightMode = new("Darklight.defaultMode", 1, Limit.Integer(0, 2));

    private static LumenstoneSettings Settings => LumenstoneMod.Settings;

    public static readonly TunableTab Lumenstone = new("Lumenstone");
    public static readonly TunableTab Firefly = new("Firefly", Allow("Firefly"), onReset: () => Settings.firefly = true);
    public static readonly TunableTab Glacite = new("Glacite", Allow("Glacite"), onReset: () => Settings.glacite = true);
    public static readonly TunableTab Vitalite = new("Vitalite", Allow("Vitalite"), onReset: () => Settings.vitalite = true);
    public static readonly TunableTab Serenite = new("Serenite", Allow("Serenite"));
    public static readonly TunableTab Verdite = new("Verdite", Allow("Verdite"));
    public static readonly TunableTab Aegite = new("Aegite", Allow("Aegite"));
    public static readonly TunableTab Ferrite = new("Ferrite", Allow("Ferrite"));
    public static readonly TunableTab Sunstone = new("Sunstone", "Allow new Sunstone content, and require Solar energy for active effects");
    public static readonly TunableTab Pyrinth = new("Pyrinth", Allow("Pyrinth"), header: PyrinthHeader,
        onReset: () => { Settings.pyrinth = Settings.pyrinthHeat = true; Settings.overridePyrinth = false; });
    public static readonly TunableTab Darklight = new("Darklight", header: DarklightHeader);
    // Animastone loads only with Royalty; the dark stones only with Ideology.
    public static readonly TunableTab Animastone = new("Animastone", Allow("Animastone"), when: () => ModsConfig.RoyaltyActive);
    public static readonly TunableTab DarkStone = new("DarkStone", when: () => ModsConfig.IdeologyActive);
    public static readonly TunableTab Sanguinite = new("Sanguinite", Allow("Sanguinite"), when: () => ModsConfig.IdeologyActive);
    public static readonly TunableTab Dreadstone = new("Dreadstone", Allow("Dreadstone"), when: () => ModsConfig.IdeologyActive);
    public static readonly TunableTab Hollowstone = new("Hollowstone", Allow("Hollowstone"), when: () => ModsConfig.IdeologyActive);
    public static readonly TunableTab Implants = new("Implants");

    public static readonly IReadOnlyList<TunableTab> All = new[]
    {
        Lumenstone, Firefly, Glacite, Vitalite, Serenite, Verdite, Aegite, Ferrite, Sunstone, Pyrinth, Darklight,
        Animastone, DarkStone, Sanguinite, Dreadstone, Hollowstone, Implants
    };

    private static string Allow(string stone) => "Allow new " + stone + " content";

    private static void PyrinthHeader(Listing_Standard list)
    {
        var edit = Tunables.Edit();
        bool overridePyrinth = edit.OverridePyrinth;
        list.CheckboxLabeled("Override Epochs - Pyrinth balance (restart required)", ref overridePyrinth);
        edit.OverridePyrinth = overridePyrinth;
        list.Label(StoneSettings.PyrinthPresent ? "Base Pyrinth content only. Add-ons keep their own settings." :
            "Epochs - Pyrinth is not loaded. Its column and external controls appear when its definitions are present.");
    }

    private static void DarklightHeader(Listing_Standard list)
    {
        string[] modes = { "Off (Normal light)", "Auto (by Ideoligion)", "Always Darklight" };
        list.Label("Default light mode for newly placed player fixtures and jars of fireflies. Existing fixtures keep their chosen mode.");
        if (list.ButtonText(modes[CompLumenDarklight.DefaultMode]))
            Find.WindowStack.Add(new FloatMenu(Enumerable.Range(0, modes.Length).Select(mode =>
                new FloatMenuOption(modes[mode], () => Tunables.Edit().Set(DarklightMode.Key, mode))).ToList()));
    }
}
