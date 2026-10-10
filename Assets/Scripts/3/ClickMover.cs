using UnityEngine;

public class ClickMover : MonoBehaviour
{
    public float step = 1f;

    public void MoveRight()
    {
        transform.position += Vector3.right * step;
    }
    public void MoveLeft()
    {
        transform.position += Vector3.left * step;
    }
    public void MoveUp()
    {
        transform.position += Vector3.up * step;
    }
    public void MoveDown()
    {
        transform.position += Vector3.down * step;
    }
}
