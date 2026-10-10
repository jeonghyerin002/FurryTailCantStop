using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.Linq.Expressions;

public class TapMover : MonoBehaviour
{
    public Transform player;
    public float speed = 8f;

    private Camera mainCamera;

    Vector2 targetPosition;
    void Start()
    {
        if (player == null)
        {
            player = transform;
        }
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        targetPosition = player.position;
    }
    void Update()
    {
        Vector2 screenPosition = Vector2.zero;
        int pointerId = -1;
        bool pressed = false;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                screenPosition = touch.position;
                pointerId = touch.fingerId;
                pressed = true;
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            pressed = true;
        }
        if (pressed)
        {
            if (!IsTargetClicked(screenPosition, pointerId))
            {
                if (mainCamera != null)
                {
                    Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -mainCamera.transform.position.z));

                    targetPosition = new Vector2(worldPosition.x, worldPosition.y);
                }
            }
        }
        player.position = Vector2.MoveTowards(player.position, targetPosition, speed * Time.deltaTime);
    }
    bool IsTargetClicked(Vector2 screenPosition, int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current);

        pointerData.position = screenPosition;
        pointerData.pointerId = pointerId;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.GetComponentInParent<Target>() != null)
            {
                return true;
            }
        }
        return false;
    }
}
