using Kotenkoff.Monsters;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TheBlindWeaver))]
public sealed class TheBlindWeaverEditor : Editor
{
    TheBlindWeaver monster;

    private void OnEnable()
    {
        monster = target as TheBlindWeaver;
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GUILayout.Label("Здоровье", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHealth"),
            new GUIContent("Максимальное здоровье", "Максимальное значение для здоровья"));
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("health"),
            new GUIContent("Здоровье", "Текущее здоровье монстра"));
        
        EditorGUILayout.Space(15);
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("target"),
            new GUIContent("Цель", "Объект, за которым 'бежит' монстр"));
        
        EditorGUILayout.Space(15);
        
        GUILayout.Label("Агрессия:", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("isAggressive"),
            new GUIContent("Агрессивен?", "Агрессивен ли сейчас монстр"));
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("aggressiveDistance"),
            new GUIContent("Дистанция анти-агрессии", "Нужная дистанция до цели, чтобы можно было снять агрессию"));
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("aggressiveTime"),
            new GUIContent("Время Агрессии", "Время, после которого если не видно цели, снимется агрессия (сек.)"));
        
        EditorGUILayout.Space(7);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isCoroutine"),
            new GUIContent("'Остывает'?", "'Остывает' ли монстр"));
        
        EditorGUILayout.Space(15);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("distance"));
            
        serializedObject.ApplyModifiedProperties();
    }
}

