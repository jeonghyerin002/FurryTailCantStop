using UnityEngine;
using UnityEngine.EventSystems;

public class SwipeMover : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Transform player;
    public float step = 1f;
    private Vector2 start;
    public void OnPointerDown(PointerEventData e) { start = e.position; }
    public void OnPointerUp(PointerEventData e)
    {
        Vector2 delta = e.position - start;
        if (delta.magnitude < 50f) return;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            Move(delta.x > 0 ? Vector3.right : Vector3.left);
        else
            Move(delta.y > 0 ? Vector3.up : Vector3.down);
    }
    void Move(Vector3 dir) { player.position += dir * step; }
}
