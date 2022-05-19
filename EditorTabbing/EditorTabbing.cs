using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Data;
using FrooxEngine.LogiX.ProgramFlow;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;

namespace EditorTabbing
{
    public class EditorTabbing : NeosMod
    {
        public static ModConfiguration Config;

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> EnableBackwardsMovement = new ModConfigurationKey<bool>("EnableBackwardsMovement", "Moves forward with Enter and backwards with Tab when enabled. Only forward with Tab when disabled.", () => false);

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosEditorTabbing";
        public override string Name => "EditorTabbing";
        public override string Version => "1.0.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            Config = GetConfiguration();
            Config.Save(true);
            harmony.PatchAll();
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
                        if (Config.GetValue(EnableBackwardsMovement) && !__instance.InputInterface.GetKey(Key.Shift)
                            && (__instance.InputInterface.TypeDelta.Contains('\n') || __instance.InputInterface.TypeDelta.Contains('\r')))
                            __instance.RunInUpdates(1, () => changeFocus(__instance, false));

                        if (__instance.InputInterface.GetKeyDown(Key.Tab))
                        {
                            __instance.Defocus();
                            changeFocus(__instance, Config.GetValue(EnableBackwardsMovement));
                        }
                    }
                }.GetEnumerator();
            }

            private static Slot getObjectRoot(Slot slot)
            {
                var implicitRoot = slot.GetComponentInParents<IImplicitObjectRoot>(null, true, false);
                var objectRoot = slot.GetObjectRoot();

                if (implicitRoot == null)
                {
                    return objectRoot;
                }

                if (objectRoot == slot)
                {
                    return implicitRoot.Slot;
                }

                if (implicitRoot.Slot.HierachyDepth > objectRoot.HierachyDepth)
                {
                    return implicitRoot.Slot;
                }

                return objectRoot;
            }
        }
    }
}