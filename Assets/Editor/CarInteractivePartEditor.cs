using UnityEditor;
using UnityEngine;
using Kotenkoff; // Не забудь твой неймспейс, если CarInteractivePart лежит в нем!

[CustomEditor(typeof(CarInteractivePart))]
public class CarInteractivePartEditor : UnityEditor.Editor
{
    // Кэшируем переменные для оптимизации (Best Practice для инспекторов)
    SerializedProperty actionType;
    SerializedProperty animationSpeed;
    SerializedProperty closedRotation;
    SerializedProperty openRotation;
    SerializedProperty closedPosition;
    SerializedProperty openPosition;
    SerializedProperty audioSource;
    SerializedProperty openSounds;
    SerializedProperty closeSounds;
    SerializedProperty isOpen;

    private void OnEnable()
    {
        actionType = serializedObject.FindProperty("actionType");
        animationSpeed = serializedObject.FindProperty("animationSpeed");
        closedRotation = serializedObject.FindProperty("closedRotation");
        openRotation = serializedObject.FindProperty("openRotation");
        closedPosition = serializedObject.FindProperty("closedPosition");
        openPosition = serializedObject.FindProperty("openPosition");
        
        // Подтягиваем новые аудио-настройки
        audioSource = serializedObject.FindProperty("audioSource");
        openSounds = serializedObject.FindProperty("openSounds");
        closeSounds = serializedObject.FindProperty("closeSounds");
        
        isOpen = serializedObject.FindProperty("isOpen");
    }

    public override void OnInspectorGUI()
    {
        CarInteractivePart part = (CarInteractivePart)target;

        serializedObject.Update();

        EditorGUILayout.Space(5);
        GUILayout.Label("🚪 Interactive Part Settings", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14, alignment = TextAnchor.MiddleCenter });
        EditorGUILayout.Space(5);

        // --- БАЗОВЫЕ НАСТРОЙКИ ---
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.PropertyField(actionType, new GUIContent("Movement Type"));
        EditorGUILayout.PropertyField(animationSpeed, new GUIContent("Animation Speed"));
        EditorGUILayout.EndVertical();

        InteractActionType type = (InteractActionType)actionType.enumValueIndex;

        // --- БЛОК ВРАЩЕНИЯ ---
        if (type == InteractActionType.RotateOnly || type == InteractActionType.Both)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🔄 Rotation (Doors, Hood, Trunk)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(closedRotation);
            EditorGUILayout.PropertyField(openRotation);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Current as CLOSED", GUILayout.Height(25)))
            {
                Undo.RecordObject(part, "Set Closed Rotation");
                part.closedRotation = part.transform.localEulerAngles;
                EditorUtility.SetDirty(part); // Обязательно помечаем как измененное для сохранения сцены
            }
            if (GUILayout.Button("Save Current as OPEN", GUILayout.Height(25)))
            {
                Undo.RecordObject(part, "Set Open Rotation");
                part.openRotation = part.transform.localEulerAngles;
                EditorUtility.SetDirty(part);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // --- БЛОК ПЕРЕМЕЩЕНИЯ ---
        if (type == InteractActionType.TranslateOnly || type == InteractActionType.Both)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("↔️ Translation (Buttons, Drawers)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(closedPosition);
            EditorGUILayout.PropertyField(openPosition);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Current as CLOSED", GUILayout.Height(25)))
            {
                Undo.RecordObject(part, "Set Closed Position");
                part.closedPosition = part.transform.localPosition;
                EditorUtility.SetDirty(part);
            }
            if (GUILayout.Button("Save Current as OPEN", GUILayout.Height(25)))
            {
                Undo.RecordObject(part, "Set Open Position");
                part.openPosition = part.transform.localPosition;
                EditorUtility.SetDirty(part);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // --- БЛОК АУДИО (ВОССТАНОВЛЕН) ---
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("🔊 Audio Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(audioSource);
        EditorGUILayout.PropertyField(openSounds, true); // true нужен, чтобы массивы рисовались со списком
        EditorGUILayout.PropertyField(closeSounds, true);
        EditorGUILayout.EndVertical();

        // --- БЛОК БЫСТРОГО ТЕСТИРОВАНИЯ ---
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("🕹 Quick Test Tools", EditorStyles.boldLabel);
        
        // Кнопки для просмотра состояний без запуска игры
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap to CLOSED (Preview)"))
        {
            Undo.RecordObject(part.transform, "Snap Closed");
            if (type == InteractActionType.RotateOnly || type == InteractActionType.Both) part.transform.localEulerAngles = part.closedRotation;
            if (type == InteractActionType.TranslateOnly || type == InteractActionType.Both) part.transform.localPosition = part.closedPosition;
        }
        if (GUILayout.Button("Snap to OPEN (Preview)"))
        {
            Undo.RecordObject(part.transform, "Snap Open");
            if (type == InteractActionType.RotateOnly || type == InteractActionType.Both) part.transform.localEulerAngles = part.openRotation;
            if (type == InteractActionType.TranslateOnly || type == InteractActionType.Both) part.transform.localPosition = part.openPosition;
        }
        EditorGUILayout.EndHorizontal();

        // Кнопка для теста анимации во время Play Mode
        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button(part.isOpen ? "Close Door (Play Mode)" : "Open Door (Play Mode)", GUILayout.Height(30)))
        {
            part.isOpen = !part.isOpen;
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndVertical();

        // --- БЛОК ОТЛАДКИ СЕТИ ---
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("🛠 Network Debug (Read Only)", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.PropertyField(isOpen);
        GUI.enabled = true;
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}