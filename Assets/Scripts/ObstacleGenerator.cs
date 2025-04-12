using System.Collections.Generic;
using UnityEngine;

public class ObstacleManager : MonoBehaviour
{
    public EnhancedMeshGenerator meshGenerator;
    public Material harmlessMaterial;
    public Material deadlyMaterial;
    public int obstacleCount = 10;
    public float obstacleSize = 1f;
    public float spawnPadding = 2f;

    [Header("Height Settings")]
    public float minHeight = 1f; 
    public float maxHeight = 5f; 

    [Header("Collision Settings")] 
    public float collisionDetectionPadding = 0.1f;

    private Mesh obstacleMesh;
    private List<Matrix4x4> obstacleMatrices = new List<Matrix4x4>();
    private List<int> obstacleColliderIds = new List<int>();
    private List<bool> isDeadlyList = new List<bool>();

    void Start()
    {
        if (harmlessMaterial != null) harmlessMaterial.enableInstancing = true;
        if (deadlyMaterial != null) deadlyMaterial.enableInstancing = true;
        meshGenerator = FindObjectOfType<EnhancedMeshGenerator>();

        CreateObstacleMesh();
        SpawnObstacles();
    }

    void CreateObstacleMesh()
    {
        obstacleMesh = new Mesh();
        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(0, 0, 0),
            new Vector3(obstacleSize, 0, 0),
            new Vector3(obstacleSize, 0, obstacleSize),
            new Vector3(0, 0, obstacleSize),
            new Vector3(0, obstacleSize, 0),
            new Vector3(obstacleSize, obstacleSize, 0),
            new Vector3(obstacleSize, obstacleSize, obstacleSize),
            new Vector3(0, obstacleSize, obstacleSize)
        };

        int[] triangles = new int[36]
        {
            // Bottom face
            0, 1, 2,
            0, 2, 3,
        
            // Top face
            4, 6, 5,
            4, 7, 6,
        
            // Front face
            0, 5, 1,
            0, 4, 5,
        
            // Right face
            1, 5, 6,
            1, 6, 2,
        
            // Back face
            2, 6, 7,
            2, 7, 3,
        
            // Left face
            3, 7, 4,
            3, 4, 0
        };

        obstacleMesh.vertices = vertices;
        obstacleMesh.triangles = triangles;
        obstacleMesh.RecalculateNormals();
        obstacleMesh.RecalculateBounds();
    }

    void SpawnObstacles()
    {
        float zPos = meshGenerator.constantZPosition;
        float playerStartX = 0f;
        float rightSideLength = meshGenerator.maxX - playerStartX;
        float sectionLength = rightSideLength / obstacleCount;

        for (int i = 0; i < obstacleCount; i++)
        {
            float sectionStart = playerStartX + (i * sectionLength);
            float sectionEnd = sectionStart + sectionLength;

            // Random height between min and max
            float randomHeight = Random.Range(minHeight, maxHeight);
            
            Vector3 position = new Vector3(
                Random.Range(sectionStart + spawnPadding, sectionEnd - spawnPadding),
                meshGenerator.groundY + randomHeight, // Use random height
                zPos
            );

            bool isDeadly = Random.value < 0.3f;
            isDeadlyList.Add(isDeadly);

            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.one;

            int id = CollisionManager.Instance.RegisterCollider(
                position,
                new Vector3(obstacleSize, obstacleSize, obstacleSize),
                false);

            Matrix4x4 obstacleMatrix = Matrix4x4.TRS(position, rotation, scale);
            obstacleMatrices.Add(obstacleMatrix);
            obstacleColliderIds.Add(id);

            CollisionManager.Instance.UpdateMatrix(id, obstacleMatrix);
        }
    }

    void Update()
    {
        CheckPlayerCollision();
        RenderObstacles();
    }

    void CheckPlayerCollision()
    {
        int playerId = meshGenerator.GetPlayerID();
        if (playerId == -1) return;

        AABBBounds playerBounds = GetColliderBounds(playerId);
        if (playerBounds == null) return;

        Vector3 expandedPlayerSize = playerBounds.Size + new Vector3(collisionDetectionPadding, collisionDetectionPadding, 0);
        AABBBounds expandedPlayerBounds = new AABBBounds(playerBounds.Center, expandedPlayerSize, -1);

        for (int i = 0; i < obstacleMatrices.Count; i++)
        {
            Vector3 position = obstacleMatrices[i].GetColumn(3);
            Vector3 scale = new Vector3(
                obstacleMatrices[i].GetColumn(0).magnitude,
                obstacleMatrices[i].GetColumn(1).magnitude,
                obstacleMatrices[i].GetColumn(2).magnitude);

            Vector3 obstacleSize = new Vector3(
                this.obstacleSize * scale.x + collisionDetectionPadding,
                this.obstacleSize * scale.y + collisionDetectionPadding,
                this.obstacleSize * scale.z);

            AABBBounds obstacleBounds = new AABBBounds(position, obstacleSize, -1);

            if (BoundsIntersect(expandedPlayerBounds, obstacleBounds))
            {
                if (isDeadlyList[i])
                {
                    // Deadly obstacle - instant kill
                    GameManager.Instance?.TakeDamage(GameManager.Instance.CurrentHealth);
                }
                // Harmless obstacles do nothing
            }
        }
    }

    void RenderObstacles()
    {
        if (obstacleMatrices.Count == 0) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Separate matrices by type
        List<Matrix4x4> harmlessMatrices = new List<Matrix4x4>();
        List<Matrix4x4> deadlyMatrices = new List<Matrix4x4>();

        for (int i = 0; i < obstacleMatrices.Count; i++)
        {
            Vector3 obstaclePos = obstacleMatrices[i].GetColumn(3);
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(obstaclePos);

            bool isVisible = viewportPos.x > -0.5f && viewportPos.x < 1.5f &&
                           viewportPos.y > -0.5f && viewportPos.y < 1.5f &&
                           viewportPos.z > mainCamera.nearClipPlane;

            Matrix4x4 matrix = isVisible ? obstacleMatrices[i] :
                Matrix4x4.TRS(obstaclePos, obstacleMatrices[i].rotation, Vector3.zero);

            if (isDeadlyList[i])
            {
                deadlyMatrices.Add(matrix);
            }
            else
            {
                harmlessMatrices.Add(matrix);
            }
        }

        // Render harmless obstacles
        if (harmlessMaterial != null && harmlessMatrices.Count > 0)
        {
            RenderObstacleBatch(obstacleMesh, harmlessMaterial, harmlessMatrices);
        }

        // Render deadly obstacles
        if (deadlyMaterial != null && deadlyMatrices.Count > 0)
        {
            RenderObstacleBatch(obstacleMesh, deadlyMaterial, deadlyMatrices);
        }
    }

    void RenderObstacleBatch(Mesh mesh, Material material, List<Matrix4x4> matrices)
    {
        Matrix4x4[] matrixArray = matrices.ToArray();

        for (int i = 0; i < matrixArray.Length; i += 1023)
        {
            int batchSize = Mathf.Min(1023, matrixArray.Length - i);
            Graphics.DrawMeshInstanced(
                mesh,
                0,
                material,
                matrixArray,
                batchSize,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false
            );
        }
    }

    AABBBounds GetColliderBounds(int id)
    {
        if (CollisionManager.Instance == null) return null;

        Matrix4x4 matrix = CollisionManager.Instance.GetMatrix(id);
        if (matrix == Matrix4x4.identity) return null;

        Vector3 position = matrix.GetColumn(3);
        Vector3 scale = new Vector3(
            matrix.GetColumn(0).magnitude,
            matrix.GetColumn(1).magnitude,
            matrix.GetColumn(2).magnitude);

        Vector3 size = new Vector3(obstacleSize * scale.x, obstacleSize * scale.y, obstacleSize * scale.z);

        return new AABBBounds(position, size, id);
    }

    bool BoundsIntersect(AABBBounds a, AABBBounds b)
    {
        float aMinX = a.Center.x - (a.Size.x * 0.5f);
        float aMaxX = a.Center.x + (a.Size.x * 0.5f);
        float aMinY = a.Center.y - (a.Size.y * 0.5f);
        float aMaxY = a.Center.y + (a.Size.y * 0.5f);
        float aMinZ = a.Center.z - (a.Size.z * 0.5f);
        float aMaxZ = a.Center.z + (a.Size.z * 0.5f);

        float bMinX = b.Center.x - (b.Size.x * 0.5f);
        float bMaxX = b.Center.x + (b.Size.x * 0.5f);
        float bMinY = b.Center.y - (b.Size.y * 0.5f);
        float bMaxY = b.Center.y + (b.Size.y * 0.5f);
        float bMinZ = b.Center.z - (b.Size.z * 0.5f);
        float bMaxZ = b.Center.z + (b.Size.z * 0.5f);

        return (aMaxX >= bMinX && aMinX <= bMaxX) &&
               (aMaxY >= bMinY && aMinY <= bMaxY) &&
               (aMaxZ >= bMinZ && aMinZ <= bMaxZ);
    }
    
    public bool IsPositionClear(Vector3 position, float radius)
    {
        foreach (var matrix in obstacleMatrices)
        {
            Vector3 obstaclePos = matrix.GetColumn(3);
            float dx = position.x - obstaclePos.x;
            float dy = position.y - obstaclePos.y;
            float dz = position.z - obstaclePos.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;
        
            if (sqrDistance < (obstacleSize + radius) * (obstacleSize + radius))
            {
                return false;
            }
        }
        return true;
    }
}