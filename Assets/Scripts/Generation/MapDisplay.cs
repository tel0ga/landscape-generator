using UnityEngine;
using UnityEngine.UIElements;

public class MapDisplay : MonoBehaviour
{
    public Renderer textureRenderer;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    Mesh ownedMesh;
    
    public void DrawTexture(Texture2D texture)
    {
        textureRenderer.sharedMaterial.mainTexture = texture;
        var filter = textureRenderer.GetComponent<MeshFilter>();
        if (filter != null)
            SetMesh(filter, MeshGenerator.GenerateTerrainMesh(new float[texture.width, texture.height], 0, MapGenerator.PixelsPerUnit));
    }
    public void DrawMesh(MeshData meshData, Texture2D texture)
    {
        SetMesh(meshFilter, meshData);
        meshRenderer.sharedMaterial.mainTexture = texture;
    }
    void SetMesh(MeshFilter filter, MeshData data)
    {
        ReleaseMesh();
        ownedMesh = data.CreateMesh(); filter.sharedMesh = ownedMesh;
        filter.transform.localScale = Vector3.one;
        filter.transform.localRotation = Quaternion.identity;
    }
    void ReleaseMesh()
    {
        if (ownedMesh == null) return;
        if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh);
        ownedMesh = null;
    }
    void OnDestroy() { ReleaseMesh(); }
}
