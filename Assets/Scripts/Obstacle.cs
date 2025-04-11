using UnityEngine;
using System.Collections.Generic;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

[System.Serializable]
public class Obstacle
{
    public string name = "Obstacle";
    public Vector3 position = Vector3.zero;
    public Vector3 scale = Vector3.one;
    public float rotationZ = 0f;
    public bool isStatic = true;
}

