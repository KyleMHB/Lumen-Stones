using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace LumenStones;

// Right-clicking the Lumenstone or Lunite ritual command toggles auto-start. A host with auto-start on begins that
// ceremony with a leader and up to two others as soon as nothing blocks it: the settlement cooldown has
// ended, the chunks or Sunstone are there, Lunite's night window is open and no hostile threat is on the map.
public class Command_AutoRitual : Command_Action
{
    private readonly Func<bool> isOn;
    private readonly Action<bool> set;
    private readonly string ritualLabel;

    public Command_AutoRitual(string ritualLabel, Func<bool> isOn, Action<bool> set)
    {
        this.ritualLabel = ritualLabel;
        this.isOn = isOn;
        this.set = set;
    }

    public override string DescPostfix => isOn()
        ? "\n\n" + "Lumen_AutoRitual_On".Translate()
        : "\n\n" + "Lumen_AutoRitual_Off".Translate();

    public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
    {
        get
        {
            bool on = isOn();
            yield return new FloatMenuOption((on ? "Lumen_AutoRitual_Stop" : "Lumen_AutoRitual_Start").Translate(ritualLabel), () =>
            {
                set(!on);
                Messages.Message((on ? "Lumen_AutoRitual_TurnedOff" : "Lumen_AutoRitual_TurnedOn").Translate(ritualLabel), MessageTypeDefOf.SilentInput, false);
            });
        }
    }

    // Vanilla ignores right-clicks on a disabled command, and these commands are disabled for the whole cooldown, which is
    // exactly when auto-start is worth setting. Open the menu here instead, before the button sees the mouse-down.
    protected override GizmoResult GizmoOnGUIInt(Rect butRect, GizmoRenderParms parms)
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(butRect))
        {
            Event.current.Use();
            Find.WindowStack.Add(new FloatMenu(RightClickFloatMenuOptions.ToList()));
        }
        return base.GizmoOnGUIInt(butRect, parms);
    }

    public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
    {
        base.DrawIcon(rect, buttonMat, parms);
        if (isOn()) GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, 20f, 20f), Widgets.CheckboxOnTex);
    }
}

public class AutoRitualState : MapComponent
{
    private const int Interval = GenTicks.TickRareInterval, MaxSpectators = 2;

    public AutoRitualState(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (!ModsConfig.IdeologyActive || (Find.TickManager.TicksGame + map.uniqueID) % Interval != 0) return;
        foreach (var host in map.listerBuildings.AllBuildingsColonistOfClass<Building_LumenstoneRitualHost>().ToList())
        {
            if (host.AutoLumenstone && TryStart(host, host.LumenstonePrecept, "Lumen_AutoRitual_Lumenstone".Translate())) return;
            if (host.AutoLunite && TryStart(host, host.LunitePrecept, "Lumen_AutoRitual_Lunite".Translate())) return;
        }
    }

    // True when the ceremony started. Silent otherwise: it is checked again on the next rare tick.
    private static bool TryStart(Building_LumenstoneRitualHost host, string precept, string label)
    {
        if (GenHostility.AnyHostileActiveThreatToPlayer(host.Map, true)) return false;
        // Lumen's own checks only see Lumen ceremonies; an Attunement at this altar also occupies it.
        if (Find.IdeoManager.GetActiveRituals(host.Map).Any(r => r.selectedTarget.Thing == host)) return false;
        var ritual = AttunementRules.Ritual(precept);
        if (ritual == null || ritual.behavior.CanStartRitualNow(host, ritual) != null) return false;
        // Only pawns the player wouldn't mind losing for an hour or two: awake, healthy, not on orders, not in another gathering.
        Dialog_BeginRitual.PawnFilter filter = (pawn, voluntary, allowOtherIdeos) =>
            !pawn.Drafted && !pawn.Downed && !pawn.InMentalState && !pawn.IsPrisoner && pawn.Awake() &&
            !HealthAIUtility.ShouldSeekMedicalRest(pawn) && pawn.CurJob?.playerForced != true && pawn.GetLord() == null;
        var assignments = Dialog_BeginRitual.CreateRitualRoleAssignments(ritual, host, host.Map, filter, null, null, null);
        assignments.FillPawns(filter, host);
        if (ritual.behavior.def.roles.Any(r => r.required && assignments.FirstAssignedPawn(r) == null)) return false;
        // The dialog's defaults pull in every free colonist. Leader plus two others already gives the full Lumenstone yield,
        // and Lunite's yield is fixed, so the rest stay at work.
        foreach (var extra in assignments.SpectatorsForReading.Skip(MaxSpectators).ToList()) assignments.RemoveParticipant(extra);
        ritual.behavior.TryExecuteOn(host, null, ritual, null, assignments, true);
        Messages.Message("Lumen_AutoRitual_Started".Translate(label, host.LabelShort), host, MessageTypeDefOf.NeutralEvent, false);
        return true;
    }
}
