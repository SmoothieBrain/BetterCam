using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;

namespace BetterCam
{
    public enum Tone { None, Warm, Cool, Rose, Gold, Teal, Mint, Violet, Red, Blue }


    // hand camera settings, saved in melonpreferences
    internal class Settings
    {
        internal static MelonPreferences_Entry<bool> On;
        internal static MelonPreferences_Entry<float> Exposure, Contrast, Saturation, Temperature, Tint, Hue, Bloom, BloomThreshold, Vignette, Grain, Chromatic, Distortion, BlurCorners;
        internal static MelonPreferences_Entry<Tone> Filter;

        internal static event Action Changed;

        internal static void Init()
        {
            var c = MelonPreferences.CreateCategory("BetterCam", "BetterCam");
            On = c.CreateEntry("on", true, "effects on");
            Exposure = Num(c, "exposure", "Post exposure", 0f, -2f, 2f);
            Contrast = Num(c, "contrast", "Contrast", 0f, -100f, 100f);
            Saturation = Num(c, "saturation", "Saturation", 0f, -100f, 100f);
            Temperature = Num(c, "temperature", "Temperature", 0f, -100f, 100f);
            Tint = Num(c, "tint", "Colour tint", 0f, -100f, 100f);
            Hue = Num(c, "hue", "Hue shift", 0f, -180f, 180f);
            Filter = c.CreateEntry("color_filter", Tone.None, "Colour filter");
            Bloom = Num(c, "bloom", "Bloom", 0f, 0f, 10f);
            BloomThreshold = Num(c, "bloom_threshold", "Bloom threshold", 0.9f, 0f, 2f);
            Vignette = Num(c, "vignette", "Vignette", 0f, 0f, 1f);
            Grain = Num(c, "grain", "Film grain", 0f, 0f, 1f);
            Chromatic = Num(c, "chromatic", "Chromatic aberration", 0f, 0f, 1f);
            Distortion = Num(c, "distortion", "Lens distortion", 0f, -100f, 100f);
            BlurCorners = Num(c, "blur_corners", "Blurred corners", 0f, 0f, 5f);

            foreach (var e in c.Entries)
                e.OnEntryValueChangedUntyped.Subscribe((a, b) => Changed?.Invoke());
        }

        static MelonPreferences_Entry<float> Num(MelonPreferences_Category c, string id, string name, float def, float min, float max)
        {
            return c.CreateEntry(id, def, name, validator: new ValueRange<float>(min, max));
        }

        //return the fucking color shit idk bro i hate my life
        internal static Color ToneColor(Tone tone)
        {
            switch (tone)
            {
                case Tone.Warm: return new Color(1f, 0.88f, 0.72f);
                case Tone.Cool: return new Color(0.78f, 0.9f, 1f);
                case Tone.Rose: return new Color(1f, 0.8f, 0.86f);
                case Tone.Gold: return new Color(1f, 0.86f, 0.5f);
                case Tone.Teal: return new Color(0.62f, 0.95f, 0.92f);
                case Tone.Mint: return new Color(0.78f, 1f, 0.82f);
                case Tone.Violet: return new Color(0.84f, 0.74f, 1f);
                case Tone.Red: return new Color(1f, 0.6f, 0.58f);
                case Tone.Blue: return new Color(0.6f, 0.72f, 1f);
                default: return Color.white;
            }
        }

        // save the shit
        internal static void Save()
        {
            try { MelonPreferences.Save(); }
            catch (Exception e) { MelonLogger.Warning("couldnt save settings " + e.Message); }
        }
    }
}
