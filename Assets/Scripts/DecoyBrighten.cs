using UnityEngine;

/// <summary>
/// Put this on the decoy prefab (next to a Collider2D). Hovering it undoes the player's
/// progress: the screen brightens back up instead of fading to black.
/// ClickHealth assigns the target automatically when it spawns the decoy.
/// </summary>
public class DecoyBrighten : MonoBehaviour
{
    [Tooltip("Seconds of progress removed per second of hovering. Higher = brightens faster than the real target darkens.")]
    public float brightenSpeed = 2f;

    [HideInInspector] public ClickHealth target;

    void Awake()
    {
        if (GetComponent<Collider2D>() == null)
            Debug.LogError("DecoyBrighten: this object needs a Collider2D to detect the mouse.", this);
    }

    void OnMouseOver()
    {
        if (target != null) target.Restore(brightenSpeed * Time.deltaTime);
    }
}
