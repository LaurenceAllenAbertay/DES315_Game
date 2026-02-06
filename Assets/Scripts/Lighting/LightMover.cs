using UnityEngine;

public class LightMover : MonoBehaviour
{
    public Vector3 pointA;
    public Vector3 pointB;
    public float speed = 3f;

    private Vector3 target;

    private void Start()
    {
        float distA = Vector3.Distance(transform.position, pointA);
        float distB = Vector3.Distance(transform.position, pointB);
        target = distA < distB ? pointB : pointA;
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            target = target == pointA ? pointB : pointA;
        }
    }
}