using UnityEngine;
using System.Collections.Generic;

public class DoorManager : MonoBehaviour
{
    public EnhancedMeshGenerator meshGenerator;
    public Material doorMaterial;
    public float doorWidth = 1f;
    public float doorHeight = 2f;
    public float doorDepth = 0.5f;
    public float minClearRadius = 5f; // Minimum clear space around door

    private Mesh doorMesh;
    private Matrix4x4 doorMatrix;
    private int doorColliderId;

    void Start()
    {
        if (doorMaterial != null) doorMaterial.enableInstancing = true;
        
        CreateDoorMesh();
        SpawnDoor();
    }

    void CreateDoorMesh()
    {
        doorMesh = new Mesh();
        
        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(0, 0, 0),
            new Vector3(doorWidth, 0, 0),
            new Vector3(doorWidth, 0, doorDepth),
            new Vector3(0, 0, doorDepth),
            new Vector3(0, doorHeight, 0),
            new Vector3(doorWidth, doorHeight, 0),
            new Vector3(doorWidth, doorHeight, doorDepth),
            new Vector3(0, doorHeight, doorDepth)
        };
        
        int[] triangles = new int[36]
        {
            0, 4, 1, 1, 4, 5,
            2, 6, 3, 3, 6, 7,
            0, 3, 4, 4, 3, 7,
            1, 5, 2, 2, 5, 6,
            0, 1, 3, 3, 1, 2,
            4, 7, 5, 5, 7, 6
        };

        doorMesh.vertices = vertices;
        doorMesh.triangles = triangles;
        doorMesh.RecalculateNormals();
        doorMesh.RecalculateBounds();
    }

    void SpawnDoor()
    {
        Vector3 doorPosition = FindValidDoorPosition();
        
        Quaternion rotation = Quaternion.identity;
        Vector3 scale = Vector3.one;

        doorColliderId = CollisionManager.Instance.RegisterCollider(
            doorPosition,
            new Vector3(doorWidth, doorHeight, doorDepth),
            false);

        doorMatrix = Matrix4x4.TRS(doorPosition, rotation, scale);
        CollisionManager.Instance.UpdateMatrix(doorColliderId, doorMatrix);
    }

    Vector3 FindValidDoorPosition()
    {
        // Try positions from right edge moving left until we find a clear spot
        float xPos = meshGenerator.maxX - doorWidth/2;
        float yPos = meshGenerator.groundY + doorHeight/2;
        
        while (xPos > meshGenerator.minX)
        {
            Vector3 testPos = new Vector3(
                xPos,
                yPos,
                meshGenerator.constantZPosition
            );

            if (IsPositionClear(testPos))
            {
                return testPos;
            }
            
            // Move left by minClearRadius
            xPos -= minClearRadius;
        }

        // If no clear spot found, just use far right
        return new Vector3(
            meshGenerator.maxX - doorWidth/2,
            yPos,
            meshGenerator.constantZPosition
        );
    }

    bool IsPositionClear(Vector3 position)
    {
        // Check against all registered colliders except the player and ground
        foreach (var kvp in CollisionManager.Instance.GetAllColliders())
        {
            if (kvp.Value.IsPlayer) continue; // Skip player
            
            Vector3 otherPos = kvp.Value.Center;
            Vector3 otherSize = kvp.Value.Size;
            
            // Calculate distance between centers
            float dx = position.x - otherPos.x;
            float dy = position.y - otherPos.y;
            float dz = position.z - otherPos.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;

            // Use max dimension of other object as radius
            float otherRadius = Mathf.Max(otherSize.x, otherSize.y, otherSize.z) * 0.5f;
            
            if (sqrDistance < (minClearRadius + otherRadius) * (minClearRadius + otherRadius))
            {
                return false;
            }
        }
        return true;
    }

    void Update()
    {
        CheckPlayerCollision();
        RenderDoor();
    }

    void CheckPlayerCollision()
    {
        if (meshGenerator.GetPlayerID() == -1) return;
        
        var playerMatrix = CollisionManager.Instance.GetMatrix(meshGenerator.GetPlayerID());
        Vector3 playerPos = playerMatrix.GetPosition();
        Vector3 playerSize = meshGenerator.GetPlayerSize();
        float playerRadius = Mathf.Max(playerSize.x, playerSize.y, playerSize.z) * 0.5f;
        
        Vector3 doorPos = doorMatrix.GetPosition();
        float doorRadius = Mathf.Max(doorWidth, doorHeight, doorDepth) * 0.5f;
        
        // Sphere collision check like other systems
        float dx = playerPos.x - doorPos.x;
        float dy = playerPos.y - doorPos.y;
        float dz = playerPos.z - doorPos.z;
        float sqrDistance = dx * dx + dy * dy + dz * dz;
        
        if (sqrDistance < (playerRadius + doorRadius) * (playerRadius + doorRadius))
        {
            WinGame();
        }
    }

    void WinGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.WinGame();
            Debug.Log("Player reached the door! You win!");
        }
        
        // Optional: Disable door after winning
        this.enabled = false;
    }

    void RenderDoor()
    {
        if (doorMaterial == null || doorMesh == null) return;
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 doorPos = doorMatrix.GetPosition();
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(doorPos);
        
        bool isVisible = viewportPos.x > -0.5f && viewportPos.x < 1.5f && 
                        viewportPos.y > -0.5f && viewportPos.y < 1.5f &&
                        viewportPos.z > mainCamera.nearClipPlane;

        Matrix4x4 renderMatrix = Matrix4x4.TRS(
            doorPos,
            doorMatrix.rotation,
            isVisible ? Vector3.one : Vector3.zero
        );
        
        Graphics.DrawMesh(doorMesh, renderMatrix, doorMaterial, 0);
    }
}