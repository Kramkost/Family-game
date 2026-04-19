using System;
using MyAssets.scripts.Kotenkoff.General;
using MyAssets.scripts.Kotenkoff.Vehicle;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(VehicleDoor))]
    public class VehicleDoorEditor : UnityEditor.Editor
    {
        private SerializedProperty isOpenProperty;
        private SerializedProperty animationSpeedProperty;
        private SerializedProperty useAnimationProperty;
        private SerializedProperty animationTypeProperty;
        private SerializedProperty closedRotation, openRotation, closedPosition, openPosition;

        private void OnEnable()
        {
            isOpenProperty = serializedObject.FindProperty("isOpen");
            animationSpeedProperty = serializedObject.FindProperty("animationSpeed");
            useAnimationProperty = serializedObject.FindProperty("useAnimation");
            animationTypeProperty = serializedObject.FindProperty("animationType");
            closedPosition = serializedObject.FindProperty("closedPosition");
            openPosition = serializedObject.FindProperty("openPosition");
            closedRotation = serializedObject.FindProperty("closedRotation");
            openRotation = serializedObject.FindProperty("openRotation");
        }

        public override void OnInspectorGUI()
        {
            var door = (VehicleDoor)target;
            var type = (Enums.AnimationType)animationTypeProperty.enumValueIndex;
            
            serializedObject.Update();
            
            GUILayout.Label("Основные:", EditorStyles.boldLabel);
            GUI.enabled = false;
            EditorGUILayout.PropertyField(isOpenProperty, new GUIContent("Открыто?", "Открыта ли дверь?"));
            GUI.enabled = true;

            EditorGUILayout.Space(5);
            
            GUILayout.Label("Анимация:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useAnimationProperty, new GUIContent("Использовать Анимацию?", "Нужно ли использовать анимацию?"));
            
            if (useAnimationProperty.boolValue)
            {
                EditorGUILayout.PropertyField(animationSpeedProperty,
                    new GUIContent("Скорость Анимации", "Скорость проигрывания анимации."));
                EditorGUILayout.PropertyField(animationTypeProperty,
                    new GUIContent("Тип Анимации", "Используемый тип анимации."));
            }
            
            GUILayout.Space(5);
            
            GUILayout.Label("Поворот:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(closedRotation,
                new GUIContent("Поворот При Закрытии", "Поворот двери при закрытии."));
            EditorGUILayout.PropertyField(openRotation, 
                new GUIContent("Поворот При Открытии", "Поворот двери при открытии."));
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Сохранить Как Закрытую", GUILayout.Height(25)))
            {
                Undo.RecordObject(door, "Set Closed Rotation");
                closedRotation.vector3Value = door.transform.localEulerAngles;
                EditorUtility.SetDirty(door); // Обязательно помечаем как измененное для сохранения сцены
            }
            if (GUILayout.Button("Сохранить Как Открытую", GUILayout.Height(25)))
            {
                Undo.RecordObject(door, "Set Open Rotation");
                openRotation.vector3Value = door.transform.localEulerAngles;
                EditorUtility.SetDirty(door);
            }
            EditorGUILayout.EndHorizontal();
                
            EditorGUILayout.Space(5);
                
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Закрыть (Предпросмотр)"))
            {
                Undo.RecordObject(door.transform, "Snap Closed");
                if (type == Enums.AnimationType.RotateOnly || type == Enums.AnimationType.Both) door.transform.localEulerAngles = closedRotation.vector3Value;
                if (type == Enums.AnimationType.MoveOnly || type == Enums.AnimationType.Both) door.transform.localPosition = closedPosition.vector3Value;
            }
            if (GUILayout.Button("Открыть (Предпросмотр)"))
            {
                Undo.RecordObject(door.transform, "Snap Open");
                if (type == Enums.AnimationType.RotateOnly || type == Enums.AnimationType.Both) door.transform.localEulerAngles = openRotation.vector3Value;
                if (type == Enums.AnimationType.MoveOnly || type == Enums.AnimationType.Both) door.transform.localPosition = openPosition.vector3Value;
            }
            EditorGUILayout.EndHorizontal();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}