using UnityEngine;

public static class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(float[,] heightMap, int levelOfDetail)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        float topLeftX = (width - 1) / -2f;
        float topLeftY = (height - 1) / 2f;

        // Всегда 4 вершины и 2 треугольника
        MeshData meshData = new MeshData(2, 2);

        // Вершины: TL, TR, BL, BR
        meshData.vertices[0] = new Vector3(topLeftX, topLeftY, 0); // top-left
        meshData.vertices[1] = new Vector3(topLeftX + width - 1, topLeftY, 0); // top-right
        meshData.vertices[2] = new Vector3(topLeftX, topLeftY - (height - 1), 0); // bottom-left
        meshData.vertices[3] = new Vector3(topLeftX + width - 1, topLeftY - (height - 1), 0); // bottom-right

        // UV
        meshData.uvs[0] = new Vector2(0, 1);
        meshData.uvs[1] = new Vector2(1, 1);
        meshData.uvs[2] = new Vector2(0, 0);
        meshData.uvs[3] = new Vector2(1, 0);

        // Треугольники (по часовой стрелке, если смотреть на -Z)
        meshData.AddTriangle(0, 1, 2);
        meshData.AddTriangle(1, 3, 2);

        return meshData;
    }
}

public class MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    int triangleIndex;
    public MeshData(int meshWidth, int meshHeight)
    {
        vertices = new Vector3[meshWidth * meshHeight];
        uvs = new Vector2[meshWidth * meshHeight];
        triangles = new int[(meshWidth-1)*(meshHeight-1) * 6];
        
    }

    public void AddTriangle(int a, int b, int c)
    {
        triangles[triangleIndex] = a;
        triangles[triangleIndex + 1] = b;
        triangles[triangleIndex + 2] = c;
        triangleIndex += 3;
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        return mesh;
    }
}