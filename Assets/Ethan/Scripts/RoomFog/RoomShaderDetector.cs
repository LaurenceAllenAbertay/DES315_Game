using UnityEngine;

//Detects when player enters a room for shader based fog of war -EM//

public class RoomShaderDetector : MonoBehaviour
{
    [HideInInspector]
    public ShaderFogOfWar fogManager;

    [HideInInspector]
    public LayerMask playerLayerMask;

    private void OnTriggerEnter(Collider other)
    {
        //Check if player entered//
        if(((1 << other.gameObject.layer) & playerLayerMask) != 0)
        {
            if(fogManager != null)
            {
                fogManager.OnRoomEntered(gameObject);
            }
        }
    }

}
