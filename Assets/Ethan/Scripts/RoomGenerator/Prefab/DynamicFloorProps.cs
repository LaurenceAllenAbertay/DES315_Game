using UnityEditor.Compilation;
using UnityEngine;

//Attach to floor prefabs to dynamically place props based on actual room size -EM//
public class DynamicFloorProps : MonoBehaviour
{
    [Header("Pillar Settings")]
    public bool spawnPillars = true;
    public GameObject pillarPrefab;
    public int minPillars = 2;
    public int maxPillars = 6;

    [Tooltip("Minimum distance from edges (in unit space 0 -1")]
    [Range(0.1f, 0.4f)]
    public float edgeBuffer = 0.25f;

    [Tooltip("Minimum distance between pillars (in unit space")]
    [Range(1f, 10f)]
    public float minPillarDistance = 4f;

    [Header("Decoration Settings")]
    public bool spawnDecorations = false;
    public GameObject[] decorationPrefabs;
    public int minDecorations = 3;
    public int maxDecorations = 8;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private bool hasSpawned = false;

    private void Start()
    {
        //Delay spawning by one fram to ensure parent scailing is applied//
        StartCoroutine(DelayedSpawn());
    }

    private System.Collections.IEnumerator DelayedSpawn()
    {
        //Wait for one frame for room builder to finish scailing//
        yield return null;

        if(!hasSpawned)
        {
            SpawnProps();
        }
    }

    private void SpawnProps()
    {
        hasSpawned = true;

        Vector3 scale = transform.localScale;

        //Only spawn props if they are actually scaled (instantiated by RoomBuilder)//
        if(scale.x > 1.1f || scale.z > 1.1f)
        {
            if(spawnPillars)
            {
                SpawnPillarsForRoom(scale);
            }

            if(spawnDecorations && decorationPrefabs != null && decorationPrefabs.Length > 0)
            {
                SpawnDecorationsForRoom(scale);
            }
        }
        else
        {
            if(showDebugLogs)
            {
                Debug.Log($"[DynamicFloorProps] {gameObject.name} - Scale too small, not spawning props");
            }
        }
    }

    private void SpawnPillarsForRoom(Vector3 roomScale)
    {
        if(pillarPrefab == null)
        {
            //Create default pillar//
            pillarPrefab = CreateDefaultPillar();
        }

        //Determine number of pillars based on room size//
        float roomArea = roomScale.x * roomScale.z;
        int pillarCount = Mathf.RoundToInt(Mathf.Lerp(minPillars,maxPillars, roomArea / 100f));
        pillarCount = Mathf.Clamp(pillarCount, minPillars, maxPillars);

        if(showDebugLogs)
        {
            Debug.Log($"[DynamicFloorProps] Spawning {pillarCount} pillars (area: {roomArea})");
        }

        //Keep track of pillar positions to ensure spacing//
        System.Collections.Generic.List<Vector3> pillarPositions = new System.Collections.Generic.List<Vector3>();

        //Generate random position with minimum spacing//
        int attempts = 0;
        //Prevent and infinite loop//
        int maxAttempts = pillarCount * 20;

        //Generate random positions that don't overlap//
        while (pillarPositions.Count < pillarCount && attempts < maxAttempts)
        {
            attempts++;

            //Get position in unit space (0-1 range)//
            Vector3 unitPos = GetRandomUnitPosition();

            //Convert to world space based on room scale//
            Vector3 worldOffset = new Vector3((unitPos.x - 0.5f) * roomScale.x, 0f, (unitPos.z - 0.5f) * roomScale.z);

            Vector3 worldPos = transform.position + worldOffset;

            //Check if this position is far enough from other pillars//
            bool tooClose = false;
            foreach (Vector3 existingPos in pillarPositions)
            {
                float distance = Vector3.Distance(worldPos, existingPos);
                if (distance < minPillarDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                //Position is good, spawn pillar//
                pillarPositions.Add(worldPos);

                //Raise pillar so it sits on floor//
                Vector3 raisedPos = worldPos + Vector3.up * 1.5f;

                GameObject pillar = Instantiate(pillarPrefab, raisedPos, Quaternion.identity, transform.parent);
                pillar.name = $"Pillar_{pillarPositions.Count}_{gameObject.name}";

                if (showDebugLogs)
                {
                    Debug.Log($"[DynamicFloorProps] Spawned pillar at world pos {worldPos}");
                }
            }
        }
    }

    private Vector3 GetRandomUnitPosition()
    {
        //Return position in 0-1 space, respecting edge buffer//
        float x = Random.Range(edgeBuffer, 1f - edgeBuffer);
        float z = Random.Range(edgeBuffer, 1f - edgeBuffer);
        return new Vector3(x, 0f, z);
    }

    private void SpawnDecorationsForRoom(Vector3 roomScale)
    {
        int decorCount = Random.Range(minDecorations, maxDecorations + 1);

        for (int i = 0; i < decorCount; i++)
        {
            GameObject decorPrefab = decorationPrefabs[Random.Range(0, decorationPrefabs.Length)];
            Vector3 unitPos = GetRandomUnitPosition();

            //Convert to world space//
            Vector3 worldOffset = new Vector3((unitPos.x - 0.5f) * roomScale.x, 0f, (unitPos.z - 0.5f) * roomScale.z);

            Vector3 worldPos = transform.position + worldOffset;

            GameObject decor = Instantiate(decorPrefab,worldPos, Quaternion.identity, transform.parent);
            decor.name = $"Decoration_{i}";
            decor.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }

    private GameObject CreateDefaultPillar()
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Default";
        pillar.transform.localScale = new Vector3(0.8f, 2f, 0.8f);

        //Artsit can assign material later//
        return pillar;
    }

    //Call this from roomBuilder for manual prop spawning -EM//
    public void SpawnPropsManually()
    {
        if(!hasSpawned)
        {
            SpawnProps();
        }
    }

    //Visualise the spawn areas -EM//
    private void OnDrawGizmosSelected()
    {
        //Visualise spawn area//
        Vector3 scale = transform.localScale;

        float minX = -scale.x * 0.5f + (scale.x * edgeBuffer);
        float maxX = scale.x * 0.5f - (scale.x * edgeBuffer);
        float minZ = -scale.z * 0.5f + (scale.z * edgeBuffer);
        float maxZ = scale.z * 0.5f - (scale.z * edgeBuffer);

        float width = maxX - minX;
        float depth = maxZ - minZ;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(width, 0.1f ,depth));
    }

}
