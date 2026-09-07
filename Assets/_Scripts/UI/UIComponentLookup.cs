using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    internal static class UIComponentLookup
    {
        public static Button FindButton(Component owner, string objectName)
        {
            Button[] buttons = owner.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
                if (button.name == objectName)
                    return button;
            return null;
        }

        public static Button[] FindButtonsUnder(Component owner, string parentName)
        {
            Transform[] children = owner.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
                if (child.name == parentName)
                    return child.GetComponentsInChildren<Button>(true);
            return new Button[0];
        }
    }
}
