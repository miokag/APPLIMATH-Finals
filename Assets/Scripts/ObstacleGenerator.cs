using System.Collections.Generic;
using UnityEngine;

public class ObstacleManager : MonoBehaviour
{
    public Material nonLethalMaterial;
    public Material lethalMaterial;
    public int obstacleCount = 10;
    public float obstacleSize = 1f;
    public float spawnPadding = 2f;
    [Range(0f, 1f)] public float lethalProbability = 0.3f;

    private Mesh obstacleMesh;
    private List<Matrix4x4> obstacleMatrices = new List<Matrix4x4>();
    private List<int> obstacleColliderIds = new List<int>();
    private List<bool> isLethalList = new List<bool>();
    private EnhancedMeshGenerator meshGen;

    void Start()
    {
        if (nonLethalMaterial != null) nonLethalMaterial.enableInstancing = true;
        if (lethalMaterial != null) lethalMaterial.enableInstancing = true;

        meshGen = FindObjectOfType<EnhancedMeshGenerator>();
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
            0, 4, 1, 1, 4, 5,
            2, 6, 3, 3, 6, 7,
            0, 3, 4, 4, 3, 7,
            1, 5, 2, 2, 5, 6,
            0, 1, 3, 3, 1, 2,
            4, 7, 5, 5, 7, 6
        };
        
        obstacleMesh.vertices = vertices;
        obstacleMesh.triangles = triangles;
        obstacleMesh.RecalculateNormals();
        obstacleMesh.RecalculateBounds();
    }

    void SpawnObstacles()
    {
        if (meshGen == null) return;

        float playerStartX = 0f;
        float rightSideLength = meshGen.maxX - playerStartX;
        float sectionLength = rightSideLength / obstacleCount;
    
        for (int i = 0; i < obstacleCount; i++)
        {
            float sectionStart = playerStartX + (i * sectionLength);
            float sectionEnd = sectionStart + sectionLength;
        
            Vector3 position = new Vector3(
                Random.Range(sectionStart + spawnPadding, sectionEnd - spawnPadding),
                meshGen.groundY + obstacleSize * 0.5f, // Half height to sit on ground
                meshGen.constantZPosition
            );

            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.one * obstacleSize;

            // Determine if this is a lethal obstacle
            bool isLethal = Random.value <= lethalProbability;
            isLethalList.Add(isLethal);

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
        CheckPlayerCollisions();
        RenderObstacles();
    }

    void CheckPlayerCollisions()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsPlayerInvincible()) return;
        if (meshGen == null || meshGen.GetPlayerID() == -1) return;

        var playerMatrix = CollisionManager.Instance.GetMatrix(meshGen.GetPlayerID());
        Vector3 playerPos = playerMatrix.GetPosition();
        Vector3 playerSize = meshGen.GetPlayerSize();
        float playerRadius = Mathf.Max(playerSize.x, playerSize.y, playerSize.z) * 0.5f;

        for (int i = 0; i < obstacleMatrices.Count; i++)
        {
            Vector3 obstaclePos = obstacleMatrices[i].GetPosition();
            
            float dx = playerPos.x - obstaclePos.x;
            float dy = playerPos.y - obstaclePos.y;
            float dz = playerPos.z - obstaclePos.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;
            
            float combinedRadius = playerRadius + obstacleSize;
            if (sqrDistance < combinedRadius * combinedRadius)
            {
                Debug.Log("Collision detected with obstacle");
                if (isLethalList[i])
                {
                    Debug.Log("Lethal obstacle hit!");
                    GameManager.Instance.TakeDamage(GameManager.Instance.CurrentHealth); // Instant death
                }
                else
                {
                    Debug.Log("Non-lethal obstacle hit");
                    // Collision response is handled by EnhancedMeshGenerator's movement code
                }
            }
        }
    }

    void RenderObstacles()
    {
        if (obstacleMatrices.Count == 0) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Separate lists for lethal and non-lethal obstacles
        List<Matrix4x4> visibleNonLethal = new List<Matrix4x4>();
        List<Matrix4x4> visibleLethal = new List<Matrix4x4>();

        for (int i = 0; i < obstacleMatrices.Count; i++)
        {
            Vector3 obstaclePos = obstacleMatrices[i].GetPosition();
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(obstaclePos);
            
            bool isVisible = viewportPos.x > -0.5f && viewportPos.x < 1.5f && 
                            viewportPos.y > -0.5f && viewportPos.y < 1.5f &&
                            viewportPos.z > mainCamera.nearClipPlane;

            Matrix4x4 matrixToRender;
            if (isVisible)
            {
                matrixToRender = obstacleMatrices[i];
            }
            else
            {
                matrixToRender = Matrix4x4.TRS(
                    obstaclePos,
                    obstacleMatrices[i].rotation,
                    Vector3.zero
                );
            }

            if (isLethalList[i])
            {
                visibleLethal.Add(matrixToRender);
            }
            else
            {
                visibleNonLethal.Add(matrixToRender);
            }
        }

        // Render non-lethal obstacles
        if (nonLethalMaterial != null && visibleNonLethal.Count > 0)
        {
            Matrix4x4[] nonLethalArray = visibleNonLethal.ToArray();
            for (int i = 0; i < nonLethalArray.Length; i += 1023)
            {
                int batchSize = Mathf.Min(1023, nonLethalArray.Length - i);
                Graphics.DrawMeshInstanced(
                    obstacleMesh,
                    0,
                    nonLethalMaterial,
                    nonLethalArray,
                    batchSize,
                    null,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false
                );
            }
        }

        // Render lethal obstacles
        if (lethalMaterial != null && visibleLethal.Count > 0)
        {
            Matrix4x4[] lethalArray = visibleLethal.ToArray();
            for (int i = 0; i < lethalArray.Length; i += 1023)
            {
                int batchSize = Mathf.Min(1023, lethalArray.Length - i);
                Graphics.DrawMeshInstanced(
                    obstacleMesh,
                    0,
                    lethalMaterial,
                    lethalArray,
                    batchSize,
                    null,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false
                );
            }
        }
    }
}