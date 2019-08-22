﻿using BepInEx.Harmony;
using BepInEx.Logging;
using Common;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using System;
using UnityEngine;

namespace IllusionFixes
{
    public partial class ResourceUnloadOptimizations
    {
        private static class Hooks
        {
            private static AsyncOperation _currentOperation;
            private static Func<AsyncOperation> _originalUnload;

            public static void InstallHooks()
            {
                var target = AccessTools.Method(typeof(Resources), nameof(Resources.UnloadUnusedAssets));
                var replacement = AccessTools.Method(typeof(Hooks), nameof(UnloadUnusedAssetsHook));

                var detour = new NativeDetour(target, replacement);
                detour.Apply();

                _originalUnload = detour.GenerateTrampoline<Func<AsyncOperation>>();

                HarmonyWrapper.PatchAll(typeof(Hooks));
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(GC), nameof(GC.Collect), new Type[0])]
            public static bool GCCollectHook()
            {
                try
                {
                    // Throttle down the calls
                    _instance.CancelInvoke(nameof(RunGarbageCollect));
                    _instance.Invoke(nameof(RunGarbageCollect), 3f);
                    // Disable the original method, Invoke will call it later
                    return false;
                }
                // Sometimes there are errors in HoneySelect with IPA installed during scene transitions. Catching them and doing nothing seems harmless, uncaught errors break the game.
                catch { }
                return true;
            }

            // Replacement methods needs to be inside a static class to be used in NativeDetour
            private static AsyncOperation UnloadUnusedAssetsHook()
            {
                if (_currentOperation == null || _currentOperation.isDone)
                {
                    Utilities.Logger.Log(LogLevel.Debug, "Starting unused resource cleanup");
                    _currentOperation = _originalUnload();
                }

                return _currentOperation;
            }
        }
    }
}