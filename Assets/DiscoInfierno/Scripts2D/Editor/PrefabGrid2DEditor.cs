using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PrefabGrid2D))]
public class PrefabGrid2DEditor : Editor
{
    SerializedProperty enabledCells;

    void OnEnable()
    {
        enabledCells = serializedObject.FindProperty("enabledCells");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Celdas de la grilla", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Verde: se genera el prefab. Gris: queda vacío. La fila superior del Inspector corresponde a la parte superior de la grilla.",
            MessageType.Info);

        PrefabGrid2D grid = (PrefabGrid2D)target;
        int columns = grid.Columns;
        int rows = grid.Rows;
        float availableWidth = EditorGUIUtility.currentViewWidth - 48f;
        float buttonSize = Mathf.Clamp(availableWidth / columns, 16f, 32f);

        for (int row = rows - 1; row >= 0; row--)
        {
            EditorGUILayout.BeginHorizontal();

            for (int column = 0; column < columns; column++)
            {
                int index = grid.GetIndex(column, row);
                if (index >= enabledCells.arraySize)
                    continue;

                SerializedProperty cell = enabledCells.GetArrayElementAtIndex(index);
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = cell.boolValue
                    ? new Color(0.35f, 0.85f, 0.45f)
                    : new Color(0.55f, 0.55f, 0.55f);

                if (GUILayout.Button(cell.boolValue ? "●" : "×",
                        GUILayout.Width(buttonSize), GUILayout.Height(buttonSize)))
                {
                    cell.boolValue = !cell.boolValue;
                }

                GUI.backgroundColor = previousColor;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Activar todas"))
            SetAllCells(true);
        if (GUILayout.Button("Vaciar todas"))
            SetAllCells(false);
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(grid.Prefab == null))
        {
            if (GUILayout.Button("Generar grilla", GUILayout.Height(30f)))
            {
                Undo.RegisterFullObjectHierarchyUndo(grid.gameObject, "Generar grilla 2D");
                grid.GenerateGrid();
                EditorUtility.SetDirty(grid.gameObject);
            }
        }

        if (GUILayout.Button("Limpiar grilla"))
        {
            Undo.RegisterFullObjectHierarchyUndo(grid.gameObject, "Limpiar grilla 2D");
            grid.ClearGrid();
            EditorUtility.SetDirty(grid.gameObject);
        }
    }

    void SetAllCells(bool value)
    {
        for (int i = 0; i < enabledCells.arraySize; i++)
            enabledCells.GetArrayElementAtIndex(i).boolValue = value;
    }
}
