using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    public EnhancedMeshGenerator meshGenerator;
    public Material healthPowerUpMaterial;
    public Material invincibilityPowerUpMaterial;
    public Material fireballPowerUpMaterial;
    public int powerUpCount = 10;
    public float powerUpHeight = 1f;
    public float powerUpSize = 0.5f;
    public float spawnPadding = 2f;
    public FireballManager fireballManager;

    private Mesh triangleMesh;
    internal List<Matrix4x4> powerUpMatrices = new List<Matrix4x4>();
    private List<int> powerUpColliderIds = new List<int>();
    private List<PowerUpType> powerUpTypes = new List<PowerUpType>();

    private enum PowerUpType
    {
        Health,
        Invincibility,
        Fireball
    }

    void Start()
    {
        if (healthPowerUpMaterial != null) healthPowerUpMaterial.enableInstancing = true;
        if (invincibilityPowerUpMaterial != null) invincibilityPowerUpMaterial.enableInstancing = true;
        if (fireballPowerUpMaterial != null) fireballPowerUpMaterial.enableInstancing = true;
        meshGenerator = FindObjectOfType<EnhancedMeshGenerator>();

        CreatePowerUpMeshes();
        SpawnPowerUps();
    }

    void CreatePowerUpMeshes()
    {
        triangleMesh = new Mesh();
        Vector3[] triangleVertices = new Vector3[4]
        {
            new Vector3(0, 0, 0),
            new Vector3(powerUpSize, 0, 0),
            new Vector3(powerUpSize/2, 0, powerUpSize),
            new Vector3(powerUpSize/2, powerUpSize, powerUpSize/2)
        };
        triangleMesh.vertices = triangleVertices;
        triangleMesh.triangles = new int[12] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 };
        triangleMesh.RecalculateNormals();
    }

    void SpawnPowerUps()
    {
        float playerStartX = 0f;
        float rightSideLength = meshGenerator.maxX - playerStartX;
        float sectionLength = rightSideLength / powerUpCount;
        
        // Get reference to ObstacleManager
        ObstacleManager obstacleManager = FindObjectOfType<ObstacleManager>();
        if (obstacleManager == null)
        {
            Debug.LogError("ObstacleManager not found!");
            return;
        }

        int maxAttempts = 10; // Maximum attempts to find a clear position
        int powerupsSpawned = 0;
        
        for (int i = 0; i < powerUpCount; i++)
        {
            float sectionStart = playerStartX + (i * sectionLength);
            float sectionEnd = sectionStart + sectionLength;
            
            Vector3 position = Vector3.zero;
            bool positionFound = false;
            int attempts = 0;
            
            // Try to find a clear position
            while (!positionFound && attempts < maxAttempts)
            {
                attempts++;
                
                position = new Vector3(
                    Random.Range(sectionStart + spawnPadding, sectionEnd - spawnPadding),
                    meshGenerator.groundY + powerUpHeight,
                    meshGenerator.constantZPosition
                );
                
                // Check if position is clear of obstacles
                if (obstacleManager.IsPositionClear(position, powerUpSize * 1.5f))
                {
                    positionFound = true;
                }
            }
            
            if (!positionFound) continue; // Skip if we couldn't find a clear spot
            
            PowerUpType type = (PowerUpType)Random.Range(0, 3);
            Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            Vector3 scale = Vector3.one;

            int id = CollisionManager.Instance.RegisterCollider(
                position, 
                new Vector3(powerUpSize, powerUpSize, powerUpSize), 
                false);

            Matrix4x4 powerUpMatrix = Matrix4x4.TRS(position, rotation, scale);
            powerUpMatrices.Add(powerUpMatrix);
            powerUpColliderIds.Add(id);
            powerUpTypes.Add(type);

            CollisionManager.Instance.UpdateMatrix(id, powerUpMatrix);
            powerupsSpawned++;
        }
        
        Debug.Log($"Spawned {powerupsSpawned} powerups (attempted {powerUpCount})");
    }

    void Update()
    {
        CheckPlayerCollision();
        RenderPowerUps();
    }

    void CheckPlayerCollision()
    {
        if (meshGenerator.GetPlayerID() == -1) return;
        
        var playerMatrix = CollisionManager.Instance.GetMatrix(meshGenerator.GetPlayerID());
        Vector3 playerPos = playerMatrix.GetPosition();
        Vector3 playerSize = meshGenerator.GetPlayerSize();
        float playerRadius = Mathf.Max(playerSize.x, playerSize.y, playerSize.z) * 0.5f;
        
        for (int i = powerUpColliderIds.Count - 1; i >= 0; i--)
        {
            int powerUpId = powerUpColliderIds[i];
            var powerUpMatrix = CollisionManager.Instance.GetMatrix(powerUpId);
            Vector3 powerUpPos = powerUpMatrix.GetPosition();
            
            float dx = playerPos.x - powerUpPos.x;
            float dy = playerPos.y - powerUpPos.y;
            float dz = playerPos.z - powerUpPos.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;
            
            float combinedRadius = playerRadius + powerUpSize;
            if (sqrDistance < combinedRadius * combinedRadius)
            {
                ApplyPowerUpEffect(powerUpTypes[i]);
                
                CollisionManager.Instance.RemoveCollider(powerUpId);
                powerUpMatrices.RemoveAt(i);
                powerUpColliderIds.RemoveAt(i);
                powerUpTypes.RemoveAt(i);
            }
        }
    }

    // Add this to the PowerUpManager's ApplyPowerUpEffect method
    void ApplyPowerUpEffect(PowerUpType type)
    {
        if (GameManager.Instance == null) return;

        switch (type)
        {
            case PowerUpType.Health:
                GameManager.Instance.HealPlayer(1);
                break;
            
            case PowerUpType.Invincibility:
                GameManager.Instance.ActivateInvincibility();
                break;
            
            case PowerUpType.Fireball:
                ActivateFireball();
                break;
        }
    }
    
    void ActivateFireball()
    {
        if (fireballManager == null || meshGenerator == null) return;
        
        int playerId = meshGenerator.GetPlayerID();
        Vector3 playerPos = CollisionManager.Instance.GetMatrix(playerId).GetColumn(3);
        Vector3 fireDirection = Vector3.right;
        
        fireballManager.SpawnFireball(playerPos + Vector3.right, fireDirection);
    }

    void RenderPowerUps()
    {
        if (powerUpMatrices.Count == 0) return;
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Separate matrices by type
        List<Matrix4x4> healthMatrices = new List<Matrix4x4>();
        List<Matrix4x4> invincibilityMatrices = new List<Matrix4x4>();
        List<Matrix4x4> fireballMatrices = new List<Matrix4x4>();

        for (int i = 0; i < powerUpMatrices.Count; i++)
        {
            Vector3 powerUpPos = powerUpMatrices[i].GetPosition();
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(powerUpPos);
            
            bool isVisible = viewportPos.x > -0.5f && viewportPos.x < 1.5f && 
                           viewportPos.y > -0.5f && viewportPos.y < 1.5f &&
                           viewportPos.z > mainCamera.nearClipPlane;

            Matrix4x4 matrix = isVisible ? powerUpMatrices[i] : 
                Matrix4x4.TRS(powerUpPos, powerUpMatrices[i].rotation, Vector3.zero);

            switch (powerUpTypes[i])
            {
                case PowerUpType.Health:
                    healthMatrices.Add(matrix);
                    break;
                case PowerUpType.Invincibility:
                    invincibilityMatrices.Add(matrix);
                    break;
                case PowerUpType.Fireball:
                    fireballMatrices.Add(matrix);
                    break;
            }
        }

        // Render health power-ups (triangle)
        if (healthPowerUpMaterial != null && healthMatrices.Count > 0)
        {
            RenderPowerUpBatch(triangleMesh, healthPowerUpMaterial, healthMatrices);
        }

        // Render invincibility power-ups (triangle)
        if (invincibilityPowerUpMaterial != null && invincibilityMatrices.Count > 0)
        {
            RenderPowerUpBatch(triangleMesh, invincibilityPowerUpMaterial, invincibilityMatrices);
        }

        // Render fireball power-ups (triangle)
        if (fireballPowerUpMaterial != null && fireballMatrices.Count > 0)
        {
            RenderPowerUpBatch(triangleMesh, fireballPowerUpMaterial, fireballMatrices);
        }
    }

    void RenderPowerUpBatch(Mesh mesh, Material material, List<Matrix4x4> matrices)
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
    
    
}
