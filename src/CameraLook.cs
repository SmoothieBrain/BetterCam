using System;
using System.Linq;
using System.Reflection;
using Alta.ScreenshotCamera;
using HarmonyLib;
using MelonLoader;
using Township.PostProcessing;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace BetterCam
{
    internal sealed class CameraLook : IDisposable
    {
        static FieldInfo frontField = AccessTools.Field(typeof(HandCamera), "frontCamera");
        static FieldInfo selfieField = AccessTools.Field(typeof(HandCamera), "selfieCamera");
        static FieldInfo filterVolumeField = AccessTools.Field(typeof(FiltersButton), "targetVolume");

        internal readonly HandCamera Camera;
        GameObject obj;
        PostProcessVolume volume;
        PostProcessProfile profile;
        PostProcessLayer[] lenses = new PostProcessLayer[2];
        Transform[] oldTriggers = new Transform[2];
        bool movedTriggers;

        internal CameraLook(HandCamera cam)
        {
            Camera = cam;
            lenses[0] = GetLens((BlockableCamera)frontField.GetValue(cam));
            lenses[1] = GetLens((BlockableCamera)selfieField.GetValue(cam));

            int lensMask = lenses[0] != null ? lenses[0].volumeLayer.value : 0;
            int headMask = 0;
            var head = PlayerController.Current != null ? PlayerController.Current.Camera : UnityEngine.Camera.main;
            if (head != null && head.GetComponent<PostProcessLayer>() != null)
                headMask = head.GetComponent<PostProcessLayer>().volumeLayer.value;

            int gameLayer = -1;
            var filters = cam.GetComponentsInChildren<FiltersButton>(true).FirstOrDefault();
            if (filters != null)
            {
                var gameVolume = (PostProcessVolume)filterVolumeField.GetValue(filters);
                if (gameVolume != null) gameLayer = gameVolume.gameObject.layer;
            }

            bool onGameLayer = gameLayer >= 0 && (lensMask & (1 << gameLayer)) != 0 && (headMask & (1 << gameLayer)) == 0;

            obj = new GameObject("BetterCam look");
            UnityEngine.Object.DontDestroyOnLoad(obj);
            obj.SetActive(false);
            volume = obj.AddComponent<PostProcessVolume>();
            volume.priority = 1000f;

            if (onGameLayer)
            {
                obj.layer = gameLayer;
                volume.isGlobal = true;
            }
            else
            {
                obj.layer = FirstLayer(lensMask);
                obj.transform.position = new Vector3(0f, -30000f, 0f);
                var box = obj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(10f, 10f, 10f);
                volume.isGlobal = false;

                for (int i = 0; i < 2; i++)
                {
                    if (lenses[i] == null) continue;
                    oldTriggers[i] = lenses[i].volumeTrigger;
                    lenses[i].volumeTrigger = obj.transform;
                }
                movedTriggers = true;
            }

            obj.SetActive(true);
            Rebuild();
            MelonLogger.Msg("hand camera ready" + (onGameLayer ? " (layer " + gameLayer + ")" : " (far volume)"));
        }

        static PostProcessLayer GetLens(BlockableCamera b)
        {
            if (b == null) return null;
            if (b.PostProcessLayer != null) return b.PostProcessLayer;
            if (b.TargetCamera != null) return b.TargetCamera.GetComponent<PostProcessLayer>();
            return null;
        }

        static int FirstLayer(int mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0) return i;
            return 0;
        }

        internal void Rebuild()
        {
            var old = profile;
            profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            profile.hideFlags = HideFlags.DontSave;

            var grade = Add<ColorGrading>();
            grade.gradingMode.Override(GradingMode.HighDefinitionRange);
            grade.tonemapper.Override(Tonemapper.ACES);
            grade.postExposure.Override(Settings.Exposure.Value);
            grade.contrast.Override(Settings.Contrast.Value);
            grade.saturation.Override(Settings.Saturation.Value);
            grade.temperature.Override(Settings.Temperature.Value);
            grade.tint.Override(Settings.Tint.Value);
            grade.hueShift.Override(Settings.Hue.Value);
            grade.colorFilter.Override(Settings.ToneColor(Settings.Filter.Value));

            if (Settings.Bloom.Value > 0f)
            {
                var bloom = Add<Bloom>();
                bloom.intensity.Override(Settings.Bloom.Value);
                bloom.threshold.Override(Settings.BloomThreshold.Value);
                bloom.softKnee.Override(0.5f);
            }
            if (Settings.Vignette.Value > 0f)
            {
                var vig = Add<Vignette>();
                vig.intensity.Override(Settings.Vignette.Value);
                vig.smoothness.Override(0.4f);
            }
            if (Settings.Grain.Value > 0f)
                Add<Grain>().intensity.Override(Settings.Grain.Value);
            if (Settings.Chromatic.Value > 0f)
                Add<ChromaticAberration>().intensity.Override(Settings.Chromatic.Value);
            if (Mathf.Abs(Settings.Distortion.Value) > 0.5f)
                Add<LensDistortion>().intensity.Override(Settings.Distortion.Value);
            if (Settings.BlurCorners.Value > 0f)
            {
                var blur = Add<BlurredCorners>();
                blur.blurIntensity.Override(Settings.BlurCorners.Value);
                blur.blurSmoothness.Override(2f);
            }

            volume.sharedProfile = profile;
            volume.enabled = Settings.On.Value;
            if (old != null) UnityEngine.Object.Destroy(old);
        }

        T Add<T>() where T : PostProcessEffectSettings
        {
            var s = profile.AddSettings<T>();
            s.enabled.Override(true);
            return s;
        }

        // dispose the shit bro
        public void Dispose()
        {
            if (movedTriggers)
            {
                for (int i = 0; i < 2; i++)
                    if (lenses[i] != null) lenses[i].volumeTrigger = oldTriggers[i];
            }
            if (obj != null) UnityEngine.Object.Destroy(obj);
            if (profile != null) UnityEngine.Object.Destroy(profile);
        }
    }
}
