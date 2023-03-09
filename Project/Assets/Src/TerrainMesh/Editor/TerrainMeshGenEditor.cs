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

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Éú³É"))
            {
                GenMeshEdge();
            }
        }

        private void GenMeshEdge()
        {
            var meshFilters = Target.InParam.RootModels.GetComponentsInChildren<MeshFilter>();
            for (var i = 0; i != meshFilters.Length; ++i)
            {
                var colors = meshFilters[i].mesh.colors32;
                var points = meshFilters[i].mesh.vertices;
                var triangles = meshFilters[i].mesh.triangles;
                Debug.Assert(triangles.Length % 3 == 0);
                for (var j = 0; j != triangles.Length; j += 3)
                {
                    var aIndex = triangles[j                         ];
                    var bIndex = triangles[(j + 1) % triangles.Length];
                    var cIndex = triangles[(j + 2) % triangles.Length];

                    if (Mathm.Color32Equal(colors[aIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(colors[bIndex], Target.InParam.EdgeColor))
                    {
                        Target.OutParam.MeshEdges.Add(new Mathm.Line2D
                        {
                            P0 = new Vector2(points[aIndex].x, points[aIndex].z), P1 = new Vector2(points[bIndex].x, points[bIndex].z)
                        });
                    }

                    if (Mathm.Color32Equal(colors[bIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(colors[cIndex], Target.InParam.EdgeColor))
                    {
                        Target.OutParam.MeshEdges.Add(new Mathm.Line2D
                        {
                            P0 = new Vector2(points[bIndex].x, points[bIndex].z), P1 = new Vector2(points[cIndex].x, points[cIndex].z)
                        });
                    }

                    if (Mathm.Color32Equal(colors[cIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(colors[aIndex], Target.InParam.EdgeColor))
                    {
                        Target.OutParam.MeshEdges.Add(new Mathm.Line2D
                        {
                            P0 = new Vector2(points[cIndex].x, points[cIndex].z),P1 = new Vector2(points[aIndex].x, points[aIndex].z)
                        });
                    }
                }
            }
        }
    }
}