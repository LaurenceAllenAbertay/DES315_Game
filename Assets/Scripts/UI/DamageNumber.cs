using TMPro;
using UnityEngine;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;

    private Camera mainCamera;
    
    public void Initialise(float damage, Vector3 centreWorldPos)
    {
        transform.position = centreWorldPos + Random.insideUnitSphere;
        label.text = damage.ToString("0");
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }
    
    public void DestroyNumber()
    {
        Destroy(gameObject);
    }
}