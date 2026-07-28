using UnityEngine;
using DG.Tweening;

public class Player2D : MonoBehaviour
{
    [SerializeField] bool isAlive = true;
    [SerializeField] bool blocked;
    [Header("Orden visual")]
    [SerializeField] string sortingLayerName = "World";
    [SerializeField, Min(1)] int sortingUnitsPerWorldUnit = 100;
    [SerializeField] int sortingOrderOffset;
    [Tooltip("Ajusta el punto Y que representa el contacto del jugador con el suelo.")]
    [SerializeField] float sortingPointYOffset;

    [Header("Caras")]
    [SerializeField] Transform facesRoot;
    [SerializeField] string defaultFaceName = "1";
    [SerializeField, Min(0.05f)] float reactionFaceDuration = 0.65f;
    [SerializeField] bool avoidRepeatingLastFace = true;
    [SerializeField, Min(0f)] float faceHitCooldown = 0.05f;

    Transform[] faces;
    Tween faceResetTween;
    int defaultFaceIndex;
    int activeFaceIndex = -1;
    float lastFaceHitTime = -10f;

    public bool IsAlive => isAlive;
    public bool CanSling => isAlive && !blocked;

    void Awake()
    {
        YSort2D ySort = GetComponent<YSort2D>();
        if (ySort == null)
            ySort = gameObject.AddComponent<YSort2D>();

        ySort.Configure(
            sortingLayerName,
            sortingUnitsPerWorldUnit,
            sortingOrderOffset,
            true,
            sortingPointYOffset);

        CacheFaces();
        ShowDefaultFace();
    }

    void OnDestroy()
    {
        faceResetTween?.Kill();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Obstacle") ||
            Time.time - lastFaceHitTime < faceHitCooldown)
            return;

        lastFaceHitTime = Time.time;
        ShowReactionFace();
    }

    public void SetBlocked(bool value) => blocked = value;

    public void SetAlive(bool value)
    {
        isAlive = value;
        if (!isAlive)
            blocked = true;
    }

    void CacheFaces()
    {
        if (facesRoot == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Caras"))
                {
                    facesRoot = child;
                    break;
                }
            }
        }

        if (facesRoot == null || facesRoot.childCount == 0)
        {
            faces = System.Array.Empty<Transform>();
            return;
        }

        faces = new Transform[facesRoot.childCount];
        defaultFaceIndex = 0;

        for (int i = 0; i < faces.Length; i++)
        {
            faces[i] = facesRoot.GetChild(i);
            faces[i].gameObject.SetActive(false);

            if (faces[i].name == defaultFaceName)
                defaultFaceIndex = i;

            SpriteRenderer faceRenderer = faces[i].GetComponent<SpriteRenderer>();
            if (faceRenderer != null)
            {
                faceRenderer.sortingLayerName = sortingLayerName;
                faceRenderer.sortingOrder = 1;
            }
        }
    }

    void ShowReactionFace()
    {
        if (faces == null || faces.Length == 0)
            return;

        faceResetTween?.Kill();
        int nextIndex = GetRandomReactionFaceIndex();

        for (int i = 0; i < faces.Length; i++)
            faces[i].gameObject.SetActive(i == nextIndex);

        activeFaceIndex = nextIndex;
        faceResetTween = DOVirtual.DelayedCall(reactionFaceDuration, ShowDefaultFace)
            .SetTarget(this);
    }

    int GetRandomReactionFaceIndex()
    {
        if (faces.Length <= 1)
            return defaultFaceIndex;

        int nextIndex;
        int attempts = 0;

        do
        {
            nextIndex = Random.Range(0, faces.Length);
            attempts++;
        }
        while ((nextIndex == defaultFaceIndex ||
                (avoidRepeatingLastFace && nextIndex == activeFaceIndex)) &&
               attempts < 30);

        if (nextIndex == defaultFaceIndex)
            nextIndex = (defaultFaceIndex + 1) % faces.Length;

        return nextIndex;
    }

    void ShowDefaultFace()
    {
        if (faces == null || faces.Length == 0)
            return;

        for (int i = 0; i < faces.Length; i++)
            faces[i].gameObject.SetActive(i == defaultFaceIndex);

        faceResetTween = null;
    }
}
