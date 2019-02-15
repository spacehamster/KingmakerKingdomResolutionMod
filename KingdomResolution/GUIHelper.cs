using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace KingdomResolution
{
    public class GUIHelper
    {
        public static void Toggle(ref bool value, string text, string tooltip = null)
        {
            GUILayout.BeginHorizontal();
            var content = tooltip == null ?
                new GUIContent(text) :
                new GUIContent(text, tooltip);
            var newValue = GUILayout.Toggle(value, content, GUILayout.ExpandWidth(false));
            value = newValue;
            GUILayout.EndHorizontal();
        }
        public static bool Button(string text, string tooltip = null)
        {
            var content = tooltip == null ?
                new GUIContent(text) :
                new GUIContent(text, tooltip);
            return GUILayout.Button(content, GUILayout.ExpandWidth(false));
        }
        public static void ChooseFactor(string text, string tooltip, float value, float maxValue, Action<float> setter, Func<float, string> formatter)
        {
            var content = tooltip == null ?
                new GUIContent(text + " ") :
                new GUIContent(text + " ", tooltip);
            GUILayout.BeginHorizontal();
            GUILayout.Label(content, GUILayout.Width(300));
            var newValue = GUILayout.HorizontalSlider(value, 0, maxValue, GUILayout.Width(200));
            GUILayout.Label(formatter(newValue));
            GUILayout.EndHorizontal();
            if (newValue != value)
            {
                setter(newValue);
            }
        }
        public static void ChooseInt(ref int value, string text, string tooltip = null, bool canBeNegative = false)
        {
            var content = tooltip == null ?
                new GUIContent(text) :
                new GUIContent(text, tooltip);
            GUILayout.BeginHorizontal();
            GUILayout.Label(content, GUILayout.Width(300));
            var result = GUILayout.TextField(value.ToString(), GUILayout.Width(300));
            GUILayout.EndHorizontal();
            int.TryParse(result, out int newValue);
            if (!canBeNegative) newValue = Math.Abs(newValue);
            value = newValue;
        }
        public static Rect ummRect = new Rect();
        public static Vector2[] ummScrollPosition;
        public static int ummTabId = 0;
        [Harmony12.HarmonyPatch(typeof(UnityModManager.UI), "Update")]
        internal static class UnityModManager_UI_Update_Patch
        {
            private static void Postfix(UnityModManager.UI __instance, ref Rect ___mWindowRect, ref Vector2[] ___mScrollPosition, ref int ___tabId)
            {
                ummRect = ___mWindowRect;
                ummScrollPosition = ___mScrollPosition;
                ummTabId = ___tabId;
            }
        }
        public static void ShowTooltip()
        {
                GUIContent tooltipString = new GUIContent(GUI.tooltip);
                GUIStyle styleRect = GUI.skin.box;
                Vector2 tooltipSize = styleRect.CalcSize(tooltipString);
                GUIStyle styleTooltip = new GUIStyle();
                Texture2D background = new Texture2D(1, 1);
                float textHeight = styleRect.CalcHeight(tooltipString, tooltipSize.x);
                if (GUI.tooltip != "" && GUI.tooltip != null)
                {
                    background.SetPixels32(new Color32[] { new Color32(0, 0, 0, 220) });
                    GUIStyleState stylestate = new GUIStyleState();
                    stylestate.background = background;
                    styleTooltip.normal = stylestate;
                }
                else
                {
                    background.SetPixels32(new Color32[] { new Color32(0, 0, 0, 0) });
                }
                float rectX = Input.mousePosition.x - ummRect.min.x + ummScrollPosition[ummTabId].x - 8;
                if (rectX > 470)
                {
                    rectX = rectX - tooltipSize.x + 8;
                }
                float rectY = Screen.height - ummRect.min.y + ummScrollPosition[ummTabId].y - Input.mousePosition.y - 65 - textHeight;
                GUI.Label(new Rect(rectX, rectY, tooltipSize.x, tooltipSize.y), GUI.tooltip, styleTooltip);
        }

        internal static void Toggle(ref bool enablePausedRandomEvents, object enablePausedRandomEventsLabel)
        {
            throw new NotImplementedException();
        }
    }
}
