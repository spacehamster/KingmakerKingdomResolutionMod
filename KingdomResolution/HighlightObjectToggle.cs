// Copyright (c) 2018 fireundubh <fireundubh@gmail.com>
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using Kingmaker.Controllers;
using Kingmaker.Controllers.MapObjects;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.SettingsUI;
using Kingmaker.Utility;
using UnityEngine;
using static Kingmaker.UI.KeyboardAccess;

namespace KingdomResolution
{
    /*
     * Note:
     * - Disable HighlightOff keybinding
     * - Uses m_IsHighlighting to track status
     * - 
     * 
     */
    class HighlightObjectToggle
    {
        static private void ToggleHighlight(InteractionHighlightController __instance)
        {
            var m_InactiveRef = Harmony12.AccessTools.FieldRefAccess<InteractionHighlightController, bool>("m_Inactive");
            var m_IsHighlightingRef = Harmony12.AccessTools.FieldRefAccess<InteractionHighlightController, bool>("m_IsHighlighting");
            if (m_InactiveRef(__instance))
            {
                return;
            }

            m_IsHighlightingRef(__instance) = !m_IsHighlightingRef(__instance);

            UpdateHighlights();
            EventBus.RaiseEvent((IInteractionHighlightUIHandler h) => h.HandleHighlightChange(m_IsHighlightingRef(__instance)));
        }
        private static void UpdateHighlights(bool raiseEvent = false)
        {
            foreach (MapObjectEntityData mapObjectEntityData in Kingmaker.Game.Instance.State.MapObjects)
            {
                mapObjectEntityData.View.UpdateHighlight();
            }

            foreach (UnitEntityData unitEntityData in Kingmaker.Game.Instance.State.Units)
            {
                unitEntityData.View.UpdateHighlight(raiseEvent);
            }
        }
        static TimeSpan m_LastTickTime;
        [Harmony12.HarmonyPatch(typeof(InteractionHighlightController), "Activate")]
        class InteractionHighlightController_Activate_Patch
        {
            static bool Prefix(InteractionHighlightController __instance, bool ___m_Inactive, ref bool ___m_IsHighlighting)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.highlightObjectsToggle) return true;
                    Main.DebugLog("Activating Highlighting");
                    Action callback = () =>
                    {
                        Main.DebugLog("Activating Highlighting callback ToggleHighlight");
                        ToggleHighlight(__instance);
                    };
                    Kingmaker.Game.Instance.Keyboard.Bind(SettingsRoot.Instance.HighlightObjects.name + UIConsts.SuffixOn, callback);
                    return false;
                } catch(Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
        [Harmony12.HarmonyPatch(typeof(InteractionHighlightController), "Deactivate")]
        class InteractionHighlightController_Deactivate_Patch
        {
            static bool Prefix(InteractionHighlightController __instance, bool ___m_Inactive, ref bool ___m_IsHighlighting)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.highlightObjectsToggle) return true;
                    Main.DebugLog("Deactiving Highlighting");
                    Action callback = () =>
                    {
                        Main.DebugLog("Deactivating Highlighting callback ToggleHighlight");
                        ToggleHighlight(__instance);
                    };
                    Kingmaker.Game.Instance.Keyboard.Unbind(SettingsRoot.Instance.HighlightObjects.name + UIConsts.SuffixOn, callback);

                    if (___m_IsHighlighting)
                    {
                        ToggleHighlight(__instance);
                    }
                    ___m_Inactive = true;
                    return false;
                } catch(Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
        [Harmony12.HarmonyPatch(typeof(InteractionHighlightController), "Tick")]
        class InteractionHighlightController_Tick_Patch
        {
            static void Postfix(bool ___m_Inactive, bool ___m_IsHighlighting)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (!Main.settings.highlightObjectsToggle || ___m_Inactive || !___m_IsHighlighting)
                    {
                        return;
                    }

                    if (Kingmaker.Game.Instance.TimeController.GameTime - m_LastTickTime < Main.settings.secondsBetweenTickGameTime.Seconds())
                    {
                        return;
                    }
                    m_LastTickTime = Kingmaker.Game.Instance.TimeController.GameTime;

                    UpdateHighlights(true);
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        [Harmony12.HarmonyPatch(typeof(KeyboardAccess), "RegisterBinding", new Type[]{
            typeof(string), typeof(KeyCode), typeof(IEnumerable<GameModeType>),
            typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(ModificationSide)
        })]
        class KeyboardAcess_RegisterBinding_Patch
        {
            static bool Prefix(string name)
            {
                try
                {
                    if(Main.enabled && Main.settings.highlightObjectsToggle && name == SettingsRoot.Instance.HighlightObjects.name + UIConsts.SuffixOff)
                    {
                        return false;
                    }
                } catch(Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
    }
}
