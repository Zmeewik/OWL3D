using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ComplexColliderHandle))]
public class ComplexColliderHandleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ComplexColliderHandle script = (ComplexColliderHandle)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Collider Control", EditorStyles.boldLabel);
        if (GUILayout.Button("Enable All Colliders"))
        {
            script.SetEnabledAll(true);
        }
        if (GUILayout.Button("Disable All Colliders"))
        {
            script.SetEnabledAll(false);
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Trigger Control", EditorStyles.boldLabel);
        if (GUILayout.Button("Make All Triggers"))
        {
            script.SetTriggerAll(true);
        }
        if (GUILayout.Button("Make All Solid"))
        {
            script.SetTriggerAll(false);
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Ragdoll Control", EditorStyles.boldLabel);
        if (GUILayout.Button("Activate Ragdoll"))
        {
            script.ActivateRagdoll();
        }
        if (GUILayout.Button("Deactivate Ragdoll"))
        {
            script.DeactivateRagdoll();
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        if (GUILayout.Button("Print Structure"))
        {
            script.PrintStructure();
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
        if (GUILayout.Button("Create Standard Humanoid"))
        {
            script.CreateStandardHumanoid();
        }
        if (GUILayout.Button("Auto Generate Bones from Root"))
        {
            script.AutoGenerateBones();
        }
        GUILayout.Space(5);
        if (GUILayout.Button("Set All Colliders New Mass"))
        {
            script.ChangeCollidersMass();
        }
        
        if (GUILayout.Button("Initialize Colliders (With Size)"))
        {
            script.InitializeCollidersWithSize();
        }

        if (GUILayout.Button("Initialize Colliders (Keep Size)"))
        {
            script.InitializeCollidersKeepSize();
        }


        GUILayout.Space(10);
        EditorGUILayout.LabelField("Cleanup", EditorStyles.boldLabel);
        if (GUILayout.Button("Remove All Colliders"))
        {
            script.RemoveAllColliders();
        }

    }
}