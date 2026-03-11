using Unity.Mathematics;
using UnityEngine;

//Floats above the player and rotates to point toward the WinObjective//
//Toggled on/off by CheatManager.ToggelShowFinalRoomArrow()//
//Add this component to a GameObject in the scene that has an arrow mesh/sprite as a child -EM//
public class FinalRoomArrow : MonoBehaviour
{
    [Header("Refernces")]
    [Tooltip("The player transform to follow")]
    public Transform playerTransform;

    [Tooltip("The WinCondition object to point toward - drag the WinObjective in")]
    public Transform targetTransform;

    [Header("Float Settings")]
    [Tooltip("How high above the player the arrow floats")]
    public float heightAbovePlayer = 2.5f;

    [Tooltip("Bob up and down speed")]
    public float bobSpeed = 1.5f;

    [Tooltip("Bob up and down height")]
    public float bobHeight = 0.15f;

    [Header("Rotation Settings")]
    [Tooltip("How fast the arrow rotates to face the target")]
    public float rotationSpeed = 5f;

    [Tooltip("Axis offset - adjust if arrow mesh point the wrong way by default")]
    public float arrowForwardOffset = 0f;

    [Header("Procedural Arrow (used if no child model exisits)")]
    [Tooltip("Colour of the auto-built arrow")]
    public Color arrowColour = Color.yellow;

    [Tooltip("Length of the arrow shaft")]
    public float shaftLength = 0.8f;

    [Tooltip("Size of the arrow head")]
    public float headSize = 0.35f;

    [Header("Debug")]
    public bool debugMode = true;

    private float bobTimer = 0;
    private bool isVisible = false;

    private void Awake()
    {
        //Start hidden//
        gameObject.SetActive(false);
    }

    private void Start()
    {
        //Find player if not assigned//
        if(playerTransform == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) playerTransform = pc.transform;
        }

        //Find WinCondition if not assigned//
        if (targetTransform == null)
        {
            WinCondition win = FindAnyObjectByType<WinCondition>();
            if (win != null)
            {
                targetTransform = win.transform;
                if (debugMode) Debug.Log($"[FinalRoomArrow] Found WinCondition at {targetTransform.position}");
            }
            else
            {
                Debug.LogWarning("[FinalRoomArrow] No WinCondition found in scene - arrow will not point anywhere.");
            }
        }

        //if no child model has been added by an artits, build a dafault arrow//
        if(transform.childCount == 0)
        {
            BuildDefaultArrow();
            if (debugMode) Debug.Log("[FinalRoomArrow] No child model found - built procedural arrow.");
        }
    }

    private void Update()
    {
        if (!isVisible) return;
        if (playerTransform == null) return;

        //Follow player at fixed height with bob//
        bobTimer += Time.deltaTime;
        float bob = Mathf.Sin(bobTimer * bobSpeed) * bobHeight;
        transform.position = playerTransform.position + Vector3.up * (heightAbovePlayer + bob);

        //Rotate to point toward the target, flat on the XZ plane//
        if(targetTransform != null)
        {
            Vector3 directionToTarget = targetTransform.position - transform.position;
            directionToTarget.y = 0f;

            if(directionToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget) * Quaternion.Euler(0f, arrowForwardOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }

    //Called by CheatManager to show or hide the arrow -EM//
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        gameObject.SetActive(visible);

        if (debugMode) Debug.Log($"[FinalRoomArrow] Arrow {(visible ? "shown" : "hidden")}");
    }

    //Builds a simple arrow from primitives pointing forward//
    //The parent rotates to face the target so the arrow always points correctly -EM//
    private void BuildDefaultArrow()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = arrowColour;

        //Shaft: a thin stretched cube//
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(shaft.GetComponent<Collider>());
        shaft.transform.SetParent(transform);
        shaft.transform.localPosition = new Vector3(0f, 0f, shaftLength * 0.5f); //Centre shaft along Z - axis//
        shaft.transform.localScale = new Vector3(0.08f, 0.08f, shaftLength);
        shaft.transform.localRotation = Quaternion.identity;
        shaft.GetComponent<Renderer>().material = mat;
        shaft.name = "ArrowShaft";

        //Heade: a cylinder rotated 90 degrees on X so its tip points along Z//
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(transform);
        head.transform.localPosition = new Vector3(0f, 0f, shaftLength + headSize * 0.3f);
        head.transform.localScale = new Vector3(headSize, headSize * 0.5f, headSize);
        head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        head.GetComponent<Renderer>().material = mat;
        head.name = "ArrowHead";
    }
}
