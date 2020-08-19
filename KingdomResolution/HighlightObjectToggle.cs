// Copyright (c) 2018 fireundubh <fireundubh@gmail.com>
// This code is licensed under MIT license (see LICENSE for details)

using System;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers;
using Kingmaker.Controllers.MapObjects;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using UnityEngine;

namespace KingdomResolution
{
    public class HighlightObjectToggle
    {
        [HarmonyPatch(typeof(InteractionHighlightController), "HighlightOn")]
        class InteractionHighlightController_Activate_Patch
        {
            static TimeSpan m_LastTickTime;
            static FastGetter<InteractionHighlightController, bool> IsHighlightingGet;
            static FastSetter<InteractionHighlightController, bool> IsHighlightingSet;
            static bool Prepare()
            {
                IsHighlightingGet = Accessors.CreateGetter<InteractionHighlightController, bool>("IsHighlighting");
                IsHighlightingSet = Accessors.CreateSetter<InteractionHighlightController, bool>("IsHighlighting");
                return true;
            }
            static bool Prefix(InteractionHighlightController __instance, bool ___m_Inactive)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.highlightObjectsToggle)
                    {
                        return true;
                    }

                    if (IsHighlightingGet(__instance) & !___m_Inactive)
                    {
                        IsHighlightingSet(__instance, false);
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
                    Main.Error(ex);
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(InteractionHighlightController), "HighlightOff")]
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
                    Main.Error(ex);
                }
                return true;
            }
        }
    }
}