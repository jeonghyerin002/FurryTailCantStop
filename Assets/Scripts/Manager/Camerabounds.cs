using UnityEngine;

/// <summary>
/// 오쏘그래픽 카메라 화면 안으로 좌표를 제한하는 유틸.
/// TargetMover, RivalAI, StunController, PlayerController가 공통으로 사용.
/// </summary>
public static class CameraBounds
{
    public static Vector2 Clamp(Vector2 pos, float padding)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return pos;

        float halfH = Mathf.Max(0f, cam.orthographicSize - padding);
        float halfW = Mathf.Max(0f, cam.orthographicSize * cam.aspect - padding);
        Vector2 c = cam.transform.position;
        return new Vector2(
            Mathf.Clamp(pos.x, c.x - halfW, c.x + halfW),
            Mathf.Clamp(pos.y, c.y - halfH, c.y + halfH));
    }
}