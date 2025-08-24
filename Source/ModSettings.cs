using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace OpenedDoorsDontBlockLight
{
    public class OpenedDoorsDontBlockLightMod : Mod
    {
        private Vector2 _scroll = Vector2.zero;
        private float _viewHeight = 0f;

        // Buffers for ALL numeric inputs (so typing "12" doesn't clamp on "1")
        private string _bufUpdateInterval;



        // Use this from patches/components: OpenedDoorsDontBlockLightMod.Settings
        public static OpenedDoorsDontBlockLightSettings Settings { get; private set; }

        // Instance copy for the settings window code
        private OpenedDoorsDontBlockLightSettings _settings;

        public OpenedDoorsDontBlockLightMod(ModContentPack content) : base(content)
        {
            _settings = GetSettings<OpenedDoorsDontBlockLightSettings>();
            Settings = _settings; // expose static handle

            // init buffers from current values
            SyncBuffersFromSettings();

            try
            {
                var harmony = new Harmony("mlph.OpenedDoorsDontBlockLight.dev");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (System.Exception e)
            {
                Log.Error("[ODBL] PatchAll failed: " + e);
            }
        }

        private void SyncBuffersFromSettings()
        {
            _bufUpdateInterval = _settings.updateInterval.ToString();

        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, Mathf.Max(_viewHeight, inRect.height + 1f));
            Widgets.BeginScrollView(inRect, ref _scroll, viewRect, true);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width - 10f };
            listing.Begin(viewRect);

            // Dynamic lighting
            listing.CheckboxLabeled(
                "ODBL_enableDynamicLighting_label".Translate(),
                ref _settings.enableDynamicLighting,
                "ODBL_enableDynamicLighting_desc".Translate()
            );

            if (_settings.enableDynamicLighting)
            {
                listing.Gap(6f);
                listing.Label("ODBL_updateInterval_desc".Translate().Colorize(ColoredText.SubtleGrayColor));
                listing.Gap(2f);

                // Buffered text field (no clamping while typing)
                DrawLabeledTextEntry(listing,
                    "ODBL_updateInterval_label".Translate(),
                    ref _bufUpdateInterval,
                    200f);
            }
            listing.GapLine(6f);

            if (listing.ButtonText("ODBL_resetDefaults_button".Translate()))
            {
                _settings.enableDynamicLighting = true;
                _settings.updateInterval = 5;
                // refresh buffers so fields show updated defaults
                SyncBuffersFromSettings();
            }

            listing.End();
            Widgets.EndScrollView();

            _viewHeight = listing.CurHeight;

            // IMPORTANT: no clamping/parsing here — we apply in WriteSettings()
        }

        // Simple labeled text entry that doesn’t touch the actual setting while typing
        private static void DrawLabeledTextEntry(Listing_Standard listing, string label, ref string buffer, float labelWidth)
        {
            var rect = listing.GetRect(24f);
            var left = rect.LeftPartPixels(labelWidth);
            var right = rect.RightPartPixels(rect.width - labelWidth).ContractedBy(4f, 0f);

            Widgets.Label(left, label);
            buffer = Widgets.TextField(right, buffer ?? string.Empty);
        }

        public override string SettingsCategory() => "Opened Doors Don't Block Light";

        public override void WriteSettings()
        {
            // Parse & clamp on save/close only
            int tmpI;

            if (int.TryParse(_bufUpdateInterval, out tmpI))
                _settings.updateInterval = Mathf.Clamp(tmpI, 1, 60);

            base.WriteSettings();

            // Re-sync buffers so UI shows the applied values next open
            SyncBuffersFromSettings();
        }
    }

    public class OpenedDoorsDontBlockLightSettings : ModSettings
    {
        public int updateInterval = 5;
        public bool enableDynamicLighting = true;




        public override void ExposeData()
        {
            Scribe_Values.Look(ref updateInterval, "ODBL_updateInterval", 5);
            Scribe_Values.Look(ref enableDynamicLighting, "ODBL_enableDynamicLighting", true);
        }
    }
}