using UnityEngine;

public class DoorManager : MonoBehaviour
{
    public EnhancedMeshGenerator meshGenerator;
    public Material doorMaterial;
    public float doorSize = 1f; // Single size parameter like EnemyManager
    public float spawnDistanceFromLastObject = 10f;

    private Mesh doorMesh;
    private Matrix4x4 doorMatrix;
    private int doorColliderId = -1;
    private bool doorSpawned = false;

    [Header("Collision Settings")]
    public float collisionDetectionPadding = 0.1f;

    void Start()
    {
        if (doorMaterial != null) doorMaterial.enableInstancing = true;
        meshGenerator = FindObjectOfType<EnhancedMeshGenerator>();
        CreateDoorMesh();
        SpawnDoorAtEnd();
    }

    void CreateDoorMesh()
    {
        doorMesh = new Mesh();
        
        Vector3[] vertices = new Vector3[8]
        {
            // Bottom face
            new Vector3(0, 0, 0),
            new Vector3(doorSize, 0, 0),
            new Vector3(doorSize, 0, doorSize),
            new Vector3(0, 0, doorSize),
            
            // Top face
            new Vector3(0, doorSize, 0),
            new Vector3(doorSize, doorSize, 0),
            new Vector3(doorSize, doorSize, doorSize),
            new Vector3(0, doorSize, doorSize)
        };

        int[] triangles = new int[36]
        {
            // Front face
            0, 4, 1,
            1, 4, 5,
            
            // Back face
            2, 6, 3,
            3, 6, 7,
            
            // Left face
            0, 3, 4,
            4, 3, 7,
            
            // Right face
            1, 5, 2,
            2, 5, 6,
            
            // Bottom face
            0, 1, 3,
            3, 1, 2,
            
            // Top face
            4, 7, 5,
            5, 7, 6
        };

        doorMesh.vertices = vertices;
        doorMesh.triangles = triangles;
        doorMesh.RecalculateNormals();
        doorMesh.RecalculateBounds();
    }

    void SpawnDoorAtEnd()
    {
        if (doorSpawned) return;

        float farthestX = FindFarthestObjectPosition();
        
        Vector3 doorPosition = new Vector3(
            farthestX + spawnDistanceFromLastObject,
            meshGenerator.groundY + doorSize * 0.5f,
            meshGenerator.constantZPosition
        );

        SpawnDoor(doorPosition);
    }

    float FindFarthestObjectPosition()
    {
        float farthestX = 0f;

        int playerId = meshGenerator.GetPlayerID();
        if (playerId != -1)
        {
            var playerMatrix = CollisionManager.Instance.GetMatrix(playerId);
            farthestX = playerMatrix.GetColumn(3).x;
        }

        EnemyManager enemyManager = FindObjectOfType<EnemyManager>();
        if (enemyManager != null)
        {
            foreach (var matrix in enemyManager.enemyMatrices)
            {
                float xPos = matrix.GetColumn(3).x;
                if (xPos > farthestX) farthestX = xPos;
            }
        }

        PowerUpManager powerUpManager = FindObjectOfType<PowerUpManager>();
        if (powerUpManager != null)
        {
            foreach (var matrix in powerUpManager.powerUpMatrices)
            {
                float xPos = matrix.GetColumn(3).x;
                if (xPos > farthestX) farthestX = xPos;
            }
        }

        ObstacleManager obstacleManager = FindObjectOfType<ObstacleManager>();
        if (obstacleManager != null)
        {
            foreach (var matrix in obstacleManager.obstacleMatrices)
            {
                float xPos = matrix.GetColumn(3).x;
                if (xPos > farthestX) farthestX = xPos;
            }
        }

        return farthestX;
    }

    void SpawnDoor(Vector3 position)
    {
        Quaternion rotation = Quaternion.identity;
        Vector3 scale = Vector3.one;

        doorColliderId = CollisionManager.Instance.RegisterCollider(
            position,
            new Vector3(doorSize, doorSize, doorSize),
            false);

        doorMatrix = Matrix4x4.TRS(position, rotation, scale);
        doorSpawned = true;

        CollisionManager.Instance.UpdateMatrix(doorColliderId, doorMatrix);
        Debug.Log("Door spawned at: " + position);
    }

    void Update()
    {
        if (!doorSpawned) return;

        CheckPlayerCollision();
        RenderDoor();
    }

    void CheckPlayerCollision()
    {
        int playerId = meshGenerator.GetPlayerID();
        if (playerId == -1) return;

        AABBBounds playerBounds = GetColliderBounds(playerId);
        if (playerBounds == null) return;

        Vector3 position = doorMatrix.GetColumn(3);
        Vector3 scale = new Vector3(
            doorMatrix.GetColumn(0).magnitude,
            doorMatrix.GetColumn(1).magnitude,
            doorMatrix.GetColumn(2).magnitude);

        // Create expanded bounds for more reliable collision
        Vector3 doorSizeWithPadding = new Vector3(
            doorSize * scale.x + collisionDetectionPadding,
            doorSize * scale.y + collisionDetectionPadding,
            doorSize * scale.z);
            
        AABBBounds doorBounds = new AABBBounds(position, doorSizeWithPadding, -1);

        if (BoundsIntersect(playerBounds, doorBounds))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.WinGame();
                Debug.Log("Player reached the door!");
            }
        }
    }

    void RenderDoor()
    {
        if (doorMaterial == null || doorMesh == null) return;

        Graphics.DrawMesh(doorMesh, doorMatrix, doorMaterial, 0);
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
        
        Vector3 size = meshGenerator.GetPlayerSize();
        
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
}