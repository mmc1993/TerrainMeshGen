using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace mmc
{
    [CustomEditor(typeof(TerrainMeshGen))]
    public class TerrainMeshGenEditor : Editor
    {
        private TerrainMeshGen Target => target as TerrainMeshGen;

        //  生成Mesh边缘
        struct JobCutlineJoin : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<UnsafeList<float3>> AllMeshEdge;

            public NativeArray<(
                UnsafeList<float3>,
                UnsafeList<float3>
                )> OutMeshEdge;

            public void Execute(int index)
            {
                var firstEdge = -1;
                var edges0 = OutMeshEdge[index].Item1;
                var edges1 = OutMeshEdge[index].Item2;
                var allMeshEdge = AllMeshEdge[index];
                for (var i = 0; i != allMeshEdge.Length; i += 2)
                {
                    if (FindHead(ref allMeshEdge, i, out var headIndex))
                    {
                        if (firstEdge == -1)
                        {
                            firstEdge = headIndex;
                            FillLink(ref allMeshEdge, headIndex, ref edges0);
                        }
                        else if (firstEdge != headIndex)
                        {
                            FillLink(ref allMeshEdge, headIndex, ref edges1);
                        }
                    }
                    else
                    {
                        FillLink(ref allMeshEdge, headIndex, ref edges0);
                    }
                }
                OutMeshEdge[index] = (edges0, edges1);
            }

            private bool FindHead(ref UnsafeList<float3> list, int origin, out int result)
            {
                var isring = false;
                var index = origin;
                for (var exit = false; !exit; )
                {
                    exit = true;
                    for (var i = 0; i != list.Length; i += 2)
                    {
                        if (Mathm.Float3Equal(list[index], list[1 + origin]))
                        {
                            isring = true; break;
                        }

                        if (Mathm.Float3Equal(list[index], list[1 + i]))
                        {
                            index = i; exit = false; break;
                        }
                    }
                }
                result = index;
                return !isring;
            }

            private void FillLink(ref UnsafeList<float3> list, int origin, ref UnsafeList<float3> result)
            {
                result.Add(list[origin]);

                var index = origin;
                for (var exit = false; !exit; )
                {
                    exit = true;
                    for (var i = 0; i != list.Length; i += 2)
                    {
                        if (index == i) { continue; }

                        if (Mathm.Float3Equal(list[i + 1], list[origin]))
                        {
                            result.Add(list[i]); break; //  环
                        }

                        if (Mathm.Float3Equal(list[index + 1], list[i]))
                        {
                            index = i;
                            result.Add(list[i]);
                            exit = false; break;
                        }
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("生成"))
            {
                GenMeshEdge();
            }
        }

        private void GenMeshEdge()
        {
            var meshFilters = CollectMeshFilters(Target.InParam.RootModels);
            var allMeshEdge = new NativeArray<UnsafeList<float3>>(meshFilters.Length,
                                                                  Allocator.TempJob);
            for (var i = 0; i != meshFilters.Length; ++i)
            {
                var triangles = meshFilters[i].sharedMesh.triangles;
                var vertColors = meshFilters[i].sharedMesh.colors32;
                var vertPoints = meshFilters[i].sharedMesh.vertices;
                Debug.Assert(triangles.Length % 3 == 0);
                var edges = new UnsafeList<float3>(1, Allocator.TempJob);
                for (var j = 0; j != triangles.Length; j += 3)
                {
                    var aIndex = triangles[j];
                    var bIndex = triangles[(j + 1) % triangles.Length];
                    var cIndex = triangles[(j + 2) % triangles.Length];
                    if (Mathm.Color32Equal(vertColors[aIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[bIndex], Target.InParam.EdgeColor))
                    {
                        edges.Add(vertPoints[aIndex]); edges.Add(vertPoints[bIndex]);
                    }
                    if (Mathm.Color32Equal(vertColors[bIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[cIndex], Target.InParam.EdgeColor))
                    {
                        edges.Add(vertPoints[bIndex]); edges.Add(vertPoints[cIndex]);
                    }
                    if (Mathm.Color32Equal(vertColors[cIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[aIndex], Target.InParam.EdgeColor))
                    {
                        edges.Add(vertPoints[cIndex]); edges.Add(vertPoints[aIndex]);
                    }
                }
                allMeshEdge[i] = edges;
            }

            var outMeshEdge = new NativeArray<(
                UnsafeList<float3>,
                UnsafeList<float3>
            )>(allMeshEdge.Length, Allocator.TempJob);
            for (var i = 0; i != outMeshEdge.Length; ++i)
            {
                outMeshEdge[i] = (
                    new UnsafeList<float3>(1, Allocator.TempJob),
                    new UnsafeList<float3>(1, Allocator.TempJob)
                );
            }

            new JobCutlineJoin()
            {
                AllMeshEdge = allMeshEdge,
                OutMeshEdge = outMeshEdge,
            }.Schedule(allMeshEdge.Length, 64).Complete();




            //  Dispose
            for (var i = 0; i != outMeshEdge.Length; ++i)
            {
                outMeshEdge[i].Item1.Dispose();
                outMeshEdge[i].Item2.Dispose();
            }
            outMeshEdge.Dispose();

            for (var i = 0; i != allMeshEdge.Length; ++i)
            {
                allMeshEdge[i].Dispose();
            }
            allMeshEdge.Dispose();
        }

        private MeshFilter[] CollectMeshFilters(Transform root)
        {
            var list = new List<MeshFilter>();
            for (var i = 0; i != root.childCount; ++i)
            {
                list.Add(root.GetChild(i).GetComponent<MeshFilter>());
            }
            return list.ToArray();
        }
    }
}