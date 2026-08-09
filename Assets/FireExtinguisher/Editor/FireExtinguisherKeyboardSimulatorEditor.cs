using _Scripts.FireExtinguishers;
using UnityEditor;
using UnityEngine;

namespace _Scripts.Editors.FireExtinguishers
{
    [CustomEditor(typeof(FireExtinguisherKeyboardSimulator))]
    public sealed class FireExtinguisherKeyboardSimulatorEditor : Editor
    {
        private SerializedProperty _controller;
        private SerializedProperty _simulateKeyboardHeartbeat;
        private SerializedProperty _heartbeatInterval;
        private SerializedProperty _lastSimulatedKey;

        private void OnEnable()
        {
            _controller =
                serializedObject.FindProperty("_controller");
            _simulateKeyboardHeartbeat =
                serializedObject.FindProperty(
                    "_simulateKeyboardHeartbeat");
            _heartbeatInterval =
                serializedObject.FindProperty("_heartbeatInterval");
            _lastSimulatedKey =
                serializedObject.FindProperty("_lastSimulatedKey");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_controller);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simulation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _simulateKeyboardHeartbeat,
                new GUIContent("Simulate Keyboard Heartbeat"));
            EditorGUILayout.PropertyField(_heartbeatInterval);

            EditorGUILayout.HelpBox(
                "On Play, the simulator sends the configured default key immediately. When heartbeat simulation is enabled, it repeats the Controller's last received keyboard key.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Runtime State",
                EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(_lastSimulatedKey);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
