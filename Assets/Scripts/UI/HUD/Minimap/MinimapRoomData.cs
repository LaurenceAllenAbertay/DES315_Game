using UnityEngine;

/// <summary>
/// Attached to a room GameObject (alongside RoomLA) when the player first visits it.
/// Fires a downward raycast grid over the room's XZ extent to produce a low-res
/// Texture2D showing floor vs occluder pixels, then caches the result.
/// </summary>
public class MinimapRoomData : MonoBehaviour
{
    public Texture2D ScannedTexture { get; private set; }
    public Color[]   PixelCache     { get; private set; }
    public Bounds    WorldBounds    { get; private set; }
    public bool      HasBeenScanned { get; private set; }

    public void ScanRoom(
        LayerMask occluderMask,
        Color     floorColor,
        Color     occluderColor,
        float     pixelsPerUnit,
        float     scanHeightOffset)
    {
        RoomLA room = GetComponent<RoomLA>();
        if (room == null) return;

        Collider[] cols = room.BoundaryColliders;
        if (cols == null || cols.Length == 0) return;

        Bounds combined = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++)
            combined.Encapsulate(cols[i].bounds);
        WorldBounds = combined;

        int texW = Mathf.Clamp(Mathf.CeilToInt(combined.size.x * pixelsPerUnit), 4, 128);
        int texH = Mathf.Clamp(Mathf.CeilToInt(combined.size.z * pixelsPerUnit), 4, 128);

        Color[] pixels = new Color[texW * texH];
        float   rayLen = combined.size.y + scanHeightOffset * 2f;

        for (int px = 0; px < texW; px++)
        {
            for (int pz = 0; pz < texH; pz++)
            {
                float   wx      = combined.min.x + (px + 0.5f) / texW * combined.size.x;
                float   wz      = combined.min.z + (pz + 0.5f) / texH * combined.size.z;
                Vector3 testPos = new Vector3(wx, combined.center.y, wz);

                if (!room.Contains(testPos))
                {
                    pixels[pz * texW + px] = Color.clear;
                    continue;
                }

                Vector3 origin = new Vector3(wx, combined.max.y + scanHeightOffset, wz);

                Color pixel;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLen,
                        Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    bool isOccluder = (occluderMask.value & (1 << hit.collider.gameObject.layer)) != 0;
                    pixel = isOccluder ? occluderColor : floorColor;
                }
                else
                {
                    pixel = floorColor;
                }

                pixels[pz * texW + px] = pixel;
            }
        }

        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels(pixels);
        tex.Apply();

        ScannedTexture = tex;
        PixelCache     = pixels;
        HasBeenScanned = true;
    }

    private void OnDestroy()
    {
        if (ScannedTexture != null)
            Destroy(ScannedTexture);
    }
}