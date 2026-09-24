using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extremo de un par de portales. Conserva velocidad y estado de vuelo del Player.
/// </summary>
[DisallowMultipleComponent]
public sealed class Teletransport2D : MonoBehaviour
{
    [Header("Pareja")]
    [SerializeField] Teletransport2D destination;
    [Tooltip("Pequeño desplazamiento desde el centro de salida en la dirección de viaje.")]
    [SerializeField, Min(0f)] float exitOffset = 0.35f;
    [SerializeField, Min(0.05f)] float reentryCooldown = 0.4f;

    [Header("Área de activación")]
    [SerializeField, Min(0.1f)] float triggerRadius = 1.35f;

    [Header("Efecto visual")]
    [Tooltip("Se rota solamente este hijo para no modificar el trigger del portal.")]
    [SerializeField] Transform visualRoot;
    [SerializeField] float rotationSpeedZ = 90f;

    readonly Dictionary<int, float> blockedBodies = new Dictionary<int, float>();
    Collider2D portalTrigger;

    public Teletransport2D Destination => destination;

    void Awake()
    {
        ResolveVisualRoot();
        ConfigureTrigger();
    }

    void Update()
    {
        if (visualRoot != null && !Mathf.Approximately(rotationSpeedZ, 0f))
            visualRoot.Rotate(0f, 0f, rotationSpeedZ * Time.deltaTime, Space.Self);
    }

    public void ConfigureDestination(Teletransport2D other)
    {
        destination = other != this ? other : null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Player2D player = other.GetComponentInParent<Player2D>();
        if (player == null || !player.IsAlive || destination == null)
            return;

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody == null)
            return;

        int bodyId = playerBody.GetInstanceID();
        if (IsBlocked(bodyId))
            return;

        float blockedUntil = Time.time + reentryCooldown;
        Block(bodyId, blockedUntil);
        destination.Block(bodyId, blockedUntil);

        Vector2 travelDirection = playerBody.linearVelocity.sqrMagnitude > 0.001f
            ? playerBody.linearVelocity.normalized
            : ((Vector2)destination.transform.position - (Vector2)transform.position).normalized;
        if (travelDirection.sqrMagnitude < 0.001f)
            travelDirection = Vector2.up;

        Vector2 arrivalPosition = (Vector2)destination.transform.position
            + travelDirection * destination.exitOffset;

        SlingMovement2D sling = player.GetComponent<SlingMovement2D>();
        if (sling != null)
            sling.TeleportTo(arrivalPosition);
        else
            playerBody.position = arrivalPosition;
    }

    bool IsBlocked(int bodyId)
    {
        if (!blockedBodies.TryGetValue(bodyId, out float blockedUntil))
            return false;

        if (Time.time < blockedUntil)
            return true;

        blockedBodies.Remove(bodyId);
        return false;
    }

    void Block(int bodyId, float blockedUntil)
    {
        blockedBodies[bodyId] = blockedUntil;
    }

    void ConfigureTrigger()
    {
        portalTrigger = GetComponent<Collider2D>();
        if (portalTrigger == null)
        {
            CircleCollider2D generated = gameObject.AddComponent<CircleCollider2D>();
            generated.radius = triggerRadius;
            portalTrigger = generated;
        }

        portalTrigger.isTrigger = true;
    }

    void ResolveVisualRoot()
    {
        if (visualRoot != null)
            return;

        Renderer renderer = GetComponentInChildren<Renderer>(true);
        visualRoot = renderer != null && renderer.transform != transform
            ? renderer.transform
            : null;
    }

    void OnValidate()
    {
        exitOffset = Mathf.Max(0f, exitOffset);
        reentryCooldown = Mathf.Max(0.05f, reentryCooldown);
        triggerRadius = Mathf.Max(0.1f, triggerRadius);

        if (portalTrigger is CircleCollider2D circle)
            circle.radius = triggerRadius;
    }
}
