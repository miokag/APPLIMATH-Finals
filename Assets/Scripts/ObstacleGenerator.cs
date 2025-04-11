using System.Collections.Generic;
using UnityEngine;

public class ObstacleManager : MonoBehaviour
{
    public EnhancedMeshGenerator meshGenerator;
    public Material lethalObstacleMaterial;
    public Material nonLethalObstacleMaterial;
    public int obstacleCount = 10;
    public float obstacleHeight = 1f;
    public float obstacleSize = 0.5f;
    public float spawnPadding = 2f;

    private Mesh obstacleMesh;
    private List<Matrix4x4> obstacleMatrices = new List<Matrix4x4>();
    private List<int> obstacleColliderIds = new List<int>();
    private List<bool> obstacleLethality = new List<bool>();
    private List<bool> obstacleHitStatus = new List<bool>(); // Track if obstacle has been hit

    void Start()
    {
        if (lethalObstacleMaterial != null) lethalObstacleMaterial.enableInstancing = true;
        if (nonLethalObstacleMaterial != null) nonLethalObstacleMaterial.enableInstancing = true;

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
            new Vector3(0, obstacleSize * 2, 0),
            new Vector3(obstacleSize, obstacleSize * 2, 0),
            new Vector3(obstacleSize, obstacleSize * 2, obstacleSize),
            new Vector3(0, obstacleSize * 2, obstacleSize)
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
        
        Vector2[] uvs = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            uvs[i] = new Vector2(vertices[i].x / obstacleSize, vertices[i].z / obstacleSize);
        }

        obstacleMesh.vertices = vertices;
        obstacleMesh.triangles = triangles;
        obstacleMesh.uv = uvs;
        obstacleMesh.RecalculateNormals();
        obstacleMesh.RecalculateBounds();
    }

    void SpawnObstacles()
    {
        float playerStartX = 0f;

        for (int i = 0; i < obstacleCount; i++)
        {
            Vector3 position;
            bool positionValid;
            int attempts = 0;
            const int maxAttempts = 50;

            do
            {
                position = new Vector3(
                    Random.Range(playerStartX + 5f, meshGenerator.maxX - spawnPadding),
                    meshGenerator.groundY + obstacleHeight,
                    meshGenerator.constantZPosition
                );

                positionValid = true;
                
                for (int j = 0; j < obstacleMatrices.Count; j++)
                {
                    Vector3 otherPos = obstacleMatrices[j].GetPosition();
                    if (Vector3.Distance(position, otherPos) < spawnPadding * 2f)
                    {
                        positionValid = false;
                        break;
                    }
                }

                attempts++;
                if (attempts >= maxAttempts) break;

            } while (!positionValid && attempts < maxAttempts);

            if (!positionValid) continue;

            bool isLethal = Random.value > 0.5f;
            Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            Vector3 scale = Vector3.one;

            int id = CollisionManager.Instance.RegisterCollider(
                position, 
                new Vector3(obstacleSize, obstacleSize * 2, obstacleSize),
                false);

            Matrix4x4 obstacleMatrix = Matrix4x4.TRS(position, rotation, scale);
            obstacleMatrices.Add(obstacleMatrix);
            obstacleColliderIds.Add(id);
            obstacleLethality.Add(isLethal);
            obstacleHitStatus.Add(false); // Initialize as not hit

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
            if (obstacleHitStatus[i]) continue; // Skip already hit obstacles

            int obstacleId = obstacleColliderIds[i];
            var obstacleMatrix = CollisionManager.Instance.GetMatrix(obstacleId);
            Vector3 obstaclePos = obstacleMatrix.GetPosition();
            
            float dx = playerPos.x - obstaclePos.x;
            float dy = playerPos.y - obstaclePos.y;
            float dz = playerPos.z - obstaclePos.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;
            
            float combinedRadius = playerRadius + obstacleSize;
            if (sqrDistance < combinedRadius * combinedRadius)
            {
                HandleObstacleEffect(obstacleLethality[i]);
                obstacleHitStatus[i] = true; // Mark as hit but don't remove
            }
        }
    }

    void HandleObstacleEffect(bool isLethal)
    {
        if (GameManager.Instance == null) return;

        if (isLethal)
        {
            Debug.Log("Hit lethal obstacle! Player dies.");
            GameManager.Instance.TakeDamage(int.MaxValue);
        }
        else
        {
            Debug.Log("Hit non-lethal obstacle. No effect.");
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
            
            // Calculate dot product between camera forward and obstacle direction
            Vector3 cameraToObstacle = (obstaclePos - mainCamera.transform.position).normalized;
            float dot = Vector3.Dot(mainCamera.transform.forward, cameraToObstacle);
            
            // Only render if in front of camera and within viewport bounds
            bool isVisible = dot > 0 && 
                           viewportPos.x > -0.5f && viewportPos.x < 1.5f && 
                           viewportPos.y > -0.5f && viewportPos.y < 1.5f &&
                           viewportPos.z > mainCamera.nearClipPlane;

            // Get original rotation and position
            Vector3 position = obstacleMatrices[i].GetPosition();
            Quaternion rotation = obstacleMatrices[i].rotation;
            
            // Scale to zero if not visible, otherwise use normal scale
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

    public void AddObstacle(bool isLethal, Vector3 position)
    {
        Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        Vector3 scale = Vector3.one;

        int id = CollisionManager.Instance.RegisterCollider(
            position, 
            new Vector3(obstacleSize, obstacleSize * 2, obstacleSize), 
            false);

        Matrix4x4 obstacleMatrix = Matrix4x4.TRS(position, rotation, scale);
        obstacleMatrices.Add(obstacleMatrix);
        obstacleColliderIds.Add(id);
        obstacleLethality.Add(isLethal);
        obstacleHitStatus.Add(false);

        CollisionManager.Instance.UpdateMatrix(id, obstacleMatrix);
    }
}