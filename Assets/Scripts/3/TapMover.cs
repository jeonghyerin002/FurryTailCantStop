using UnityEngine;
using UnityEngine.EventSystems;

public class TapMover : MonoBehaviour, IPointerDownHandler
{
    public Transform player;
    public float speed = 8f;
    private Vector3 target;
    void Start() { target = player.position; }
    public void OnPointerDown(PointerEventData e)
    {
        target = Camera.main.ScreenToWorldPoint(e.position);
        target.z = player.position.z;
    }
    void Update()
    {
        player.position = Vector3.MoveTowards(
            player.position, target, speed * Time.deltaTime);
    }
}
