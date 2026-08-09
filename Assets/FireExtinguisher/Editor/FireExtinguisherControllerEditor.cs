using _Scripts.FireExtinguishers;
using UnityEditor;
using UnityEngine;

namespace _Scripts.Editors.FireExtinguishers
{
    [CustomEditor(typeof(FireExtinguisherController))]
    public sealed class FireExtinguisherControllerEditor : Editor
    {
        private SerializedProperty _fireExtinguisher;
        private SerializedProperty _inputSettings;
        private SerializedProperty _connectionTimeout;
        private SerializedProperty _logReceivedStates;
        private SerializedProperty _currentState;
        private SerializedProperty _isConnected;
        private SerializedProperty _lastReceivedKey;
        private SerializedProperty _hasReceivedKey;

        private void OnEnable()
        {
            _fireExtinguisher =
                serializedObject.FindProperty("_fireExtinguisher");
            _inputSettings =
                serializedObject.FindProperty("_inputSettings");
            _connectionTimeout =
                serializedObject.FindProperty("_connectionTimeout");
            _logReceivedStates =
                serializedObject.FindProperty("_logReceivedStates");
            _currentState =
                serializedObject.FindProperty("_currentState");
            _isConnected =
                serializedObject.FindProperty("_isConnected");
            _lastReceivedKey =
                serializedObject.FindProperty("_lastReceivedKey");
            _hasReceivedKey =
                serializedObject.FindProperty("_hasReceivedKey");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_fireExtinguisher);
            EditorGUILayout.PropertyField(_inputSettings);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_connectionTimeout);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _logReceivedStates,
                new GUIContent("Log Received States"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Runtime State",
                EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_isConnected);
                EditorGUILayout.PropertyField(_currentState, true);
                EditorGUILayout.PropertyField(_hasReceivedKey);
                EditorGUILayout.PropertyField(_lastReceivedKey);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
