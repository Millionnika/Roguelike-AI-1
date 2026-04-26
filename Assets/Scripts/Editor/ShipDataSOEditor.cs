using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipDataSO))]
public sealed class ShipDataSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIdentitySection();
        DrawMovementSection();
        DrawSurvivabilitySection();
        DrawLoadoutSection();
        DrawVisualSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIdentitySection()
    {
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("role"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("roleRu"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("descriptionRu"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shipClass"));
        EditorGUILayout.Space(6f);
    }

    private void DrawMovementSection()
    {
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("acceleration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("drag"));
        EditorGUILayout.Space(6f);
    }

    private void DrawSurvivabilitySection()
    {
        EditorGUILayout.LabelField("Survivability", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxShield"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxArmor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHull"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("capacitor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("capacitorRechargeTime"));
        EditorGUILayout.Space(6f);
    }

    private void DrawLoadoutSection()
    {
        SerializedProperty weaponSlotCount = serializedObject.FindProperty("weaponSlotCount");
        SerializedProperty moduleSlotCount = serializedObject.FindProperty("moduleSlotCount");
        SerializedProperty startingWeapons = serializedObject.FindProperty("startingWeapons");
        SerializedProperty startingModules = serializedObject.FindProperty("startingModules");

        EditorGUILayout.LabelField("Loadout", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(weaponSlotCount);
        EditorGUILayout.PropertyField(moduleSlotCount);
        if (EditorGUI.EndChangeCheck())
        {
            int weapons = Mathf.Max(0, weaponSlotCount.intValue);
            int modules = Mathf.Max(0, moduleSlotCount.intValue);
            weaponSlotCount.intValue = weapons;
            moduleSlotCount.intValue = modules;
            Resize(startingWeapons, weapons);
            Resize(startingModules, modules);
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("damageMultiplier"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("repairMultiplier"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shipPrefab"));

        DrawSlotList(startingWeapons, "Starting Weapons");
        DrawSlotList(startingModules, "Starting Modules");

        if (GUILayout.Button("Sync Slot Lists"))
        {
            Resize(startingWeapons, Mathf.Max(0, weaponSlotCount.intValue));
            Resize(startingModules, Mathf.Max(0, moduleSlotCount.intValue));
        }

        EditorGUILayout.Space(6f);
    }

    private void DrawVisualSection()
    {
        EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("accentColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("auraColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shipIcon"));
    }

    private static void DrawSlotList(SerializedProperty property, string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            EditorGUILayout.PropertyField(element, new GUIContent("Slot " + (i + 1)));
        }
        EditorGUI.indentLevel--;
    }

    private static void Resize(SerializedProperty property, int targetCount)
    {
        while (property.arraySize < targetCount)
        {
            property.InsertArrayElementAtIndex(property.arraySize);
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = null;
        }

        while (property.arraySize > targetCount)
        {
            property.DeleteArrayElementAtIndex(property.arraySize - 1);
        }
    }
}
