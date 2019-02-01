// Copyright (c) 2018 fireundubh <fireundubh@gmail.com>
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using Kingmaker;
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
    class HighlightObjectToggle
    {
        private static void UpdateHighlights(bool raiseEvent = false)
        {
            foreach (MapObjectEntityData mapObjectEntityData in Game.Instance.State.MapObjects)
            {
                mapObjectEntityData.View.UpdateHighlight();
            }

            foreach (UnitEntityData unitEntityData in Game.Instance.State.Units)
            {
                unitEntityData.View.UpdateHighlight(raiseEvent);
            }
        }
        static TimeSpan m_LastTickTime;
        [Harmony12.HarmonyPatch(typeof(InteractionHighlightController), "HighlightOn")]
        class InteractionHighlightController_Activate_Patch
        {

            static bool Prefix(InteractionHighlightController __instance, bool ___m_Inactive, ref bool ___m_IsHighlighting)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.highlightObjectsToggle)
                    {
                        return true;
                    }
                    if (___m_IsHighlighting & !___m_Inactive)
                    {
                        ___m_IsHighlighting = false;
                        foreach (MapObjectEntityData mapObjectEntityData in Game.Instance.State.MapObjects)
                        {
                            mapObjectEntityData.View.UpdateHighlight();
                        }
                        foreach (UnitEntityData unitEntityData in Game.Instance.State.Units)
                        {
                            unitEntityData.View.UpdateHighlight(false);
                        }
                        EventBus.RaiseEvent<IInteractionHighlightUIHandler>(delegate (IInteractionHighlightUIHandler h)
                        {
                            h.HandleHighlightChange(false);
                        });
                        return false;
                    }
                } catch(Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
        [Harmony12.HarmonyPatch(typeof(InteractionHighlightController), "HighlightOff")]
        class InteractionHighlightController_Deactivate_Patch
        {
            static bool Prefix(InteractionHighlightController __instance)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (Main.settings.highlightObjectsToggle)
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
                    var secondsBetweenTickGameTime = 1;
                    if (Game.Instance.TimeController.GameTime - m_LastTickTime < secondsBetweenTickGameTime.Seconds())
                    {
                        return;
                    }
                    m_LastTickTime = Game.Instance.TimeController.GameTime;

                    UpdateHighlights(true);
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
    }
}
