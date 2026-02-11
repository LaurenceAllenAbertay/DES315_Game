using UnityEngine;

//Teleports the player from light to shadow, leaving a 1 - Health clone behind, the clone acts as a decoy to draw enemy attention -EM//
[CreateAssetMenu(fileName = "TeleportEffect", menuName = "Abilities/Effects/Teleport")]
public class TeleportEffect : AbilityEffect
{
    [Header("Teleport Settings")]
    [Tooltip("Prefab for the clone that gets left behind")]
    public GameObject clonePrefab;

    [Tooltip("How long the clone lives before automatically dying (seconds). Set to 0 for infinite")]
    public float cloneLifetime = 10f;

    [Header("Requirements")]
    [Tooltip("Does caster need to be in light to teleport?")]
    public bool requireCasterInLight = true;
    [Tooltip("Does target point need to be in shadow to teleport")]
    public bool requireTargetInShadow = true;

    public override void Execute(AbilityExecutionContext context)
    {
        //This effect must target self (the caster moves)//
        if(!targetSelf)
        {
            Debug.LogWarning("[TeleportEffect] TeleportEffect should have targetSelf = true!");
            return;
        }

        if(context.Caster == null)
        {
            Debug.LogWarning("[TeleportEffect] No Caster found!");
            return;
        }

        //Check light requirements//
        if(requireCasterInLight && !context.IsCasterInLight())
        {
            MessageUI.Instance?.EnqueueMessage("You must be in light to use the ability!");
            Debug.Log("[TeleportEffect] Teleport failed: Caster not in light");
            return;
        }

        if (requireTargetInShadow && LightDetectionManager.Instance != null)
        {
            if(LightDetectionManager.Instance.IsPointInLight(context.TargetPoint))
            {
                MessageUI.Instance?.EnqueueMessage("You can only teleport into shadow!");
                Debug.Log("[TeleportEffect] Teleport failed: Target point not in shadow");
                return;
            }
        }

        //Store original position for clone spawning//
        Vector3 originalPosition = context.Caster.transform.position;
        Quaternion originalRotation = context.Caster.transform.rotation;

        //Teleport the player//
        context.Caster.transform.position = context.TargetPoint;

        Debug.Log($"[TeleportEffect] Player teleported from {originalPosition} to {context.TargetPoint}");
        MessageUI.Instance?.EnqueueMessage("You teleprted into the shadows!");

        //Spawn the clone at original position//
        if(clonePrefab != null)
        {
            SpawnClone(originalPosition, originalRotation, context.Caster);
        }
        else
        {
            Debug.LogWarning("[TeleportEffect] No clone prefab assigned!");
        }
    }

    //Spawn a 1 - Health clone at the specified position -EM//
    private void SpawnClone(Vector3 position, Quaternion rotation, Player originalPlayer)
    {
        GameObject cloneObject = GameObject.Instantiate(clonePrefab, position, rotation);
        cloneObject.name = "PlayerClone_Decoy";

        //Get the unit component (could be Enemy or custom clone component)//
        Unit cloneUnit = cloneObject.GetComponent<Unit>();

        if(cloneUnit != null )
        {
            //Set clone to 1 health -EM//
            cloneUnit.SetMaxHealth(1f, adjustCurrentHealth: true);

            Debug.Log($"[TeleportEffect] Clone spawned at {position} with 1 health");
            MessageUI.Instance?.EnqueueMessage("A shadow decoy remains in your place!");

            //Auto-destroy after lifetime if set//
            if(cloneLifetime > 0f)
            {
                GameObject.Destroy(cloneObject, cloneLifetime);   
            }

            //Subscribe to death event to clean up//
            cloneUnit.OnDied += (unit) =>
            {
                Debug.Log("[TeleportEffect] Clone was destroyed");
                if (cloneObject != null)
                {
                    GameObject.Destroy(cloneObject, 0.1f);
                }
            };
        }
        else
        {
            Debug.LogWarning("[TeleportEffect] Clone prefab doesn't have a Unit component!");
            GameObject.Destroy(clonePrefab);
        }
    }
}
