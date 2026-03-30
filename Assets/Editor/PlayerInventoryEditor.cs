using Kotenkoff;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerInventory))]
public class PlayerInventoryEditor : Editor
{
    private SerializedProperty inventory;
    
    private void OnEnable()
    {
        inventory = serializedObject.FindProperty("inventory");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.PropertyField(inventory, new GUIContent("Инвентарь", "Это инвентарь игрока. Добавляй или убирай элементы, чтобы менять количество слотов"));
        
        serializedObject.ApplyModifiedProperties();
    }
}
