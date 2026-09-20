using UnityEngine;

public class BoxWalls : MonoBehaviour
{
    void Awake()
    {
        Bounds b = GetComponent<SpriteRenderer>().sprite.bounds;
        Vector2 c = b.center, e = b.extents;

        var edge = gameObject.AddComponent<EdgeCollider2D>();
        edge.points = new Vector2[]
        {
            c + new Vector2(-e.x, -e.y),
            c + new Vector2( e.x, -e.y),
            c + new Vector2( e.x,  e.y),
            c + new Vector2(-e.x,  e.y),
            c + new Vector2(-e.x, -e.y),
        };
    }
}