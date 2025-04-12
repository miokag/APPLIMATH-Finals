using System.Collections.Generic;
using UnityEngine;

public class FireballManager : MonoBehaviour
{
    public EnhancedMeshGenerator meshGenerator;
    public Material fireballMaterial;
    public float fireballSpeed = 10f;
    public float fireballSize = 0.5f;
    public float fireballLifetime = 3f;
    
    private Mesh fireballMesh;
    private List<Matrix4x4> fireballMatrices = new List<Matrix4x4>();
    private List<int> fireballColliderIds = new List<int>();
    private List<float> fireballTimers = new List<float>();
    private List<Vector3> fireballDirections = new List<Vector3>();
    
    [Header("Fireball Settings")]
    public float fireballSpawnHeight = 1.5f;

    void Start()
    {
        if (fireballMaterial != null) fireballMaterial.enableInstancing = true;
        meshGenerator = FindObjectOfType<EnhancedMeshGenerator>();
        CreateFireballMesh();
    }

    void CreateFireballMesh()
    {
        fireballMesh = new Mesh();
        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(0, 0, 0),
            new Vector3(fireballSize, 0, 0),
            new Vector3(fireballSize, 0, fireballSize),
            new Vector3(0, 0, fireballSize),
            new Vector3(0, fireballSize, 0),
            new Vector3(fireballSize, fireballSize, 0),
            new Vector3(fireballSize, fireballSize, fireballSize),
            new Vector3(0, fireballSize, fireballSize)
        };
        
        int[] triangles = new int[36]
        {
            0, 2, 1, 0, 3, 2, // Bottom
            4, 5, 6, 4, 6, 7, // Top
            0, 1, 5, 0, 5, 4, // Front
            1, 2, 6, 1, 6, 5, // Right
            2, 3, 7, 2, 7, 6, // Back
            3, 0, 4, 3, 4, 7  // Left
        };
        
        fireballMesh.vertices = vertices;
        fireballMesh.triangles = triangles;
        fireballMesh.RecalculateNormals();
    }

    public void SpawnFireball(Vector3 position, Vector3 direction)
    {
        position.y += fireballSpawnHeight;
    
        Quaternion rotation = Quaternion.LookRotation(direction);
        Vector3 scale = Vector3.one * fireballSize;
    
        int id = CollisionManager.Instance.RegisterCollider(
            position, 
            new Vector3(fireballSize, fireballSize, fireballSize), 
            false);
    
        Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
        fireballMatrices.Add(matrix);
        fireballColliderIds.Add(id);
        fireballTimers.Add(fireballLifetime);
        fireballDirections.Add(direction.normalized);
    
        CollisionManager.Instance.UpdateMatrix(id, matrix);
    }

    void Update()
    {
        MoveFireballs();
        CheckCollisions();
        RenderFireballs();
        UpdateTimers();
    }

    void MoveFireballs()
    {
        for (int i = 0; i < fireballMatrices.Count; i++)
        {
            Vector3 position = fireballMatrices[i].GetColumn(3);
            position += fireballDirections[i] * fireballSpeed * Time.deltaTime;
            
            Quaternion rotation = Quaternion.LookRotation(fireballDirections[i]);
            Matrix4x4 newMatrix = Matrix4x4.TRS(position, rotation, Vector3.one * fireballSize);
            fireballMatrices[i] = newMatrix;
            
            CollisionManager.Instance.UpdateCollider(fireballColliderIds[i], position, 
                new Vector3(fireballSize, fireballSize, fireballSize));
            CollisionManager.Instance.UpdateMatrix(fireballColliderIds[i], newMatrix);
        }
    }

    void CheckCollisions()
    {
        for (int i = fireballColliderIds.Count - 1; i >= 0; i--)
        {
            int fireballId = fireballColliderIds[i];
            Vector3 fireballPos = fireballMatrices[i].GetColumn(3);
            
            // Check if fireball is off-screen
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 viewportPos = mainCamera.WorldToViewportPoint(fireballPos);
                if (viewportPos.x < -0.1f || viewportPos.x > 1.1f || 
                    viewportPos.y < -0.1f || viewportPos.y > 1.1f)
                {
                    DestroyFireball(i);
                    continue;
                }
            }
            
            // Check collision with enemies (you'll need a reference to EnemyManager)
            if (EnemyManager.Instance != null)
            {
                if (EnemyManager.Instance.CheckFireballCollision(fireballPos, fireballSize))
                {
                    DestroyFireball(i);
                }
            }
        }
    }

    void DestroyFireball(int index)
    {
        CollisionManager.Instance.RemoveCollider(fireballColliderIds[index]);
        fireballMatrices.RemoveAt(index);
        fireballColliderIds.RemoveAt(index);
        fireballTimers.RemoveAt(index);
        fireballDirections.RemoveAt(index);
    }

    void UpdateTimers()
    {
        for (int i = fireballTimers.Count - 1; i >= 0; i--)
        {
            fireballTimers[i] -= Time.deltaTime;
            if (fireballTimers[i] <= 0)
            {
                DestroyFireball(i);
            }
        }
    }

    void RenderFireballs()
    {
        if (fireballMaterial == null || fireballMesh == null) return;
        
        for (int i = 0; i < fireballMatrices.Count; i += 1023)
        {
            int batchSize = Mathf.Min(1023, fireballMatrices.Count - i);
            Matrix4x4[] batchMatrices = new Matrix4x4[batchSize];
            
            for (int j = 0; j < batchSize; j++)
            {
                batchMatrices[j] = fireballMatrices[i + j];
            }
            
            Graphics.DrawMeshInstanced(
                fireballMesh, 
                0, 
                fireballMaterial, 
                batchMatrices, 
                batchSize,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false
            );
        }
    }
}