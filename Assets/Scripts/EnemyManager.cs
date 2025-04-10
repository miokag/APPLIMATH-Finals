using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public Material enemyMaterial;
    public int enemyCount = 5;
    public float enemySize = 1f;
    public float moveSpeed = 2f;
    public float minMoveDistance = 3f;
    public float maxMoveDistance = 8f;
    public float spawnPadding = 2f;
    public int damageToPlayer = 1;
    public float damageCooldown = 1f;

    private Mesh enemyMesh;
    private List<Matrix4x4> enemyMatrices = new List<Matrix4x4>();
    private List<int> enemyColliderIds = new List<int>();
    private List<float> moveDirections = new List<float>();
    private List<float> moveDistances = new List<float>();
    private List<Vector3> startPositions = new List<Vector3>();
    private float lastDamageTime;
    private EnhancedMeshGenerator meshGen; // Cache reference

    void Start()
    {
        if (enemyMaterial != null)
        {
            enemyMaterial.enableInstancing = true;
        }

        meshGen = FindObjectOfType<EnhancedMeshGenerator>(); // Get reference once
        CreateEnemyMesh();
        SpawnEnemies();
        lastDamageTime = -damageCooldown;
    }

    void CreateEnemyMesh()
    {
        enemyMesh = new Mesh();
        
        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(0, 0, 0),
            new Vector3(enemySize, 0, 0),
            new Vector3(enemySize, 0, enemySize),
            new Vector3(0, 0, enemySize),
            new Vector3(0, enemySize, 0),
            new Vector3(enemySize, enemySize, 0),
            new Vector3(enemySize, enemySize, enemySize),
            new Vector3(0, enemySize, enemySize)
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
        
        enemyMesh.vertices = vertices;
        enemyMesh.triangles = triangles;
        enemyMesh.RecalculateNormals();
        enemyMesh.RecalculateBounds();
    }

    void SpawnEnemies()
    {
        if (meshGen == null) return;

        // Get player's starting X position (center is at x=0)
        float playerStartX = 0f;

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 position;
            bool positionValid;
            int attempts = 0;
            const int maxAttempts = 50;

            do
            {
                // Only spawn on right side (positive X) of player
                position = new Vector3(
                    Random.Range(playerStartX + 5f, meshGen.maxX - spawnPadding), // Start 5 units right of player
                    meshGen.groundY + meshGen.height, 
                    meshGen.constantZPosition
                );

                positionValid = true;
                
                for (int j = 0; j < enemyMatrices.Count; j++)
                {
                    Vector3 otherPos = enemyMatrices[j].GetPosition();
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

            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.one * enemySize;

            int id = CollisionManager.Instance.RegisterCollider(
                position,
                new Vector3(enemySize, meshGen.height, enemySize),
                false);

            Matrix4x4 enemyMatrix = Matrix4x4.TRS(position, rotation, scale);
            enemyMatrices.Add(enemyMatrix);
            enemyColliderIds.Add(id);
            moveDirections.Add(Random.value > 0.5f ? 1f : -1f);
            moveDistances.Add(Random.Range(minMoveDistance, maxMoveDistance));
            startPositions.Add(position);

            CollisionManager.Instance.UpdateMatrix(id, enemyMatrix);
        }
    }

    void Update()
    {
        MoveEnemies();
        CheckPlayerCollisions();
        RenderEnemies();
    }

    void MoveEnemies()
    {
        if (meshGen == null) return;

        for (int i = 0; i < enemyMatrices.Count; i++)
        {
            Vector3 currentPos = enemyMatrices[i].GetPosition();
            Vector3 startPos = startPositions[i];
            
            float targetX = startPos.x + (moveDistances[i] * moveDirections[i]);
            float newX = Mathf.MoveTowards(currentPos.x, targetX, moveSpeed * Time.deltaTime);
            
            if (Mathf.Approximately(newX, targetX))
            {
                moveDirections[i] *= -1f;
                startPositions[i] = currentPos;
            }

            Vector3 newPos = new Vector3(
                newX, 
                meshGen.groundY + meshGen.height, // Maintain height above ground
                currentPos.z
            );
            
            enemyMatrices[i] = Matrix4x4.TRS(newPos, enemyMatrices[i].rotation, enemyMatrices[i].lossyScale);
            
            CollisionManager.Instance.UpdateCollider(
                enemyColliderIds[i], 
                newPos, 
                new Vector3(enemySize, meshGen.height, enemySize)
            );
        }
    }

    void CheckPlayerCollisions()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsPlayerInvincible()) return;
        if (meshGen == null || meshGen.GetPlayerID() == -1) return;
        if (Time.time - lastDamageTime < damageCooldown) return;

        var playerMatrix = CollisionManager.Instance.GetMatrix(meshGen.GetPlayerID());
        Vector3 playerPos = playerMatrix.GetPosition();
        Vector3 playerSize = meshGen.GetPlayerSize();
        float playerRadius = Mathf.Max(playerSize.x, playerSize.y, playerSize.z) * 0.5f;

        bool playerHit = false;

        for (int i = 0; i < enemyMatrices.Count; i++)
        {
            Vector3 enemyPos = enemyMatrices[i].GetPosition();
            
            float dx = playerPos.x - enemyPos.x;
            float dy = playerPos.y - enemyPos.y;
            float dz = playerPos.z - enemyPos.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;
            
            float combinedRadius = playerRadius + enemySize;
            if (sqrDistance < combinedRadius * combinedRadius)
            {
                playerHit = true;
                break;
            }
        }

        if (playerHit)
        {
            GameManager.Instance.TakeDamage(damageToPlayer);
            lastDamageTime = Time.time;
            Debug.Log($"Enemy hit player! Health: {GameManager.Instance.CurrentHealth}. Next damage in {damageCooldown} seconds.");
        }
    }

    void RenderEnemies()
    {
        if (enemyMaterial == null || enemyMesh == null || enemyMatrices.Count == 0) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        List<Matrix4x4> visibleEnemies = new List<Matrix4x4>();

        foreach (var matrix in enemyMatrices)
        {
            Vector3 enemyPos = matrix.GetPosition();
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(enemyPos);
            
            bool isVisible = viewportPos.x > -0.5f && viewportPos.x < 1.5f && 
                            viewportPos.y > -0.5f && viewportPos.y < 1.5f &&
                            viewportPos.z > mainCamera.nearClipPlane;

            if (isVisible)
            {
                visibleEnemies.Add(matrix);
            }
            else
            {
                Matrix4x4 zeroScaleMatrix = Matrix4x4.TRS(
                    enemyPos,
                    matrix.rotation,
                    Vector3.zero
                );
                visibleEnemies.Add(zeroScaleMatrix);
            }
        }

        Matrix4x4[] matricesArray = visibleEnemies.ToArray();

        for (int i = 0; i < matricesArray.Length; i += 1023)
        {
            int batchSize = Mathf.Min(1023, matricesArray.Length - i);
            Graphics.DrawMeshInstanced(
                enemyMesh,
                0,
                enemyMaterial,
                matricesArray,
                batchSize,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false
            );
        }
    }

    public void DestroyEnemy(int colliderId)
    {
        int index = enemyColliderIds.IndexOf(colliderId);
        if (index >= 0)
        {
            CollisionManager.Instance.RemoveCollider(colliderId);
            enemyMatrices.RemoveAt(index);
            enemyColliderIds.RemoveAt(index);
            moveDirections.RemoveAt(index);
            moveDistances.RemoveAt(index);
            startPositions.RemoveAt(index);
        }
    }
}