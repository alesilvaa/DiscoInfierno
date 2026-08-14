using UnityEngine;
using DG.Tweening;

public class PrefabGrid2D : MonoBehaviour
{
    [Header("Contenido")]
    [SerializeField] GameObject prefab;
    [SerializeField] Transform generatedParent;

    [Header("Distribución")]
    [SerializeField, Min(1)] int columns = 12;
    [SerializeField, Min(1)] int rows = 12;
    [SerializeField, Min(0.01f)] float cellWidth = 1f;
    [SerializeField, Min(0.01f)] float cellHeight = 1f;

    [Header("Espaciado")]
    [Tooltip("Espacio vacío adicional entre una columna y la siguiente.")]
    [SerializeField, Min(0f)] float horizontalSpacing = 0.1f;

    [Tooltip("Espacio vacío adicional entre una fila y la siguiente.")]
    [SerializeField, Min(0f)] float verticalSpacing = 0.1f;

    [Header("Generación")]
    [SerializeField] bool centerGrid = true;
    [SerializeField] bool generateOnStart = true;

    [Header("Exit")]
    [Tooltip("Objeto Exit de la escena. Reemplazará el cubo de una esquina aleatoria.")]
    [SerializeField] GameObject exitObject;
    [SerializeField] Transform exitHole;
    [SerializeField] Transform exitSign;
    [SerializeField, Min(0.01f)] float exitHoleDuration = 0.28f;
    [Tooltip("Tiempo desde que aparece el hoyo hasta que comienza a aparecer el cartel.")]
    [SerializeField, Min(0f)] float exitSignDelay = 0.2f;
    [SerializeField, Min(0.01f)] float exitSignDuration = 0.32f;
    [SerializeField, Min(1f)] float exitOvershootScale = 1.08f;

    [Header("Chests")]
    [Tooltip("Objeto de escena o prefab usado como plantilla para los cofres.")]
    [SerializeField] GameObject chestTemplate;
    [SerializeField, Min(0)] int chestCount = 2;

    [Header("Orden visual 2D")]
    [Tooltip("Ordena las instancias por fila para que la perspectiva 2D se vea correctamente.")]
    [SerializeField] bool sortByRow = true;

    [Tooltip("Sorting Layer que utilizarán las instancias generadas.")]
    [SerializeField] string sortingLayerName = "World";

    [Tooltip("Precisión del orden por posición Y. 100 permite diferencias de 0.01 unidades.")]
    [SerializeField, Min(1)] int sortingUnitsPerWorldUnit = 100;

    [Tooltip("Desplazamiento adicional del Order in Layer de toda la grilla.")]
    [SerializeField] int sortingOrderOffset;

    [Tooltip("Ajusta el punto Y usado como base visual del prefab. Usá un valor negativo si el pivote está por encima del suelo.")]
    [SerializeField] float sortingPointYOffset;

    [Tooltip("Una casilla marcada genera el prefab; una desmarcada deja un hueco.")]
    [SerializeField, HideInInspector] bool[] enabledCells = new bool[12 * 12];
    [SerializeField, HideInInspector] int storedColumns = 12;
    [SerializeField, HideInInspector] int storedRows = 12;

    Vector3 exitHoleBaseScale;
    Vector3 exitSignBaseScale;
    bool exitScalesCached;
    GameObject exitCellCube;

    public GameObject Prefab => prefab;
    public int Columns => columns;
    public int Rows => rows;
    public bool[] EnabledCells => enabledCells;

    void Reset()
    {
        ResizeCellArray(true);
    }

    void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        cellWidth = Mathf.Max(0.01f, cellWidth);
        cellHeight = Mathf.Max(0.01f, cellHeight);
        horizontalSpacing = Mathf.Max(0f, horizontalSpacing);
        verticalSpacing = Mathf.Max(0f, verticalSpacing);
        sortingUnitsPerWorldUnit = Mathf.Max(1, sortingUnitsPerWorldUnit);
        ResizeCellArray(true);
    }

    void Start()
    {
        if (generateOnStart)
            GenerateGrid();
    }

    [ContextMenu("Generar grilla")]
    public void GenerateGrid()
    {
        if (prefab == null)
        {
            Debug.LogWarning("PrefabGrid2D: asigná un prefab antes de generar la grilla.", this);
            return;
        }

        ResizeCellArray(true);
        ClearGrid();

        Transform parent = generatedParent != null ? generatedParent : transform;
        float horizontalStep = cellWidth + horizontalSpacing;
        float verticalStep = cellHeight + verticalSpacing;
        Vector2 offset = centerGrid
            ? new Vector2((columns - 1) * horizontalStep * 0.5f, (rows - 1) * verticalStep * 0.5f)
            : Vector2.zero;
        int exitCellIndex = ResolveExitCornerIndex();
        int[] chestCellIndices = ResolveChestCellIndices(exitCellIndex);
        int generatedChestCount = 0;

        if (chestTemplate != null && chestTemplate.scene.IsValid())
            chestTemplate.SetActive(false);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = GetIndex(column, row);
                if (!enabledCells[index])
                    continue;

                Vector3 localPosition = new Vector3(
                    column * horizontalStep - offset.x,
                    row * verticalStep - offset.y,
                    0f);

                if (index == exitCellIndex)
                    PrepareExit(parent, localPosition);

                if (IsChestCell(index, chestCellIndices))
                {
                    GenerateChest(parent, localPosition, generatedChestCount++);
                    continue;
                }

                GameObject instance = Instantiate(prefab, parent);
                instance.name = $"{prefab.name}_{column}_{row}";
                instance.transform.localPosition = localPosition;

                if (sortByRow)
                    ApplyYSorting(instance);

                if (index == exitCellIndex)
                    exitCellCube = instance;
            }
        }
    }

    int[] ResolveChestCellIndices(int exitCellIndex)
    {
        if (chestTemplate == null || chestCount <= 0)
            return System.Array.Empty<int>();

        int availableCount = 0;
        for (int i = 0; i < enabledCells.Length; i++)
        {
            if (enabledCells[i] && i != exitCellIndex)
                availableCount++;
        }

        int amount = Mathf.Min(chestCount, availableCount);
        if (amount < chestCount)
        {
            Debug.LogWarning(
                $"PrefabGrid2D: sólo hay {amount} celdas disponibles para {chestCount} cofres.",
                this);
        }

        int[] available = new int[availableCount];
        int cursor = 0;
        for (int i = 0; i < enabledCells.Length; i++)
        {
            if (enabledCells[i] && i != exitCellIndex)
                available[cursor++] = i;
        }

        for (int i = available.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (available[i], available[randomIndex]) =
                (available[randomIndex], available[i]);
        }

        int[] result = new int[amount];
        System.Array.Copy(available, result, amount);
        return result;
    }

    static bool IsChestCell(int cellIndex, int[] chestCellIndices)
    {
        for (int i = 0; i < chestCellIndices.Length; i++)
        {
            if (chestCellIndices[i] == cellIndex)
                return true;
        }

        return false;
    }

    void GenerateChest(Transform parent, Vector3 localPosition, int chestNumber)
    {
        GameObject chest = Instantiate(chestTemplate, parent);
        chest.name = $"GeneratedChest_{chestNumber + 1}";
        chest.transform.localPosition = localPosition;
        chest.SetActive(true);

        Chest2D chestInteraction = chest.GetComponent<Chest2D>();
        if (chestInteraction == null)
            chestInteraction = chest.AddComponent<Chest2D>();

        Chest2D.ChestRewardType rewardType = chestNumber % 2 == 0
            ? Chest2D.ChestRewardType.Equipment
            : Chest2D.ChestRewardType.Upgrades;
        chestInteraction.ConfigureReward(rewardType);
        chest.name = rewardType == Chest2D.ChestRewardType.Upgrades
            ? $"GeneratedUpgradeChest_{chestNumber + 1}"
            : $"GeneratedEquipmentChest_{chestNumber + 1}";

        if (sortByRow)
            ApplyYSorting(chest);
    }

    int ResolveExitCornerIndex()
    {
        if (exitObject == null)
            return -1;

        int[] corners =
        {
            GetIndex(0, 0),
            GetIndex(columns - 1, 0),
            GetIndex(0, rows - 1),
            GetIndex(columns - 1, rows - 1)
        };

        int validCount = 0;
        for (int i = 0; i < corners.Length; i++)
        {
            bool duplicate = false;
            for (int previous = 0; previous < i; previous++)
            {
                if (corners[previous] == corners[i])
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate && enabledCells[corners[i]])
                validCount++;
        }

        if (validCount == 0)
        {
            exitObject.SetActive(false);
            Debug.LogWarning(
                "PrefabGrid2D: el Exit necesita al menos una esquina activa en la grilla.",
                this);
            return -1;
        }

        int selected = Random.Range(0, validCount);
        for (int i = 0; i < corners.Length; i++)
        {
            bool duplicate = false;
            for (int previous = 0; previous < i; previous++)
            {
                if (corners[previous] == corners[i])
                {
                    duplicate = true;
                    break;
                }
            }

            if (duplicate || !enabledCells[corners[i]])
                continue;

            if (selected-- == 0)
                return corners[i];
        }

        return -1;
    }

    void PrepareExit(Transform parent, Vector3 localPosition)
    {
        exitObject.SetActive(true);
        ResolveExitChildren();

        Vector3 targetWorldPosition = parent.TransformPoint(localPosition);
        exitObject.transform.position = targetWorldPosition;

        // La celda se alinea con el centro del hoyo, aunque el root Exit
        // tenga un pivote u offset diferente.
        if (exitHole != null)
            exitObject.transform.position += targetWorldPosition - exitHole.position;

        if (sortByRow)
            ApplyYSorting(exitObject, exitHole);

        if (exitHole == null || exitSign == null)
        {
            Debug.LogWarning(
                "PrefabGrid2D: asigná los hijos Hole y Sign del objeto Exit.",
                this);
            return;
        }

        if (!exitScalesCached)
        {
            exitHoleBaseScale = exitHole.localScale;
            exitSignBaseScale = exitSign.localScale;
            exitScalesCached = true;
        }

        exitHole.localScale = exitHoleBaseScale;
        exitSign.localScale = exitSignBaseScale;

        if (Application.isPlaying)
            LockExit();
    }

    public void LockExit()
    {
        if (exitObject == null)
            return;

        DOTween.Kill(exitObject);
        exitObject.transform.DOKill();
        if (exitHole != null)
            exitHole.DOKill();
        if (exitSign != null)
            exitSign.DOKill();

        exitObject.SetActive(false);
    }

    public void RevealExit()
    {
        if (exitObject == null)
        {
            Debug.LogWarning("PrefabGrid2D: no hay un objeto Exit asignado.", this);
            return;
        }

        ResolveExitChildren();
        if (exitHole == null || exitSign == null)
        {
            Debug.LogWarning(
                "PrefabGrid2D: asigná los hijos Hole y Sign del objeto Exit.",
                this);
            return;
        }

        RemoveExitCellCube();

        if (!exitScalesCached)
        {
            exitHoleBaseScale = exitHole.localScale;
            exitSignBaseScale = exitSign.localScale;
            exitScalesCached = true;
        }

        exitObject.SetActive(true);
        DOTween.Kill(exitObject);
        exitHole.DOKill();
        exitSign.DOKill();
        exitHole.localScale = Vector3.zero;
        exitSign.localScale = Vector3.zero;

        exitHole.DOScale(exitHoleBaseScale * exitOvershootScale, exitHoleDuration * 0.7f)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
                exitHole.DOScale(exitHoleBaseScale, exitHoleDuration * 0.3f)
                    .SetEase(Ease.OutSine));

        Sequence signSequence = DOTween.Sequence().SetTarget(exitObject);
        signSequence.AppendInterval(exitSignDelay);
        signSequence.Append(
            exitSign.DOScale(exitSignBaseScale * exitOvershootScale, exitSignDuration * 0.7f)
                .SetEase(Ease.OutBack));
        signSequence.Append(
            exitSign.DOScale(exitSignBaseScale, exitSignDuration * 0.3f)
                .SetEase(Ease.OutSine));
    }

    void RemoveExitCellCube()
    {
        if (exitCellCube == null)
            return;

        GameObject cubeToRemove = exitCellCube;
        exitCellCube = null;

        // Este cubo se reemplaza por la salida, no cuenta como una muerte.
        // Se elimina antes de activar el Exit para que nunca se superpongan.
        if (Application.isPlaying)
            Destroy(cubeToRemove);
        else
            DestroyImmediate(cubeToRemove);
    }

    void ResolveExitChildren()
    {
        if (exitObject == null)
            return;

        if (exitHole == null)
            exitHole = exitObject.transform.Find("Hole");

        if (exitSign != null)
            return;

        for (int i = 0; i < exitObject.transform.childCount; i++)
        {
            Transform child = exitObject.transform.GetChild(i);
            if (child != exitHole)
            {
                exitSign = child;
                break;
            }
        }
    }

    [ContextMenu("Limpiar grilla")]
    public void ClearGrid()
    {
        if (prefab == null)
        {
            Debug.LogWarning("PrefabGrid2D: no se puede identificar la grilla porque no hay un prefab asignado.", this);
            return;
        }

        Transform parent = generatedParent != null ? generatedParent : transform;
        exitCellCube = null;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            bool isGeneratedCube = child.name.StartsWith(prefab.name + "_");
            bool isGeneratedChest = child.name.StartsWith("GeneratedChest_");
            if (!isGeneratedCube && !isGeneratedChest)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    public int GetIndex(int column, int row)
    {
        return row * columns + column;
    }

    void ApplyYSorting(GameObject instance, Transform sortPoint = null)
    {
        YSort2D ySort = instance.GetComponent<YSort2D>();
        if (ySort == null)
            ySort = instance.AddComponent<YSort2D>();

        ySort.Configure(
            sortingLayerName,
            sortingUnitsPerWorldUnit,
            sortingOrderOffset,
            false,
            sortingPointYOffset,
            sortPoint);
    }

    void ResizeCellArray(bool defaultValue)
    {
        int newCellCount = columns * rows;
        if (enabledCells != null &&
            enabledCells.Length == newCellCount &&
            storedColumns == columns &&
            storedRows == rows)
            return;

        bool[] previous = enabledCells;
        int previousColumns = Mathf.Max(1, storedColumns);
        int previousRows = Mathf.Max(1, storedRows);
        bool[] resized = new bool[newCellCount];

        for (int i = 0; i < resized.Length; i++)
            resized[i] = defaultValue;

        if (previous != null)
        {
            int copiedColumns = Mathf.Min(previousColumns, columns);
            int copiedRows = Mathf.Min(previousRows, rows);

            for (int row = 0; row < copiedRows; row++)
            {
                for (int column = 0; column < copiedColumns; column++)
                {
                    int oldIndex = row * previousColumns + column;
                    int newIndex = row * columns + column;

                    if (oldIndex < previous.Length)
                        resized[newIndex] = previous[oldIndex];
                }
            }
        }

        enabledCells = resized;
        storedColumns = columns;
        storedRows = rows;
    }
}
