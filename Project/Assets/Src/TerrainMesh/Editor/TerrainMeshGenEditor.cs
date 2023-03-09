using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace mmc
{
    [CustomEditor(typeof(TerrainMeshGen))]
    public class TerrainMeshGenEditor : Editor
    {
        private TerrainMeshGen Target => target as TerrainMeshGen;
    }
}