using System.Collections.Generic;
using UnityEngine;

public class ObstacleManager : MonoBehaviour
{
    public EnhancedMeshGenerator meshGenerator;
    public Material lethalObstacleMaterial;
    public Material nonLethalObstacleMaterial;
    public int obstacleCount = 10;
    public float obstacleWidth = 0.5f; // Only width needs to change
    public float spawnPadding = 2f;

    private Mesh obstacleMesh;
    private List<Matrix4x4> obstacleMatrices = new List<Matrix4x4>();
    private List<int> obstacleColliderIds = new List<int>();
    private List<bool> obstacleLethality = new List<bool>();
    private List<bool> obstacleHitStatus = new List<bool>();

    void Start()
    {
        if (lethalObstacleMaterial != null) lethalObstacleMaterial.enableInstancing = true;
        if (nonLethalObstacleMaterial != null) nonLethalObstacleMaterial.enableInstancing = true;

        CreateObstacleMesh();
        SpawnObstacles();
    }

    void CreateObstacleMesh()
    {
        // Same cube as EnhancedMeshGenerator but with adjustable width
        obstacleMesh = new Mesh();
        
        float height = 1f; // Same as player
        float depth = 1f;  // Same as player
        
        Vector3[] vertices = new Vector3[8]
        {
            // Bottom face
            new Vector3(0, 0, 0),
            new Vector3(obstacleWidth, 0, 0),
            new Vector3(obstacleWidth, 0, depth),
            new Vector3(0, 0, depth),
            // Top face
            new Vector3(0, height, 0),
            new Vector3(obstacleWidth, height, 0),
            new Vector3(obstacleWidth, height, depth),
            new Vector3(0, height, depth)
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

        obstacleMesh.vertices = vertices;
        obstacleMesh.triangles = triangles;
        obstacleMesh.RecalculateNormals();
    }

    void SpawnObstacles()
    {
        float playerStartX = 0f;
        float rightSideLength = meshGenerator.maxX - playerStartX;
        float sectionLength = rightSideLength / obstacleCount;

        for (int i = 0; i < obstacleCount; i++)
        {
            float sectionStart = playerStartX + (i * sectionLength);
            float sectionEnd = sectionStart + sectionLength;
            
            Vector3 position = new Vector3(
                Random.Range(sectionStart + spawnPadding, sectionEnd - spawnPadding),
                meshGenerator.groundY + 0.5f, // Half of height (1 unit)
                meshGenerator.constantZPosition
            );

            bool isLethal = Random.value > 0.5f;
            Quaternion rotation = Quaternion.identity; // No rotation
            Vector3 scale = Vector3.one;

            // Collider matches visual size exactly
            Vector3 colliderSize = new Vector3(obstacleWidth, 1f, 1f);
            
            int id = CollisionManager.Instance.RegisterCollider(
                position, 
                colliderSize,
                false);

            Matrix4x4 obstacleMatrix = Matrix4x4.TRS(position, rotation, scale);
            obstacleMatrices.Add(obstacleMatrix);
            obstacleColliderIds.Add(id);
            obstacleLethality.Add(isLethal);
            obstacleHitStatus.Add(false);

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
        if (meshGenerator.GetPlayerID() == -1) return;
    
        var playerMatrix = CollisionManager.Instance.GetMatrix(meshGenerator.GetPlayerID());
        Vector3 playerPos = playerMatrix.GetPosition();
        Vector3 playerSize = meshGenerator.GetPlayerSize();
        float playerRadius = Mathf.Max(playerSize.x, playerSize.y, playerSize.z) * 0.5f;
    
        for (int i = 0; i < obstacleColliderIds.Count; i++)
        {
            if (obstacleHitStatus[i]) continue;

            int obstacleId = obstacleColliderIds[i];
            var obstacleMatrix = CollisionManager.Instance.GetMatrix(obstacleId);
            Vector3 obstaclePos = obstacleMatrix.GetPosition();
        
            float dx = playerPos.x - obstaclePos.x;
            float dy = playerPos.y - obstaclePos.y;
            float dz = playerPos.z - obstaclePos.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;
        
            // Changed obstacleSize to obstacleWidth here
            float combinedRadius = playerRadius + obstacleWidth;
            if (sqrDistance < combinedRadius * combinedRadius)
            {
                HandleObstacleEffect(obstacleLethality[i]);
                obstacleHitStatus[i] = true;
            }
        }
    }

    void HandleObstacleEffect(bool isLethal)
    {
        if (GameManager.Instance == null) return;

        if (isLethal & GameManager.Instance.IsPlayerInvincible() == false)
        {
            GameManager.Instance.GameOver("Death By Obstacle");
        }
    }

    void RenderObstacles()
    {
        if (obstacleMatrices.Count == 0) return;
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        List<Matrix4x4> lethalMatrices = new List<Matrix4x4>();
        List<Matrix4x4> nonLethalMatrices = new List<Matrix4x4>();

        for (int i = 0; i < obstacleMatrices.Count; i++)
        {
            Vector3 obstaclePos = obstacleMatrices[i].GetPosition();
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(obstaclePos);
            
            Vector3 cameraToObstacle = (obstaclePos - mainCamera.transform.position).normalized;
            float dot = Vector3.Dot(mainCamera.transform.forward, cameraToObstacle);
            
            bool isVisible = dot > 0 && 
                           viewportPos.x > -0.5f && viewportPos.x < 1.5f && 
                           viewportPos.y > -0.5f && viewportPos.y < 1.5f &&
                           viewportPos.z > mainCamera.nearClipPlane;

            Vector3 position = obstacleMatrices[i].GetPosition();
            Quaternion rotation = obstacleMatrices[i].rotation;
            Vector3 scale = isVisible ? Vector3.one : Vector3.zero;
            
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);

            if (obstacleLethality[i])
            {
                lethalMatrices.Add(matrix);
            }
            else
            {
                nonLethalMatrices.Add(matrix);
            }
        }

        if (lethalObstacleMaterial != null && lethalMatrices.Count > 0)
        {
            RenderObstacleBatch(lethalMatrices, lethalObstacleMaterial);
        }

        if (nonLethalObstacleMaterial != null && nonLethalMatrices.Count > 0)
        {
            RenderObstacleBatch(nonLethalMatrices, nonLethalObstacleMaterial);
            RenderObstacleBatch(nonLethalMatrices, nonLethalObstacleMaterial);
        }
    }

    void RenderObstacleBatch(List<Matrix4x4> matrices, Material material)
    {
        Matrix4x4[] matrixArray = matrices.ToArray();
        
        for (int i = 0; i < matrixArray.Length; i += 1023)
        {
            int batchSize = Mathf.Min(1023, matrixArray.Length - i);
            Graphics.DrawMeshInstanced(
                obstacleMesh, 
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
}