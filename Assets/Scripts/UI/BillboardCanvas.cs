using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            return;
        }

        transform.rotation = mainCamera.transform.rotation;
    }
}