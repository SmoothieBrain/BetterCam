using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Alta.QuickAccessActions;

namespace BetterCam.Tablet
{
    internal enum WheelCommand { Settings, Toggle }

    internal sealed class WheelAction : QuickAccessMenuAction
    {
        internal WheelCommand Command;

        public override bool IsValid => Player.Current != null;
        public override bool IsIgnored => !IsValid || base.IsIgnored;
        public override bool IsActive => Command == WheelCommand.Settings && TabletController.IsOpen;

        public override void Run(Controller controller)
        {
            try
            {
                if (Command == WheelCommand.Settings)
                {
                    TabletController.Toggle(controller);
                }
                else
                {
                    Settings.On.Value = !Settings.On.Value;
                    Settings.Save();
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("wheel: " + e.Message);
            }
        }
    }

    internal static class Wheel
    {
        static FieldInfo menusField = AccessTools.Field(typeof(QuickAccessMenuController), "quickAccessMenus");
        static FieldInfo nestedField = AccessTools.Field(typeof(NestingMenuAction), "nestedActions");
        static FieldInfo bubblesField = AccessTools.Field(typeof(NestingMenuAction), "spawnedBubbles");
        static FieldInfo prefabField = AccessTools.Field(typeof(QuickAccessMenuAction), "prefab");
        static FieldInfo nameField = AccessTools.Field(typeof(QuickAccessMenuAction), "displayName");

        const string RootName = "BetterCam";

        static Transform holder;
        static HashSet<int> done = new HashSet<int>();
        static HashSet<NestingMenuAction> ours = new HashSet<NestingMenuAction>();
        static Dictionary<string, GameObject> icons = new Dictionary<string, GameObject>();

        internal static void Patch(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(QuickAccessMenuController), "InitializeControllerExtension"),
                postfix: new HarmonyMethod(typeof(Wheel), nameof(AfterInit)));
            harmony.Patch(AccessTools.Method(typeof(NestingMenuAction), "SpawnNestedBubbles"),
                postfix: new HarmonyMethod(typeof(Wheel), nameof(AfterSpawn)));
        }

        static Transform Holder()
        {
            if (holder == null)
            {
                var go = new GameObject("BetterCam wheel icons");
                go.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(go);
                holder = go.transform;
            }
            return holder;
        }

        static void AfterInit(QuickAccessMenuController __instance)
        {
            try
            {
                AddBubble(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("couldnt add the bettercam bubble: " + e.Message);
            }
        }

        static void AddBubble(QuickAccessMenuController controller)
        {
            if (controller == null) return;
            int id = controller.GetInstanceID();
            if (done.Contains(id)) return;

            var menus = (QuickAccessMenuAction[])menusField.GetValue(controller);
            if (menus == null || menus.Length == 0) return;
            done.Add(id);
            if (menus.Any(m => m != null && m.DisplayName == RootName)) return;

            var templates = menus.Where(m => m != null).ToArray();
            var nest = templates.OfType<NestingMenuAction>().FirstOrDefault();
            if (nest == null)
            {
                MelonLogger.Warning("no submenu shit to copy the bettercam bubble is off");
                return;
            }

            var root = UnityEngine.Object.Instantiate(nest);
            Setup(root, templates[0], RootName, "CAM", GameIcon("Camera", null));

            var settings = MakeAction(templates[0], "cam settings", "SET", WheelCommand.Settings);
            var gear = GameIcon("Server Settings", "font_");
            if (gear != null) prefabField.SetValue(settings, gear);
            var toggle = MakeAction(templates[1 % templates.Length], "effects on/off", "ON", WheelCommand.Toggle);

            nestedField.SetValue(root, new QuickAccessMenuAction[] { settings, toggle });
            bubblesField.SetValue(root, new List<QuickAccessMenuBubble>());
            ours.Add(root);

            var list = menus.ToList();
            list.Add(root);
            menusField.SetValue(controller, list.ToArray());
            MelonLogger.Msg("added cam bubble to radial menu");
        }

        static WheelAction MakeAction(QuickAccessMenuAction template, string name, string label, WheelCommand command)
        {
            var action = ScriptableObject.CreateInstance<WheelAction>();
            action.Command = command;
            Setup(action, template, name, label, null);
            return action;
        }

        static void Setup(QuickAccessMenuAction action, QuickAccessMenuAction template, string name, string label, GameObject icon)
        {
            var prefab = (UnityEngine.Object)prefabField.GetValue(template);
            if (icon != null) prefabField.SetValue(action, icon);
            else prefabField.SetValue(action, TextIcon(prefab, label) ?? prefab);
            nameField.SetValue(action, name);
        }

        static GameObject GameIcon(string name, string hidePrefix)
        {
            if (icons.TryGetValue(name, out var have) && have != null) return have;

            GameObject source = null;
            foreach (var a in Resources.FindObjectsOfTypeAll<QuickAccessMenuAction>())
            {
                if (a == null || a.DisplayName != name) continue;
                source = prefabField.GetValue(a) as GameObject;
                if (source != null) break;
            }
            if (source == null)
            {
                MelonLogger.Warning("no " + name + " icon in the game, using text");
                return null;
            }

            var copy = UnityEngine.Object.Instantiate(source, Holder(), false);
            copy.name = "BetterCam " + name + " icon";
            if (hidePrefix != null)
            {
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.name.StartsWith(hidePrefix)) r.gameObject.SetActive(false);
            }
            icons[name] = copy;
            return copy;
        }

        static UnityEngine.Object TextIcon(UnityEngine.Object template, string label)
        {
            if (template == null) return null;
            var copy = UnityEngine.Object.Instantiate(template);
            GameObject go = copy as GameObject;
            if (go == null && copy is Component comp) go = comp.gameObject;
            if (go == null) return null;

            go.transform.SetParent(Holder(), false);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var textObj = new GameObject("BetterCam icon");
            textObj.transform.SetParent(go.transform, false);
            textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = textObj.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 64;
            text.characterSize = label.Length > 3 ? 0.01f : 0.0125f;
            text.color = Color.white;
            return copy;
        }

        static void AfterSpawn(NestingMenuAction __instance, Vector3 __0)
        {
            if (!ours.Contains(__instance)) return;
            var bubbles = bubblesField.GetValue(__instance) as List<QuickAccessMenuBubble>;
            if (bubbles == null) return;
            foreach (var b in bubbles)
            {
                if (b == null) continue;
                b.transform.position = __0 + (b.transform.position - __0) * 1.65f;
            }
        }
    }
}
