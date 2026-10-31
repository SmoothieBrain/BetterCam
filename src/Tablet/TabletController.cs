using System;
using System.Collections.Generic;
using System.Reflection;
using Alta.Meta.UI;
using Alta.QuickAccessActions;
using HarmonyLib;
using UnityEngine;
using MelonLoader;

namespace BetterCam.Tablet
{
    internal static class TabletController
    {
        static FieldInfo actionItemsField = AccessTools.Field(typeof(ActionItemsManager), "actionItems");
        static FieldInfo setupField = AccessTools.Field(typeof(TouchScreenMenuBase), "isSetupLocal");

        static TabletView view;
        static SocialTablet tablet;
        static float waitUntil = -1f;
        static bool summoning;
        static TabletMenu menu = new TabletMenu();
        static HashSet<SocialTablet> leaving = new HashSet<SocialTablet>();

        internal static bool IsOpen => view != null || waitUntil > 0f;

        internal static void Patch(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Window), "SwitchToPanel", new[] { typeof(Panel) }),
                prefix: new HarmonyMethod(typeof(TabletController), nameof(BeforeSwitch)));
            harmony.Patch(AccessTools.Method(typeof(Window), "FinishSwitch"),
                prefix: new HarmonyMethod(typeof(TabletController), nameof(BeforeFinish)));
            harmony.Patch(AccessTools.Method(typeof(SummonActionItemQuickAccess), "Run"),
                prefix: new HarmonyMethod(typeof(TabletController), nameof(BeforeSummon)));
            harmony.Patch(AccessTools.Method(typeof(SocialTablet), "StartFade"),
                postfix: new HarmonyMethod(typeof(TabletController), nameof(AfterFade)));
        }

        internal static void Toggle(Controller controller)
        {
            if (IsOpen)
            {
                bool wasShowing = view != null;
                Close();
                if (wasShowing && FindTablet() != null) Summon(controller);
                return;
            }

            if (FindTablet() == null && !Summon(controller)) return;
            waitUntil = Time.unscaledTime + 6f;
        }

        static bool Summon(Controller controller)
        {
            var summon = SocialTabletAction();
            if (summon == null)
            {
                MelonLogger.Warning("couldnt find the social tablet on the wheel");
                return false;
            }
            summoning = true;
            try { summon.Run(controller); }
            finally { summoning = false; }
            return true;
        }

        internal static void Close()
        {
            waitUntil = -1f;
            var v = view;
            view = null;
            tablet = null;
            if (v != null) v.Dispose();
        }

        internal static void Tick()
        {
            if (view != null)
            {
                if (tablet == null || !tablet.gameObject.activeInHierarchy || leaving.Contains(tablet))
                {
                    Close();
                    return;
                }
                menu.Tick(view);
                return;
            }

            if (waitUntil < 0f) return;
            if (Time.unscaledTime > waitUntil)
            {
                waitUntil = -1f;
                MelonLogger.Warning("the social tablet didnt come out so the camera settings cant open");
                return;
            }

            var t = FindTablet();
            if (t != null) Open(t);
        }

        static void Open(SocialTablet t)
        {
            waitUntil = -1f;
            tablet = t;
            view = TabletView.Create(t);
            if (view == null)
            {
                tablet = null;
                MelonLogger.Warning("camera settings couldnt open on the tablet");
                return;
            }

            try
            {
                view.Home = () => menu.ShowHome(view);
                var screen = t.GetComponentInChildren<TouchScreenMenuBase>(true);
                if (screen != null && !(bool)setupField.GetValue(screen)) screen.Setup(true);
                menu.ShowHome(view);
                view.Window.SwitchToPanel(view.Panel);
                MelonLogger.Msg("camera settings opened on the social tablet");
            }
            catch (Exception e)
            {
                Close();
                MelonLogger.Warning("camera settings couldnt open on the tablet " + e);
            }
        }

        static SocialTablet FindTablet()
        {
            leaving.RemoveWhere(x => x == null);
            foreach (var t in UnityEngine.Object.FindObjectsOfType<SocialTablet>())
            {
                if (t != null && t.IsLocalPlayer && t.gameObject.activeInHierarchy && !leaving.Contains(t))
                    return t;
            }
            return null;
        }

        static SummonActionItemQuickAccess SocialTabletAction()
        {
            if (ActionItemsManager.Instance == null) return null;
            var items = actionItemsField.GetValue(ActionItemsManager.Instance) as ActionItemData[];
            if (items == null) return null;
            foreach (var item in items)
            {
                if (item != null && item.Action != null && item.Action.DisplayName == "Social Tablet")
                    return item.Action;
            }
            return null;
        }

        static void AfterFade(SocialTablet __instance, bool isFadingIn)
        {
            if (__instance == null) return;
            if (isFadingIn) leaving.Remove(__instance);
            else leaving.Add(__instance);
        }

        static bool KeepOurPanel(Window window, ref Panel panel, bool finishing)
        {
            if (view == null || view.Window != window || view.Panel == null) return true;
            if (panel == view.Panel) return true;
            panel = view.Panel;
            return finishing || window.CurrentPanel != panel;
        }

        static bool BeforeSwitch(Window __instance, ref Panel panel)
        {
            return KeepOurPanel(__instance, ref panel, false);
        }

        static bool BeforeFinish(Window __instance, ref Panel panel)
        {
            return KeepOurPanel(__instance, ref panel, true);
        }

        static void BeforeSummon(SummonActionItemQuickAccess __instance)
        {
            if (!summoning && view != null && __instance != null && __instance.DisplayName == "Social Tablet")
                Close();
        }
    }
}
