using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MelonLoader;

namespace BetterCam.Tablet
{
    internal sealed class TabletMenu
    {
        class Slider
        {
            public string Name, Help;
            public Func<MelonPreferences_Entry<float>> Entry;
            public float Min, Max, Step, Default;
            public Func<float, string> Format;

            public Slider(string name, string help, Func<MelonPreferences_Entry<float>> entry, float min, float max, float step, float def, Func<float, string> format)
            {
                Name = name;
                Help = help;
                Entry = entry;
                Min = min;
                Max = max;
                Step = step;
                Default = def;
                Format = format;
            }
        }

        static Slider[] sliders =
        {
            new Slider("Post exposure", "Brighter or darker, in stops.", () => Settings.Exposure, -2f, 2f, 0.25f, 0f, v => v.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + " EV"),
            new Slider("Contrast", "Punchier or flatter.", () => Settings.Contrast, -100f, 100f, 10f, 0f, Signed),
            new Slider("Saturation", "More colour or less. All the way down is black and white.", () => Settings.Saturation, -100f, 100f, 10f, 0f, Signed),
            new Slider("Temperature", "Warmer (orange) or cooler (blue).", () => Settings.Temperature, -100f, 100f, 10f, 0f, Signed),
            new Slider("Colour tint", "Towards magenta or towards green.", () => Settings.Tint, -100f, 100f, 10f, 0f, Signed),
            new Slider("Hue shift", "Turns every colour round the colour wheel.", () => Settings.Hue, -180f, 180f, 15f, 0f, v => Signed(v) + " deg"),
            new Slider("Bloom", "Glow around bright things.", () => Settings.Bloom, 0f, 10f, 0.5f, 0f, OneDecimal),
            new Slider("Bloom threshold", "How bright something has to be to glow.", () => Settings.BloomThreshold, 0f, 2f, 0.1f, 0.9f, OneDecimal),
            new Slider("Vignette", "Darker corners.", () => Settings.Vignette, 0f, 1f, 0.1f, 0f, Percent),
            new Slider("Film grain", "Noise like film.", () => Settings.Grain, 0f, 1f, 0.1f, 0f, Percent),
            new Slider("Chromatic aberration", "Colour fringes towards the edges.", () => Settings.Chromatic, 0f, 1f, 0.1f, 0f, Percent),
            new Slider("Lens distortion", "Bulge out (fisheye) or pinch in.", () => Settings.Distortion, -100f, 100f, 10f, 0f, Signed),
            new Slider("Blurred corners", "Soft focus away from the middle.", () => Settings.BlurCorners, 0f, 5f, 0.5f, 0f, OneDecimal),
        };

        TabletView view;
        Func<Page> current;
        float nextRefresh;

        internal void ShowHome(TabletView v)
        {
            view = v;
            Go(Home);
        }

        internal void Tick(TabletView v)
        {
            if (current == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            v.Show(current(), true);
        }

        void Go(Func<Page> page)
        {
            if (view == null) return;
            current = page;
            nextRefresh = Time.unscaledTime + 1f;
            view.Show(page(), false);
        }

        void Redraw()
        {
            if (view == null || current == null) return;
            view.Show(current(), true);
            Settings.Save();
        }

        Page Home()
        {
            var page = new Page { Title = "BETTERCAM", Status = "Your camera settings" };
            page.Add("Effects", Settings.On.Value ? "On" : "Off", () =>
            {
                Settings.On.Value = !Settings.On.Value;
                Redraw();
            });

            foreach (var s in sliders)
            {
                var slider = s;
                page.Add(slider.Name, slider.Format(slider.Entry().Value), () => Go(() => SliderPage(slider)));
                if (slider.Name == "Hue shift")
                    page.Add("Colour filter", Settings.Filter.Value.ToString(), () => Go(FilterPage));
            }

            page.Add("Reset", "All", () =>
            {
                foreach (var s in sliders) s.Entry().Value = s.Default;
                Settings.Filter.Value = Tone.None;
                Redraw();
            });
            return page;
        }

        Page SliderPage(Slider s)
        {
            var entry = s.Entry();
            var page = new Page
            {
                Title = s.Name.ToUpperInvariant(),
                Status = s.Format(entry.Value) + "   " + Bar(entry.Value, s.Min, s.Max),
                Footer = s.Help,
                Back = () => Go(Home),
            };
            string step = s.Format(s.Step).TrimStart('+');
            page.Add("More", "+" + step, () => Nudge(s, s.Step));
            page.Add("Less", "-" + step, () => Nudge(s, -s.Step));
            page.Add("A lot more", null, () => Nudge(s, s.Step * 4f));
            page.Add("Reset", s.Format(s.Default), () =>
            {
                entry.Value = s.Default;
                Redraw();
            });
            return page;
        }

        void Nudge(Slider s, float amount)
        {
            var entry = s.Entry();
            float half = s.Step / 2f;
            float v = Mathf.Clamp(entry.Value + amount, s.Min, s.Max);
            entry.Value = Mathf.Round(v / half) * half;
            Redraw();
        }

        Page FilterPage()
        {
            var page = new Page { Title = "COLOUR FILTER", Status = "A colour over the whole picture.", Back = () => Go(Home) };
            foreach (Tone tone in Enum.GetValues(typeof(Tone)))
            {
                var t = tone;
                string label = t == Settings.Filter.Value ? "> " + t : t.ToString();
                page.Add(label, null, () =>
                {
                    Settings.Filter.Value = t;
                    Redraw();
                });
            }
            return page;
        }

        static string Percent(float v)
        {
            return Mathf.RoundToInt(v * 100f) + "%";
        }

        static string Signed(float v)
        {
            return v.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        }

        static string OneDecimal(float v)
        {
            return v.ToString("0.0", CultureInfo.InvariantCulture);
        }

        static string Bar(float v, float min, float max)
        {
            int filled = Mathf.RoundToInt(Mathf.InverseLerp(min, max, v) * 10f);
            return "[" + new string('|', filled) + new string('.', 10 - filled) + "]";
        }
    }
}
