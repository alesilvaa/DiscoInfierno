using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class ExitPortal2D : MonoBehaviour
{
    bool transitionRequested;

    void Reset()
    {
        Collider2D exitCollider = GetComponent<Collider2D>();
        if (exitCollider != null)
            exitCollider.isTrigger = true;
    }

    void OnEnable()
    {
        transitionRequested = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryEnter(other.GetComponentInParent<Player2D>());
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryEnter(collision.gameObject.GetComponentInParent<Player2D>());
    }

    void TryEnter(Player2D player)
    {
        if (transitionRequested || player == null || !player.IsAlive)
            return;

        GameController game = GameController.Instance;
        if (game == null || !game.ExitUnlocked)
            return;

        transitionRequested = true;
        player.SetBlocked(true);
        player.GetComponent<SlingMovement2D>()?.StopImmediately();
        if (!game.TryCompleteLevel())
        {
            transitionRequested = false;
            player.SetBlocked(false);
        }
    }
}
