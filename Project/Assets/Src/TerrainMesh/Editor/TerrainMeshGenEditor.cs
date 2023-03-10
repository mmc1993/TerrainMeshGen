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
        static class JobHelper
        {
            private static bool Float3Equals(float3 a, float3 b, float radius = math.EPSILON)
            {
                var diff = math.abs(a - b);
                return diff.x < radius
                    && diff.y < radius
                    && diff.z < radius;
            }

            private static bool FindHeadByEdge(ref UnsafeList<float3> list, int origin, out int result)
            {
                var exit = false;
                var ring = false;
                var curr = origin;
                var ringEnd = list[origin + 1];
                while (!exit)
                {
                    exit = true;
                    for (var i = 0; i != list.Length; i += 2)
                    {
                        if (Float3Equals(list[curr], list[i + 1]))
                        {
                            ring = Float3Equals(list[i], ringEnd);
                            if (!ring) { curr = i; exit = false; }
                            break;
                        }
                    }
                }
                result = curr;
                return !ring;
            }

            private static void FillLinkByEdge(ref UnsafeList<float3> list, int origin, ref UnsafeList<float3> result)
            {
                var curr = origin;
                result.Add(list[origin    ]);
                result.Add(list[origin + 1]);
                for (var exit = false; !exit;)
                {
                    exit = true;
                    for (var i = 0; i != list.Length; i += 2)
                    {
                        if (i == curr) { continue; }

                        var ipt = list[i + 1];
                        if (Float3Equals(list[curr + 1], list[i]))
                        {
                            exit = Float3Equals(ipt, list[origin]);
                            result.Add(ipt);
                            curr = i; break;
                        }
                    }
                }
            }

            //  生成Mesh边缘
            public struct JobCutedgeJoin : IJobParallelFor
            {
                [ReadOnly]
                public NativeArray<UnsafeList<float3>> InMeshEdges;

                [WriteOnly]
                public NativeArray<(
                    UnsafeList<float3>,
                    UnsafeList<float3>,
                    UnsafeList<float3>)> OutCutlines;

                public void Execute(int index)
                {
                    var outHead0 = -1;
                    var outHead1 = -1;
                    var outHead2 = -1;
                    UnsafeList<float3> outList0 = new(1, Allocator.TempJob);
                    UnsafeList<float3> outList1 = new(1, Allocator.TempJob);
                    UnsafeList<float3> outList2 = new(1, Allocator.TempJob);
                    var meshEdges = InMeshEdges[index];
                    for (var i = 0; i != meshEdges.Length; i += 2)
                    {
                        if (FindHeadByEdge(ref meshEdges, i, out var headIndex))
                        {
                            if (outHead0 == -1)
                            {
                                outHead0 = headIndex;
                                FillLinkByEdge(ref meshEdges, headIndex, ref outList0);
                            }
                            else if (outHead1 == -1 && outHead0 != headIndex)
                            {
                                outHead1 = headIndex;
                                FillLinkByEdge(ref meshEdges, headIndex, ref outList1);
                            }
                            else if (outHead2 == -1 && outHead0 != headIndex && outHead1 != headIndex)
                            {
                                outHead2 = headIndex;
                                FillLinkByEdge(ref meshEdges, headIndex, ref outList2);
                            }

                            if (outHead0 != -1 && outHead1 != -1 && outHead2 != -1)
                            {
                                break;
                            }
                        }
                        else
                        {
                            FillLinkByEdge(ref meshEdges, headIndex, ref outList0); break;
                        }
                    }
                    OutCutlines[index] = (outList0, outList1, outList2);
                }
            }

            private static bool FindHeadByLine(ref NativeArray<UnsafeList<float3>> list, int origin, out int result)
            {
                var exit = false;
                var ring = false;
                var curr = origin;
                var ringEnd = list[origin][^1];
                while (!exit)
                {
                    exit = true;
                    for (var i = 0; i != list.Length; ++i)
                    {
                        if (Float3Equals(list[curr][0], list[i][^1]))
                        {
                            ring = Float3Equals(list[i][0], ringEnd);
                            if (!ring) { curr = i; exit = false; }
                            break;
                        }
                    }
                }
                result = curr;
                return !ring;
            }

            private static void FillLinkByLine(ref NativeArray<UnsafeList<float3>> list, int origin, ref UnsafeList<float3> result)
            {
                var curr = origin;
                result.AddRange(list[origin]);
                for (var exit = false; !exit;)
                {
                    exit = true;
                    for (var i = 0; i != list.Length; ++i)
                    {
                        if (i == curr) { continue; }

                        var ipt = list[i][^1];
                        if (Float3Equals(list[curr][^1], list[i][0]))
                        {
                            exit = Float3Equals(ipt, list[origin][0]);
                            for (var j = 1; j != list[i].Length; ++j)
                            {
                                result.Add(list[i][j]);
                            }
                            curr = i; break;
                        }
                    }
                }
            }

            //  生成Mesh边缘
            public struct JobCutlineJoin0 : IJobParallelFor
            {
                private static readonly object sLockMutex = new();

                [ReadOnly]
                public NativeArray<UnsafeList<float3>> InCutlines;
                [WriteOnly]
                public NativeHashSet<int> OutHeadIndexs;

                public void Execute(int index)
                {
                    FindHeadByLine(ref InCutlines, index, out int result);

                    lock (sLockMutex) { OutHeadIndexs.Add(result); }
                }
            }

            public struct JobCutlineJoin1 : IJobParallelFor
            {
                [ReadOnly]
                public NativeArray<UnsafeList<float3>> InCutlines;
                [ReadOnly]
                public NativeArray<int> InHeadIndexs;
                [WriteOnly]
                public NativeArray<UnsafeList<float3>> OutCutlines;

                public void Execute(int index)
                {
                    var list = new UnsafeList<float3>(1, Allocator.TempJob);
                    FillLinkByLine(ref InCutlines, InHeadIndexs[index], ref list);
                    OutCutlines[index] = list;
                }
            }
        }

        private TerrainMeshGen Target => target as TerrainMeshGen;

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
                var meshFilter = meshFilters[i];
                var vertColors = meshFilter.sharedMesh.colors32;
                var vertPoints = meshFilter.sharedMesh.vertices;
                var triangles = meshFilter.sharedMesh.triangles;
                Debug.Assert(triangles.Length % 3 == 0);
                var edges = new UnsafeList<float3>(1, Allocator.TempJob);
                for (var j = 0; j != triangles.Length; j += 3)
                {
                    var aIndex = triangles[j];
                    var bIndex = triangles[(j + 1) % triangles.Length];
                    var cIndex = triangles[(j + 2) % triangles.Length];
                    if (Mathm.Color32Equal(vertColors[aIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[bIndex], Target.InParam.EdgeColor))
                    {
                        edges.Add(meshFilter.transform.TransformPoint(vertPoints[aIndex]));
                        edges.Add(meshFilter.transform.TransformPoint(vertPoints[bIndex]));
                    }
                    if (Mathm.Color32Equal(vertColors[bIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[cIndex], Target.InParam.EdgeColor))
                    {
                        edges.Add(meshFilter.transform.TransformPoint(vertPoints[bIndex]));
                        edges.Add(meshFilter.transform.TransformPoint(vertPoints[cIndex]));
                    }
                    if (Mathm.Color32Equal(vertColors[cIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[aIndex], Target.InParam.EdgeColor))
                    {
                        edges.Add(meshFilter.transform.TransformPoint(vertPoints[cIndex]));
                        edges.Add(meshFilter.transform.TransformPoint(vertPoints[aIndex]));
                    }
                }
                allMeshEdge[i] = edges;
            }

            var outMeshEdge = new NativeArray<(
                UnsafeList<float3>,
                UnsafeList<float3>,
                UnsafeList<float3>
            )>(allMeshEdge.Length, Allocator.TempJob);

            new JobHelper.JobCutedgeJoin()
            {
                InMeshEdges = allMeshEdge,
                OutCutlines = outMeshEdge,
            }.Schedule(allMeshEdge.Length, 64).Complete();


            //  copy
            Target.OutParam.MeshEdges = new();
            for (var i = 0; i != outMeshEdge.Length; ++i)
            {
                if (outMeshEdge[i].Item1.Length != 0)
                {
                    var genEdge = new float3[outMeshEdge[i].Item1.Length];
                    for (var j = 0; j != outMeshEdge[i].Item1.Length; ++j)
                    {
                        genEdge[j] = outMeshEdge[i].Item1[j];
                    }
                    Target.OutParam.MeshEdges.Add(genEdge);
                }

                if (outMeshEdge[i].Item2.Length != 0)
                {
                    var genEdge = new float3[outMeshEdge[i].Item2.Length];
                    for (var j = 0; j != outMeshEdge[i].Item2.Length; ++j)
                    {
                        genEdge[j] = outMeshEdge[i].Item2[j];
                    }
                    Target.OutParam.MeshEdges.Add(genEdge);
                }

                if (outMeshEdge[i].Item3.Length != 0)
                {
                    var genEdge = new float3[outMeshEdge[i].Item3.Length];
                    for (var j = 0; j != outMeshEdge[i].Item3.Length; ++j)
                    {
                        genEdge[j] = outMeshEdge[i].Item3[j];
                    }
                    Target.OutParam.MeshEdges.Add(genEdge);
                }
            }

            //  Dispose
            for (var i = 0; i != outMeshEdge.Length; ++i)
            {
                outMeshEdge[i].Item1.Dispose();
                outMeshEdge[i].Item2.Dispose();
                outMeshEdge[i].Item3.Dispose();
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