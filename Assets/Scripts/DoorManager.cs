using UnityEngine;

public class DoorManager : MonoBehaviour
{
    public EnhancedMeshGenerator meshGenerator;
    public Material doorMaterial;
    public float doorWidth = 1f;
    public float doorHeight = 2f; // Taller than player (player is height 1)
    public float doorDepth = 0.5f;

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
            // Bottom face
            new Vector3(0, 0, 0),
            new Vector3(doorWidth, 0, 0),
            new Vector3(doorWidth, 0, doorDepth),
            new Vector3(0, 0, doorDepth),
            // Top face
            new Vector3(0, doorHeight, 0),
            new Vector3(doorWidth, doorHeight, 0),
            new Vector3(doorWidth, doorHeight, doorDepth),
            new Vector3(0, doorHeight, doorDepth)
        };
        
        int[] triangles = new int[36]
        {
            0, 4, 1, 1, 4, 5, // Front
            2, 6, 3, 3, 6, 7, // Back
            0, 3, 4, 4, 3, 7, // Left
            1, 5, 2, 2, 5, 6, // Right
            0, 1, 3, 3, 1, 2, // Bottom
            4, 7, 5, 5, 7, 6  // Top
        };

        doorMesh.vertices = vertices;
        doorMesh.triangles = triangles;
        doorMesh.RecalculateNormals();
    }

    void SpawnDoor()
    {
        // Position at end of ground (right side)
        Vector3 doorPosition = new Vector3(
            meshGenerator.maxX - doorWidth/2, // Center of door at edge
            meshGenerator.groundY + doorHeight/2, // Center vertically
            meshGenerator.constantZPosition
        );

        Quaternion rotation = Quaternion.identity;
        Vector3 scale = Vector3.one;

        // Register collider
        doorColliderId = CollisionManager.Instance.RegisterCollider(
            doorPosition,
            new Vector3(doorWidth, doorHeight, doorDepth),
            false);

        doorMatrix = Matrix4x4.TRS(doorPosition, rotation, scale);
        CollisionManager.Instance.UpdateMatrix(doorColliderId, doorMatrix);
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
        
        var doorPos = doorMatrix.GetPosition();
        
        // Simple AABB collision check
        if (Mathf.Abs(playerPos.x - doorPos.x) < (playerSize.x + doorWidth)/2 &&
            Mathf.Abs(playerPos.y - doorPos.y) < (playerSize.y + doorHeight)/2 &&
            Mathf.Abs(playerPos.z - doorPos.z) < (playerSize.z + doorDepth)/2)
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
        if (doorMaterial == null) return;
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 doorPos = doorMatrix.GetPosition();
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(doorPos);
        
        Vector3 cameraToDoor = (doorPos - mainCamera.transform.position).normalized;
        float dot = Vector3.Dot(mainCamera.transform.forward, cameraToDoor);
        
        bool isVisible = dot > 0 && 
                       viewportPos.x > -0.5f && viewportPos.x < 1.5f && 
                       viewportPos.y > -0.5f && viewportPos.y < 1.5f &&
                       viewportPos.z > mainCamera.nearClipPlane;

        Vector3 position = doorMatrix.GetPosition();
        Quaternion rotation = doorMatrix.rotation;
        Vector3 scale = isVisible ? Vector3.one : Vector3.zero;
        
        Matrix4x4 renderMatrix = Matrix4x4.TRS(position, rotation, scale);
        Graphics.DrawMesh(doorMesh, renderMatrix, doorMaterial, 0);
    }
}