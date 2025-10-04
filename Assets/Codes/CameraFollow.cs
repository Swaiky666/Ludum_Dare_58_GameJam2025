using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float dampSpeed = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPosition, dampSpeed * Time.deltaTime);
    }
}