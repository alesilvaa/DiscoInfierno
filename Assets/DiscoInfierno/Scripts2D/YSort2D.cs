using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ordena un objeto 2D según su posición vertical:
/// cuanto más abajo está en pantalla, más adelante se dibuja.
/// SortingGroup mantiene unidos todos los SpriteRenderer hijos.
/// </summary>
[DisallowMultipleComponent]
public class YSort2D : MonoBehaviour
{
    [SerializeField] string sortingLayerName = "World";
    [SerializeField, Min(1)] int unitsPerWorldUnit = 100;
    [SerializeField] int orderOffset;
    [SerializeField] Transform sortPoint;
    [SerializeField] float sortPointYOffset;
    [SerializeField] bool updateContinuously = true;

    SortingGroup sortingGroup;

    void Awake()
    {
        EnsureSortingGroup();
        RefreshSortingOrder();
    }

    void LateUpdate()
    {
        if (updateContinuously)
            RefreshSortingOrder();
    }

    void OnValidate()
    {
        unitsPerWorldUnit = Mathf.Max(1, unitsPerWorldUnit);
        EnsureSortingGroup();
        RefreshSortingOrder();
    }

    public void Configure(
        string layerName,
        int precision,
        int offset = 0,
        bool continuous = true,
        float pointYOffset = 0f,
        Transform point = null)
    {
        sortingLayerName = layerName;
        unitsPerWorldUnit = Mathf.Max(1, precision);
        orderOffset = offset;
        updateContinuously = continuous;
        sortPointYOffset = pointYOffset;
        sortPoint = point;
        EnsureSortingGroup();
        RefreshSortingOrder();
    }

    public void RefreshSortingOrder()
    {
        EnsureSortingGroup();

        Transform reference = sortPoint != null ? sortPoint : transform;
        sortingGroup.sortingLayerName = sortingLayerName;
        sortingGroup.sortingOrder =
            orderOffset -
            Mathf.RoundToInt((reference.position.y + sortPointYOffset) * unitsPerWorldUnit);
    }

    void EnsureSortingGroup()
    {
        if (sortingGroup == null)
            sortingGroup = GetComponent<SortingGroup>();

        if (sortingGroup == null)
            sortingGroup = gameObject.AddComponent<SortingGroup>();
    }
}
