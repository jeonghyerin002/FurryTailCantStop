using UnityEngine;
using UnityEngine.InputSystem;

public class StickMover : MonoBehaviour
{
    public float speed = 5f;
    private Vector2 move;

    public void OnMove(InputValue value) { move = value.Get<Vector2>(); }

    void Update() { transform.Translate(move * speed * Time.deltaTime); }
}
