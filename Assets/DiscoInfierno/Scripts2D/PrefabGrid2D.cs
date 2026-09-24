using System.Collections.Generic;
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

    [Header("Objetos especiales")]
    [Tooltip("Reemplaza cubos por rebotadores. Este nivel usa uno.")]
    [SerializeField] GameObject rebotadorTemplate;
    [SerializeField, Min(0)] int rebotadorCount = 1;
    [Tooltip("Cada pareja genera exactamente dos portales enlazados.")]
    [SerializeField] GameObject teletransportTemplate;
    [SerializeField, Min(0)] int teletransportPairCount = 1;
    [Tooltip("Separación Manhattan mínima deseada entre ambos extremos del portal.")]
    [SerializeField, Min(1)] int minimumTeletransportCellDistance = 4;
    [Tooltip("Variantes explosivas tomadas del hijo ExplosiveCube del prefab de obstáculo.")]
    [SerializeField, Min(0)] int explosiveCubeCount = 3;

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
        rebotadorCount = Mathf.Max(0, rebotadorCount);
        teletransportPairCount = Mathf.Max(0, teletransportPairCount);
        minimumTeletransportCellDistance = Mathf.Max(1, minimumTeletransportCellDistance);
        explosiveCubeCount = Mathf.Max(0, explosiveCubeCount);
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
        List<int> availableCells = BuildAvailableCells(exitCellIndex);
        int[] teletransportCellIndices = ReserveTeletransportCells(availableCells);
        int[] rebotadorCellIndices = ReserveRandomCells(
            availableCells,
            rebotadorTemplate != null ? rebotadorCount : 0,
            "rebotadores");
        int[] explosiveCubeCellIndices = ReserveRandomCells(
            availableCells,
            HasExplosiveCubeVariant() ? explosiveCubeCount : 0,
            "cubos explosivos");
        int[] chestCellIndices = ReserveRandomCells(
            availableCells,
            chestTemplate != null ? chestCount : 0,
            "cofres");
        int generatedChestCount = 0;
        int generatedRebotadorCount = 0;
        int generatedExplosiveCubeCount = 0;
        var generatedTeletransports = new Teletransport2D[teletransportCellIndices.Length];

        DisableSceneTemplate(chestTemplate);
        DisableSceneTemplate(rebotadorTemplate);
        DisableSceneTemplate(teletransportTemplate);

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

                if (IsReservedCell(index, teletransportCellIndices))
                {
                    int teletransportSlot = GetReservedCellSlot(index, teletransportCellIndices);
                    generatedTeletransports[teletransportSlot] = GenerateTeletransport(
                        parent,
                        localPosition,
                        teletransportSlot + 1);
                    continue;
                }

                if (IsReservedCell(index, rebotadorCellIndices))
                {
                    GenerateRebotador(parent, localPosition, ++generatedRebotadorCount);
                    continue;
                }

                if (IsReservedCell(index, explosiveCubeCellIndices))
                {
                    GenerateExplosiveCube(
                        parent,
                        localPosition,
                        ++generatedExplosiveCubeCount);
                    continue;
                }

                if (IsReservedCell(index, chestCellIndices))
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

        LinkTeletransportPairs(generatedTeletransports);
    }

    List<int> BuildAvailableCells(int exitCellIndex)
    {
        var result = new List<int>();
        for (int i = 0; i < enabledCells.Length; i++)
        {
            if (enabledCells[i] && i != exitCellIndex)
                result.Add(i);
        }

        return result;
    }

    int[] ReserveRandomCells(List<int> availableCells, int requestedCount, string label)
    {
        int amount = Mathf.Min(requestedCount, availableCells.Count);
        if (amount < requestedCount)
        {
            Debug.LogWarning(
                $"PrefabGrid2D: sólo hay {amount} celdas disponibles para {requestedCount} {label}.",
                this);
        }

        int[] result = new int[amount];
        for (int i = 0; i < amount; i++)
        {
            int randomIndex = Random.Range(0, availableCells.Count);
            result[i] = availableCells[randomIndex];
            availableCells.RemoveAt(randomIndex);
        }

        return result;
    }

    int[] ReserveTeletransportCells(List<int> availableCells)
    {
        if (teletransportTemplate == null || teletransportPairCount <= 0)
            return System.Array.Empty<int>();

        int availablePairs = availableCells.Count / 2;
        int pairAmount = Mathf.Min(teletransportPairCount, availablePairs);
        if (pairAmount < teletransportPairCount)
        {
            Debug.LogWarning(
                $"PrefabGrid2D: sólo hay espacio para {pairAmount} de {teletransportPairCount} parejas de teletransportes.",
                this);
        }

        int[] result = new int[pairAmount * 2];
        for (int pair = 0; pair < pairAmount; pair++)
        {
            int firstListIndex = Random.Range(0, availableCells.Count);
            int firstCell = availableCells[firstListIndex];
            availableCells.RemoveAt(firstListIndex);

            int secondListIndex = FindTeletransportPartner(firstCell, availableCells);
            result[pair * 2] = firstCell;
            result[pair * 2 + 1] = availableCells[secondListIndex];
            availableCells.RemoveAt(secondListIndex);
        }

        return result;
    }

    int FindTeletransportPartner(int firstCell, List<int> candidates)
    {
        int bestDistance = -1;
        var preferred = new List<int>();

        for (int i = 0; i < candidates.Count; i++)
        {
            int distance = GetManhattanDistance(firstCell, candidates[i]);
            if (distance >= minimumTeletransportCellDistance)
                preferred.Add(i);

            if (distance > bestDistance)
                bestDistance = distance;
        }

        if (preferred.Count > 0)
            return preferred[Random.Range(0, preferred.Count)];

        var furthest = new List<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (GetManhattanDistance(firstCell, candidates[i]) == bestDistance)
                furthest.Add(i);
        }

        return furthest[Random.Range(0, furthest.Count)];
    }

    int GetManhattanDistance(int firstCell, int secondCell)
    {
        int firstColumn = firstCell % columns;
        int firstRow = firstCell / columns;
        int secondColumn = secondCell % columns;
        int secondRow = secondCell / columns;
        return Mathf.Abs(firstColumn - secondColumn) + Mathf.Abs(firstRow - secondRow);
    }

    static bool IsReservedCell(int cellIndex, int[] reservedCells)
    {
        for (int i = 0; i < reservedCells.Length; i++)
        {
            if (reservedCells[i] == cellIndex)
                return true;
        }

        return false;
    }

    static int GetReservedCellSlot(int cellIndex, int[] reservedCells)
    {
        for (int i = 0; i < reservedCells.Length; i++)
        {
            if (reservedCells[i] == cellIndex)
                return i;
        }

        return -1;
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

    void GenerateRebotador(Transform parent, Vector3 localPosition, int number)
    {
        GameObject instance = GenerateSpecial(
            rebotadorTemplate,
            parent,
            localPosition,
            $"GeneratedRebotador_{number}");
        if (instance.GetComponent<Rebotador2D>() == null)
            instance.AddComponent<Rebotador2D>();
    }

    void GenerateExplosiveCube(Transform parent, Vector3 localPosition, int number)
    {
        GameObject instance = Instantiate(prefab, parent);
        instance.name = $"GeneratedExplosiveCube_{number}";
        instance.transform.localPosition = localPosition;

        Obstacle2D regularObstacle = instance.GetComponent<Obstacle2D>();
        if (regularObstacle != null)
            regularObstacle.enabled = false;

        ExplosiveCube2D explosive = instance.GetComponent<ExplosiveCube2D>();
        if (explosive == null)
            explosive = instance.AddComponent<ExplosiveCube2D>();
        explosive.ActivateVariant();

        if (sortByRow)
            ApplyYSorting(instance);
    }

    bool HasExplosiveCubeVariant()
    {
        if (prefab == null || explosiveCubeCount <= 0)
            return false;

        Transform variant = prefab.transform.Find("ExplosiveCube");
        if (variant != null)
            return true;

        Debug.LogWarning(
            "PrefabGrid2D: el prefab de obstáculo no contiene un hijo ExplosiveCube.",
            this);
        return false;
    }

    Teletransport2D GenerateTeletransport(
        Transform parent,
        Vector3 localPosition,
        int number)
    {
        GameObject instance = GenerateSpecial(
            teletransportTemplate,
            parent,
            localPosition,
            $"GeneratedTeletransport_{number}");
        Teletransport2D teletransport = instance.GetComponent<Teletransport2D>();
        return teletransport != null
            ? teletransport
            : instance.AddComponent<Teletransport2D>();
    }

    GameObject GenerateSpecial(
        GameObject template,
        Transform parent,
        Vector3 localPosition,
        string instanceName)
    {
        GameObject instance = Instantiate(template, parent);
        instance.name = instanceName;
        instance.transform.localPosition = localPosition;
        instance.SetActive(true);

        if (sortByRow)
            ApplyYSorting(instance);

        return instance;
    }

    static void LinkTeletransportPairs(Teletransport2D[] teletransports)
    {
        for (int i = 0; i + 1 < teletransports.Length; i += 2)
        {
            if (teletransports[i] == null || teletransports[i + 1] == null)
                continue;

            teletransports[i].ConfigureDestination(teletransports[i + 1]);
            teletransports[i + 1].ConfigureDestination(teletransports[i]);
        }
    }

    static void DisableSceneTemplate(GameObject template)
    {
        if (template != null && template.scene.IsValid())
            template.SetActive(false);
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
            bool isGeneratedChest =
                child.name.StartsWith("GeneratedChest_") ||
                child.name.StartsWith("GeneratedEquipmentChest_") ||
                child.name.StartsWith("GeneratedUpgradeChest_");
            bool isGeneratedSpecial =
                child.name.StartsWith("GeneratedRebotador_") ||
                child.name.StartsWith("GeneratedTeletransport_") ||
                child.name.StartsWith("GeneratedExplosiveCube_");
            if (!isGeneratedCube && !isGeneratedChest && !isGeneratedSpecial)
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
