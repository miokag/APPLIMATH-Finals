using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }
    
    [Header("Enemy Settings")]
    public int enemyCount = 10;
    public Material enemyMaterial;
    public float enemyWidth = 1f;
    public float enemyHeight = 1f;
    public float enemyDepth = 1f;
    public float movementSpeed = 2f;
    public float groundY = -20f;
    public float patrolDistance = 5f; 
    public float spawnHeight = 1f;

    private Mesh enemyMesh;
    private List<Matrix4x4> enemyMatrices = new List<Matrix4x4>();
    private List<int> enemyColliderIds = new List<int>();
    private List<float> enemyMovementDirections = new List<float>();
    private List<float> enemySpawnPositionsX = new List<float>();
    private EnhancedMeshGenerator meshGenerator;
    
    [Header("Collision Settings")]
    public float collisionBumpForce = 1f;
    public float collisionDetectionPadding = 0.1f;
    
    [Header("Damage Settings")]
    public int damageAmount = 1;
    public float damageCooldown = 1f;
    private float lastDamageTime;
    private bool canDamage = true;
    
    [Header("Spawn Settings")]
    public float minSpawnDistanceFromPlayer = 10f; // Minimum distance from player to spawn
    public float enemySpacing = 3f; // Space between enemies
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        meshGenerator = FindObjectOfType<EnhancedMeshGenerator>();
        if (meshGenerator == null)
        {
            Debug.LogError("EnhancedMeshGenerator not found in scene!");
            return;
        }

        CreateEnemyMesh();
        SpawnEnemies();
    }

    void CreateEnemyMesh()
    {
        enemyMesh = new Mesh();
        
        Vector3[] vertices = new Vector3[8];
        
        // Bottom face
        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(enemyWidth, 0, 0);
        vertices[2] = new Vector3(enemyWidth, 0, enemyDepth);
        vertices[3] = new Vector3(0, 0, enemyDepth);
        
        // Top face
        vertices[4] = new Vector3(0, enemyHeight, 0);
        vertices[5] = new Vector3(enemyWidth, enemyHeight, 0);
        vertices[6] = new Vector3(enemyWidth, enemyHeight, enemyDepth);
        vertices[7] = new Vector3(0, enemyHeight, enemyDepth);

        int[] triangles = new int[36]
        {
            0, 4, 1, 1, 4, 5,    // Front
            2, 6, 3, 3, 6, 7,    // Back
            0, 3, 4, 4, 3, 7,    // Left
            1, 5, 2, 2, 5, 6,    // Right
            0, 1, 3, 3, 1, 2,    // Bottom
            4, 7, 5, 5, 7, 6     // Top
        };

        enemyMesh.vertices = vertices;
        enemyMesh.triangles = triangles;
        enemyMesh.RecalculateNormals();
    }

    void SpawnEnemies()
    {
        float zPos = meshGenerator.constantZPosition;
        Camera mainCamera = Camera.main;
    
        // Get player position
        int playerId = meshGenerator.GetPlayerID();
        Vector3 playerPos = CollisionManager.Instance.GetMatrix(playerId).GetColumn(3);
    
        for (int i = 0; i < enemyCount; i++)
        {
            // Calculate spawn position to the right of player
            float xPos = playerPos.x + minSpawnDistanceFromPlayer + (i * enemySpacing);
        
            // Position just above ground
            float yPos = groundY + spawnHeight + (enemyHeight * 0.5f);
        
            Vector3 position = new Vector3(xPos, yPos, zPos);
            Quaternion rotation = Quaternion.identity;
        
            // Check if position is outside camera view
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(position);
            bool isVisible = viewportPos.x >= 0 && viewportPos.x <= 1 && 
                             viewportPos.y >= 0 && viewportPos.y <= 1 &&
                             viewportPos.z > 0;
        
            // Set scale to zero if outside camera view
            Vector3 scale = isVisible ? Vector3.one : Vector3.zero;
        
            // Register with collision system (only if visible)
            int id = CollisionManager.Instance.RegisterCollider(
                position, 
                isVisible ? new Vector3(enemyWidth * scale.x, enemyHeight * scale.y, enemyDepth * scale.z) : Vector3.zero,
                false);
        
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
            enemyMatrices.Add(matrix);
            enemyColliderIds.Add(id);
            enemyMovementDirections.Add(Random.value > 0.5f ? 1f : -1f);
            enemySpawnPositionsX.Add(xPos);
        
            CollisionManager.Instance.UpdateMatrix(id, matrix);
        }
    }

    void Update()
    {
        if (meshGenerator == null) return;

        UpdateEnemyVisibility();
        MoveEnemies();
        CheckPlayerCollision();
        RenderEnemies();

        // Handle damage cooldown
        if (!canDamage && Time.time - lastDamageTime >= damageCooldown)
        {
            canDamage = true;
        }
    }
    
    void UpdateEnemyVisibility()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        for (int i = 0; i < enemyMatrices.Count; i++)
        {
            Vector3 position = enemyMatrices[i].GetColumn(3);
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(position);
        
            bool isVisible = viewportPos.x >= -0.5f && viewportPos.x <= 1.5f && 
                             viewportPos.y >= -0.5f && viewportPos.y <= 1.5f &&
                             viewportPos.z > 0;
        
            // Get current rotation and scale
            Quaternion rotation = Quaternion.LookRotation(
                enemyMatrices[i].GetColumn(2),
                enemyMatrices[i].GetColumn(1));
            Vector3 currentScale = new Vector3(
                enemyMatrices[i].GetColumn(0).magnitude,
                enemyMatrices[i].GetColumn(1).magnitude,
                enemyMatrices[i].GetColumn(2).magnitude);
        
            // Only update if visibility state changed
            if ((currentScale == Vector3.zero && isVisible) || 
                (currentScale != Vector3.zero && !isVisible))
            {
                Vector3 newScale = isVisible ? Vector3.one : Vector3.zero;
                Matrix4x4 newMatrix = Matrix4x4.TRS(position, rotation, newScale);
                enemyMatrices[i] = newMatrix;
            
                // Update collider only when becoming visible
                if (isVisible)
                {
                    CollisionManager.Instance.UpdateCollider(
                        enemyColliderIds[i], 
                        position, 
                        new Vector3(enemyWidth, enemyHeight, enemyDepth));
                }
                CollisionManager.Instance.UpdateMatrix(enemyColliderIds[i], newMatrix);
            }
        }
    }
    
    void MoveEnemies()
    {
        float zPos = meshGenerator.constantZPosition;
        
        for (int i = 0; i < enemyMatrices.Count; i++)
        {
            // Decompose current matrix
            Vector3 position = enemyMatrices[i].GetColumn(3);
            Quaternion rotation = Quaternion.LookRotation(
                enemyMatrices[i].GetColumn(2),
                enemyMatrices[i].GetColumn(1));
            Vector3 scale = new Vector3(
                enemyMatrices[i].GetColumn(0).magnitude,
                enemyMatrices[i].GetColumn(1).magnitude,
                enemyMatrices[i].GetColumn(2).magnitude);
            
            // Calculate desired movement
            float moveAmount = enemyMovementDirections[i] * movementSpeed * Time.deltaTime;
            float newX = position.x + moveAmount;
            
            // Get patrol boundaries
            float spawnX = enemySpawnPositionsX[i];
            float minPatrolX = spawnX - patrolDistance;
            float maxPatrolX = spawnX + patrolDistance;
            
            // Check for patrol boundary collision
            if (newX < minPatrolX || newX > maxPatrolX)
            {
                enemyMovementDirections[i] *= -1f;
                newX = Mathf.Clamp(newX, minPatrolX, maxPatrolX);
            }
            
            // Update position
            position.x = newX;
            
            // Update matrix and collider
            Matrix4x4 newMatrix = Matrix4x4.TRS(position, rotation, scale);
            enemyMatrices[i] = newMatrix;
            
            Vector3 enemySize = new Vector3(enemyWidth * scale.x, enemyHeight * scale.y, enemyDepth * scale.z);
            CollisionManager.Instance.UpdateCollider(enemyColliderIds[i], position, enemySize);
            CollisionManager.Instance.UpdateMatrix(enemyColliderIds[i], newMatrix);
        }
    }

    void CheckPlayerCollision()
{
    int playerId = meshGenerator.GetPlayerID();
    if (playerId == -1) return;
    
    AABBBounds playerBounds = GetColliderBounds(playerId);
    if (playerBounds == null) return;
    
    // Create expanded bounds for more reliable collision
    Vector3 expandedPlayerSize = playerBounds.Size + new Vector3(collisionDetectionPadding, collisionDetectionPadding, 0);
    AABBBounds expandedPlayerBounds = new AABBBounds(playerBounds.Center, expandedPlayerSize, -1);
    
    for (int i = enemyMatrices.Count - 1; i >= 0; i--)
    {
        // Get enemy position and size
        Vector3 position = enemyMatrices[i].GetColumn(3);
        Vector3 scale = new Vector3(
            enemyMatrices[i].GetColumn(0).magnitude,
            enemyMatrices[i].GetColumn(1).magnitude,
            enemyMatrices[i].GetColumn(2).magnitude);
        
        Vector3 enemySize = new Vector3(
            enemyWidth * scale.x + collisionDetectionPadding,
            enemyHeight * scale.y + collisionDetectionPadding,
            enemyDepth * scale.z);
            
        AABBBounds enemyBounds = new AABBBounds(position, enemySize, -1);
        
        if (BoundsIntersect(expandedPlayerBounds, enemyBounds))
        {
            if (GameManager.Instance.IsPlayerInvincible())
            {
                // Player is invincible - destroy enemy immediately
                DestroyEnemy(i);
                continue; // Skip the rest of the collision handling for this enemy
            }
            
            // Normal collision handling (your original logic)
            // Calculate penetration depth and direction
            float penetrationX = Mathf.Min(
                enemyBounds.Max.x - playerBounds.Min.x,
                playerBounds.Max.x - enemyBounds.Min.x);
            
            float penetrationY = Mathf.Min(
                enemyBounds.Max.y - playerBounds.Min.y,
                playerBounds.Max.y - enemyBounds.Min.y);
            
            // Determine primary collision direction
            bool horizontalCollision = penetrationX < penetrationY;
            
            // Apply damage if cooldown allows
            if (canDamage && GameManager.Instance != null)
            {
                GameManager.Instance.TakeDamage(damageAmount);
                lastDamageTime = Time.time;
                canDamage = false;
            }
            
            // Calculate new position to resolve collision
            Vector3 newPosition = position;
            Quaternion rotation = Quaternion.LookRotation(
                enemyMatrices[i].GetColumn(2),
                enemyMatrices[i].GetColumn(1));
            
            if (horizontalCollision)
            {
                // Horizontal collision - push enemy left/right
                float directionX = Mathf.Sign(position.x - playerBounds.Center.x);
                newPosition.x += directionX * collisionBumpForce * Time.deltaTime;
                
                // Reverse movement direction
                enemyMovementDirections[i] = directionX;
            }
            else
            {
                // Vertical collision - only adjust if player is below enemy
                if (position.y > playerBounds.Center.y)
                {
                    float directionY = Mathf.Sign(position.y - playerBounds.Center.y);
                    newPosition.y += directionY * collisionBumpForce * Time.deltaTime;
                }
            }
            
            // Update enemy position
            Matrix4x4 newMatrix = Matrix4x4.TRS(newPosition, rotation, scale);
            enemyMatrices[i] = newMatrix;
            CollisionManager.Instance.UpdateCollider(enemyColliderIds[i], newPosition, enemySize);
            CollisionManager.Instance.UpdateMatrix(enemyColliderIds[i], newMatrix);
        }
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
        
        Vector3 size = new Vector3(enemyWidth * scale.x, enemyHeight * scale.y, enemyDepth * scale.z);
        
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

    void RenderEnemies()
    {
        if (enemyMaterial == null || enemyMesh == null) return;
        
        for (int i = 0; i < enemyMatrices.Count; i += 1023)
        {
            int batchSize = Mathf.Min(1023, enemyMatrices.Count - i);
            Matrix4x4[] batchMatrices = new Matrix4x4[batchSize];
            
            for (int j = 0; j < batchSize; j++)
            {
                batchMatrices[j] = enemyMatrices[i + j];
            }
            
            Graphics.DrawMeshInstanced(enemyMesh, 0, enemyMaterial, batchMatrices, batchSize);
        }
    }
    
    public bool CheckFireballCollision(Vector3 fireballPos, float fireballRadius)
    {
        for (int i = enemyColliderIds.Count - 1; i >= 0; i--)
        {
            Vector3 enemyPos = enemyMatrices[i].GetColumn(3);
            float distance = Vector3.Distance(fireballPos, enemyPos);
            float enemyRadius = Mathf.Max(enemyWidth, enemyHeight, enemyDepth) * 0.5f;
            
            if (distance < enemyRadius + fireballRadius)
            {
                DestroyEnemy(i);
                return true;
            }
        }
        return false;
    }
    
    public void DestroyEnemy(int index)
    {
        CollisionManager.Instance.RemoveCollider(enemyColliderIds[index]);
        enemyMatrices.RemoveAt(index);
        enemyColliderIds.RemoveAt(index);
        enemyMovementDirections.RemoveAt(index);
        enemySpawnPositionsX.RemoveAt(index);
    }
    
}