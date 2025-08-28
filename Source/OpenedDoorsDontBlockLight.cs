using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace OpenedDoorsDontBlockLight
{
    // NOTE: Patching now happens in OpenedDoorsDontBlockLightMod (Mod constructor).

    [HarmonyPatch(typeof(Building_Door), "DoorOpen")]
    static class DoorOpen_Patch
    {
        public static void Postfix(Building_Door __instance)
        {
            var glow = __instance.Map?.glowGrid;
            if (glow == null) return;

            // Open doors don't block light on ANY of their cells
            foreach (var c in Utility.DoorCells.Cells(__instance))
            {
                glow.LightBlockerRemoved(c);
                glow.DirtyCell(c);
            }

            // Optional: keep updating while animating
            if (OpenedDoorsDontBlockLightMod.Settings.enableDynamicLighting)
                DoorGlowGridManager.instance?.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(Building_Door), "DoorTryClose")]
    static class DoorTryClose_Patch
    {
        public static void Postfix(Building_Door __instance)
        {
            var glow = __instance.Map?.glowGrid;
            if (glow == null) return;

            // Closed doors block light on ALL occupied cells
            foreach (var c in Utility.DoorCells.Cells(__instance))
            {
                glow.LightBlockerAdded(c);
                glow.DirtyCell(c);
            }

            // Optional: keep updating while animating
            if (OpenedDoorsDontBlockLightMod.Settings.enableDynamicLighting)
                DoorGlowGridManager.instance?.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.SpawnSetup))]
    static class Door_SpawnSetup_Patch
    {
        static void Postfix(Building_Door __instance, Map map, bool respawningAfterLoad)
        {
            try
            {
                if (map == null || __instance == null || !__instance.Spawned) return;
                var glow = map.glowGrid;
                if (glow == null) return;

                if (__instance.Open)
                {
                    foreach (var c in Utility.DoorCells.Cells(__instance))
                    {
                        glow.LightBlockerRemoved(c);
                        glow.DirtyCell(c);
                    }
                }
                else
                {
                    foreach (var c in Utility.DoorCells.Cells(__instance))
                    {
                        glow.LightBlockerAdded(c);
                        glow.DirtyCell(c);
                    }
                }

                if (OpenedDoorsDontBlockLightMod.Settings.enableDynamicLighting && __instance.IsMoving())
                    DoorGlowGridManager.instance?.Add(__instance);
            }
            catch (System.Exception e)
            {
                Log.Error("[ODBL] SpawnSetup postfix failed: " + e);
            }
        }
    }


    // ---------------------------
    // NO GlowGrid postfix anymore
    // ---------------------------

    internal class DoorGlowGridManager : GameComponent
    {
        private readonly List<Building_Door> movingDoors = new List<Building_Door>();
        public static DoorGlowGridManager instance;

        public DoorGlowGridManager(Game _)
        {
            instance = this;
        }

        public override void GameComponentTick()
        {
            // Only used for dynamic lighting while doors animate
            if (!OpenedDoorsDontBlockLightMod.Settings.enableDynamicLighting) return;

            // Reasonable throttle so we’re not dirtying every tick unnecessarily
            int interval = Mathf.Clamp(OpenedDoorsDontBlockLightMod.Settings.updateInterval, 1, 60);
            if (Find.TickManager.TicksGame % interval != 0) return;

            for (int i = movingDoors.Count - 1; i >= 0; i--)
            {
                var door = movingDoors[i];
                if (door == null || !door.Spawned)
                {
                    movingDoors.RemoveAt(i);
                    continue;
                }

                var glow = door.Map?.glowGrid;
                if (glow != null)
                {
                    // Dirty every occupied cell, not just Position
                    foreach (var c in Utility.DoorCells.Cells(door))
                        glow.DirtyCell(c);
                }

                if (!door.IsMoving())
                    movingDoors.RemoveAt(i);
            }
        }

        public void Add(Building_Door door)
        {
            if (door != null && !movingDoors.Contains(door))
                movingDoors.Add(door);
        }

        public bool Contains(Building_Door door) => movingDoors.Contains(door);
    }

    public static class Utility
    {
        private static readonly Func<Building_Door, int> ticksSinceOpen;

        static Utility()
        {
            var f = AccessTools.Field(typeof(Building_Door), "ticksSinceOpen");
            ticksSinceOpen = (door) => (int)f.GetValue(door);
        }

        public static int Get_ticksSinceOpen(this Building_Door door)
        {
            return ticksSinceOpen(door);
        }

        public static bool IsMoving(this Building_Door door)
        {
            int visualTicksOpen = door.Get_ticksSinceOpen();
            if (!door.Open) return visualTicksOpen > 0;
            return visualTicksOpen < door.TicksToOpenNow;
        }

        internal static class DoorCells
        {
            public static IEnumerable<IntVec3> Cells(Building_Door door)
            {
                if (door == null || !door.Spawned) yield break;
                foreach (var c in GenAdj.CellsOccupiedBy(door)) yield return c;
            }
        }
    }
}
