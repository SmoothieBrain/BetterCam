using System;
using Alta.ScreenshotCamera;
using MelonLoader;
using BetterCam;
using TavernLib.ModdingApi;

[assembly: MelonInfo(typeof(Mod), "BetterCam", "2.1.0", "Smoothie")]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace BetterCam
{
    [TavernMod(ModSide.Client)]
    public sealed class Mod : MelonMod
    {
        internal static CameraLook Look;
        static bool dirty;

        public override void OnInitializeMelon()
        {
            Settings.Init();
            Settings.Changed += () => dirty = true;
            Tablet.Wheel.Patch(HarmonyInstance);
            Tablet.TabletController.Patch(HarmonyInstance);
        }

        public override void OnUpdate()
        {
            try
            {
                FollowCamera();
                if (dirty)
                {
                    dirty = false;
                    if (Look != null) Look.Rebuild();
                }
                Tablet.TabletController.Tick();
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }

        public override void OnDeinitializeMelon()
        {
            Settings.Save();
        }

        static void FollowCamera()
        {
            var cam = HandCamera.Current;
            if (cam != null && !cam.IsLocalPlayer) cam = null;

            if (Look != null && Look.Camera != cam)
            {
                Look.Dispose();
                Look = null;
            }
            if (Look == null && cam != null) Look = new CameraLook(cam);
        }
    }
}
