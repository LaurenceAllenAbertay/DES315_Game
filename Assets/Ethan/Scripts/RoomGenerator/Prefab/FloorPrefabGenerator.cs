using UnityEngine;
using Unity.VisualScripting;


#if UNITY_EDITOR
using UnityEditor;
#endif

//Helper to quickly create test floor prefabs for each room type -EM//
//Creates only geometry - artists can assign materials later - EM//

public class FloorPrefabGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Settings")]
    [Tooltip("Where to save generated prefabs")]
    public string savePath = "Assets/Ethan/Scripts/RoomGenerator/Prefab";

    [ContextMenu("Create All Floor Prefabs")]
    private void CreateAllFloorPrefabs()
    {
        CreateLobbyFloor();
        CreateType1Floor();
        CreateType2Floor();
        CreateType3Floor();
        CreateType4Floor();

        Debug.Log("[FloorPrefabGenerator] Created all 5 floor prefabs");
    }

    [ContextMenu("Create lobby floor")]
    private void CreateLobbyFloor()
    {
        GameObject floor = CreateBaseFloor("Floor_Lobby");

        //Add a center decoration for lobby//
        GameObject centerPiece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        centerPiece.name = "CenterDecoration";
        centerPiece.transform.SetParent(floor.transform);
        centerPiece.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        centerPiece.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);

        SaveAsPrefab(floor, "Floor_lobby.prefab");
    }

    [ContextMenu("Create Type 1 Floor (Pillared Hall)")]
    private void CreateType1Floor()
    {
        GameObject floor = CreateBaseFloor("Floor_Type1");

        //Add Dynamic pillar spawner//
        DynamicFloorProps propsScript = floor.AddComponent<DynamicFloorProps>();
        propsScript.spawnPillars = true;
        propsScript.minPillars = 2;
        propsScript.maxPillars = 4;
        propsScript.edgeBuffer = 0.25f;

        SaveAsPrefab(floor, "Floor_Type1.prefab");
    }

    [ContextMenu("Create Type 2 Floor (Plain)")]
    private void CreateType2Floor()
    {
        GameObject floor = CreateBaseFloor("Floor_Type2");

        //Just a plain room with no props//

        SaveAsPrefab(floor, "Floor_Type2.prefab");
    }

    [ContextMenu("Create Type 3 Floor (Pillared)")]
    private void CreateType3Floor()
    {
        GameObject floor = CreateBaseFloor("Floor_Type3");

        //Add Dynamic pillar spawner//
        DynamicFloorProps propsScript = floor.AddComponent<DynamicFloorProps>();
        propsScript.spawnPillars = true;
        propsScript.minPillars = 2;
        propsScript.maxPillars = 4;
        propsScript.edgeBuffer = 0.25f;

        SaveAsPrefab(floor, "Floor_Type3.prefab");
    }

    [ContextMenu("Create Type 4 Floor (Pillared)")]
    private void CreateType4Floor()
    {
        GameObject floor = CreateBaseFloor("Floor_Type4");

        //Add Dynamic pillar spawner//
        DynamicFloorProps propsScript = floor.AddComponent<DynamicFloorProps>();
        propsScript.spawnPillars = true;
        propsScript.minPillars = 2;
        propsScript.maxPillars = 4;
        propsScript.edgeBuffer = 0.25f;

        SaveAsPrefab(floor, "Floor_Type4.prefab");
    }

    private GameObject CreateBaseFloor(string name)
    {
        //Create container//
        GameObject container = new GameObject(name);
        container.transform.position = Vector3.zero;

        //Create floor base (cube slightly above ground)//
        GameObject floorBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorBase.name = "FloorBase";
        floorBase.transform.SetParent(container.transform);
        floorBase.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        floorBase.transform.localScale = new Vector3(1f, 0.1f, 1f);

        return container;
    }

    private void SaveAsPrefab(GameObject obj, string fileName)
    {
        //Ensure the directiory exists//
        if(!AssetDatabase.IsValidFolder(savePath))
        {
            string[] folders = savePath.Split('/');
            string currentPath = folders[0];
            for(int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if(!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = newPath;
            }
        }

        string fullPath = savePath + "/" + fileName;

       GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, fullPath);
        if(prefab != null)
        {
            Debug.Log($"[FloorPrefabGenerator] Created : {fullPath}");
        }
        else
        {
            Debug.Log($"[FloorPrefabGenerator] Failed to create prefab : {fullPath}");
        }
            
        //Clean up the scene object//
        DestroyImmediate(obj);
        AssetDatabase.Refresh();
    }
#endif
}
