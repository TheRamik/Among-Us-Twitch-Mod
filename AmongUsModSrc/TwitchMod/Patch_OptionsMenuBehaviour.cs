using HarmonyLib;
using UnityEngine;
using System.Collections;

namespace TwitchMod
{
    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
    public static class OptionsMenuBehaviour_StartPatch
    {
        public static void Prefix(OptionsMenuBehaviour __instance)
        {
            GameObject generalContent = null, dataContent = null;
            foreach (TabGroup tab in __instance.Tabs)
            {
                ModManager.WriteToConsole(tab.name);
                if (tab.name == "GeneralButton")
                {
                    generalContent = tab.Content;
                    ModManager.WriteToConsole(generalContent.name);
                }
                else if (tab.name == "DataButton")
                {
                    dataContent = tab.Content;
                }
            }
            if (generalContent != null)
            {
                //Get a text game object to duplicate
                Transform origTextTransform = generalContent.transform.Find("ControlGroup/ControlText");
                GameObject newText = Object.Instantiate(origTextTransform.gameObject, origTextTransform.parent.parent);
                
                //Grab its components
                TextRenderer newText_textRenderer = newText.GetComponent<TextRenderer>();
                TextTranslator newText_textTranslator = newText.GetComponent<TextTranslator>();

                //Sets the text without letting it be reset/overridden
                newText_textTranslator.ResetOnlyWhenNoDefault = true;
                newText_textTranslator.defaultStr = "Activate Twitch Plugin (Streamers Only!)";
                newText_textRenderer.Text = "Activate Twitch Plugin (Streamers Only!)";

                //Hide the text before it gets moved
                newText.SetActive(false);

                //Move the text, not sure why this is needed but without it the text won't reposition
                //Maybe some other script is moving it first?
                IEnumerator coroutine = waitForReposition(newText);
                Reactor.Coroutines.Start(coroutine);
            }
        }

        private static IEnumerator waitForReposition(GameObject text)
        {
            yield return new WaitForSeconds(1);
            text.transform.position = new Vector3(-2.6f, -1.5f, -720);
            text.SetActive(true);
            ModManager.WriteToConsole(text.transform.position.ToString());
        }
    }
}