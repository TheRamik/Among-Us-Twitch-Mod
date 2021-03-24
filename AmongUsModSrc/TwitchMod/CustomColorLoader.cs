using System.Collections.Generic;
using Reactor;
using UnityEngine;

namespace TwitchMod
{
    public static class CustomColorLoader
    {
        public class CustomColor
        {
            public StringNames shortColorName;
            public StringNames fullColorName;
            public Color32 mainColor;
            public Color32 shadowColor;

            public CustomColor(string shortColorName, string fullColorName, Color32 mainColor, Color32 shadowColor)
            {
                this.shortColorName = CustomStringName.Register(shortColorName);
                this.fullColorName = CustomStringName.Register(fullColorName);
                this.mainColor = mainColor;
                this.shadowColor = shadowColor;
            }
        }

        public static List<CustomColor> customColors = new List<CustomColor>();


        private static void InitializeCustomColors()
        {
            CustomColor twitchColor = new CustomColor("TWCH", "Twitch", new Color32(164, 108, 243, byte.MaxValue), new Color32(111, 45, 204, byte.MaxValue));
            customColors.Add(twitchColor);
        }

        public static void AddCustomColorsToGame()
        {
            InitializeCustomColors();

            //Allocate array memory for custom colors
            int colorCount = Palette.PlayerColors.Length;
            UnhollowerBaseLib.Il2CppStructArray<Color32> playerColors = new UnhollowerBaseLib.Il2CppStructArray<Color32>(colorCount + customColors.Count);
            UnhollowerBaseLib.Il2CppStructArray<Color32> shadowColors = new UnhollowerBaseLib.Il2CppStructArray<Color32>(colorCount + customColors.Count);
            UnhollowerBaseLib.Il2CppStructArray<StringNames> shortColorNames = new UnhollowerBaseLib.Il2CppStructArray<StringNames>(colorCount + customColors.Count);
            UnhollowerBaseLib.Il2CppStructArray<StringNames> fullColorNames = new UnhollowerBaseLib.Il2CppStructArray<StringNames>(colorCount + customColors.Count);

            //Add orig colors to array
            for (int i = 0; i < colorCount; i++)
            {
                playerColors[i] = Palette.PlayerColors[i];
                shadowColors[i] = Palette.ShadowColors[i];
                shortColorNames[i] = Palette.ShortColorNames[i];
                fullColorNames[i] = Palette.ColorNames[i];
            }

            //Add cusotm colors to array
            for (int i = 0; i < customColors.Count; i++)
            {
                playerColors[colorCount + i] = customColors[i].mainColor;
                shadowColors[colorCount + i] = customColors[i].shadowColor;
                shortColorNames[colorCount + i] = customColors[i].shortColorName;
                fullColorNames[colorCount + i] = customColors[i].fullColorName;
            }

            //Update palette
            Palette.PlayerColors = playerColors;
            Palette.ShadowColors = shadowColors;
            Palette.ShortColorNames = shortColorNames;
            Palette.ColorNames = fullColorNames;
            MedScanMinigame.ColorNames = fullColorNames;
        }
    }
}