using System.Collections.Generic;
using System.Linq;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Key = FrooxEngine.Key;

namespace EditorTabbing
{
    public class EditorTabbing : NeosMod
    {
        public static ModConfiguration Config;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> OverlayCompatibilityBackwardsMovement = new ModConfigurationKey<bool>("OverlayCompatibilityBackwardsMovement", "Moves forward with Enter when Steam Overlay could be enabled to not trigger it.", () => true);

        private static bool launchedInDesktop = false;
        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosEditorTabbing";
        public override string Name => "EditorTabbing";
        public override string Version => "1.2.0";
        private static bool SteamOverlayPossible => launchedInDesktop && !Engine.Current.TokensSupported;
        private static bool hasUnconfirmedImeInput = false;

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            Config = GetConfiguration();
            Config.Save(true);
            harmony.PatchAll();

            var outputDevice = Engine.Current.SystemInfo.HeadDevice;
            launchedInDesktop = outputDevice == HeadOutputDevice.Screen || outputDevice == HeadOutputDevice.Screen360 || outputDevice == HeadOutputDevice.LegacyScreen;

            Keyboard.current.onIMECompositionChange += OnIMECompositionChange;
        }

        private void OnIMECompositionChange(IMECompositionString compStr)
        {
            // when you confirm the candidate, an empty string comes in.
            hasUnconfirmedImeInput = compStr.Count != 0;
        }

        [HarmonyPatch(typeof(TextEditor))]
        internal static class TextEditorPatch
        {
            private static void changeFocus(TextEditor current, bool backwards)
            {
                var direction = backwards ? -1 : 1;
                var maxParent = getObjectRoot(current.Slot).Parent;

                var currentParent = current.Slot.Parent;
                var child = current.Slot.ChildIndex;

                while (currentParent != null && currentParent != maxParent)
                {
                    child += direction;

                    if (child < 0 || child >= currentParent.ChildrenCount)
                    {
                        child = currentParent.ChildIndex;
                        currentParent = currentParent.Parent;
                        continue;
                    }

                    var possibleEditors = currentParent[child].GetComponentsInChildren<TextEditor>();
                    var editor = backwards ? possibleEditors.LastOrDefault() : possibleEditors.FirstOrDefault();

                    if (editor != null)
                    {
                        editor.Focus();
                        editor.RunInUpdates(1, editor.SelectAll);

                        return;
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch("EditCoroutine")]
            private static void EditCoroutinePostfix(TextEditor __instance, ref IEnumerator<Context> __result)
            {
                __result = new EnumerableInjector<Context>(__result)
                {
                    // PostItem is after control has returned to the enumerator again,
                    // i.e. when there is an update - running before EditingRoutine checks it
                    PostItem = (item, returned) =>
                    {
                        if (SteamOverlayPossible && Config.GetValue(OverlayCompatibilityBackwardsMovement) && !__instance.InputInterface.GetKey(Key.Shift)
                            && (__instance.InputInterface.TypeDelta.Contains('\n') || __instance.InputInterface.TypeDelta.Contains('\r')))
                            __instance.RunInUpdates(1, () => changeFocus(__instance, false));

                        if (!hasUnconfirmedImeInput && __instance.InputInterface.GetKeyDown(Key.Tab))
                        {
                            __instance.Defocus();
                            changeFocus(__instance,
                                __instance.InputInterface.GetKey(Key.Shift) || (SteamOverlayPossible && Config.GetValue(OverlayCompatibilityBackwardsMovement)));
                        }
                    }
                }.GetEnumerator();
            }

            private static Slot getObjectRoot(Slot slot)
            {
                var implicitRoot = slot.GetComponentInParents<IImplicitObjectRoot>(null, true, false);
                var objectRoot = slot.GetObjectRoot();

                if (implicitRoot == null)
                    return objectRoot;

                if (objectRoot == slot || implicitRoot.Slot.HierachyDepth > objectRoot.HierachyDepth)
                    return implicitRoot.Slot;

                return objectRoot;
            }
        }
    }
}