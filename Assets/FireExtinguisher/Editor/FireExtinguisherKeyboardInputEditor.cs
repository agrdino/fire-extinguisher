using _Scripts.FireExtinguishers;
using UnityEditor;

namespace _Scripts.Editors.FireExtinguishers
{
    [CustomEditor(typeof(FireExtinguisherKeyboardInput))]
    public sealed class FireExtinguisherKeyboardInputEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.HelpBox(
                "This component always reads real keyboard presses and sends the raw Key to the Controller. Key-to-state conversion is owned by the Controller and its Input Settings asset.",
                MessageType.Info);
        }
    }
}
