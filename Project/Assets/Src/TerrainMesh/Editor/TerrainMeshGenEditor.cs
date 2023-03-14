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
        struct Float3Pair
        {
            public float3 P0;
            public float3 P1;
        }

        static class JobHelper
        {
            private static bool F3Equals(float3 a, float3 b, float radius = math.EPSILON)
            {
                var diff = math.abs(a - b);
                return diff.x < radius
                    && diff.y < radius
                    && diff.z < radius;
            }

            private static bool FindCutline0(ref UnsafeList<Float3Pair> list, int origin, out int result)
            {
                var exit = false;
                var ring = false;
                var curr = origin;
                var ringEnd = list[origin].P1;
                while (!exit)
                {
                    exit = true;
                    for (var i = 0; i != list.Length; ++i)
                    {
                        if (F3Equals(list[curr].P0, list[i].P1))
                        {
                            ring = F3Equals(list[i].P0, ringEnd);
                            if (!ring) { curr = i; exit = false; }
                            break;
                        }
                    }
                }
                result = curr;
                return !ring;
            }

            private static void LinkCutline0(ref UnsafeList<Float3Pair> list, int origin, ref UnsafeList<float3> result)
            {
                var curr = origin;
                result.Add(list[origin].P0);
                result.Add(list[origin].P1);
                for (var exit = false; !exit;)
                {
                    exit = true;
                    for (var i = 0; i != list.Length; ++i)
                    {
                        if (i == curr) { continue; }

                        var pt = list[i].P1;
                        if (F3Equals(list[curr].P1, list[i].P0))
                        {
                            exit = F3Equals(pt,list[origin].P0);
                            result.Add(pt);
                            curr = i;break;
                        }
                    }
                }
            }

            public struct JobCombineCutedge0 : IJobParallelFor
            {
                [ReadOnly]
                public NativeArray<UnsafeList<Float3Pair>> InCutlines;

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
                    var meshEdges = InCutlines[index];
                    for (var i = 0; i != meshEdges.Length; ++i)
                    {
                        if (FindCutline0(ref meshEdges, i, out var headIndex))
                        {
                            if (outHead0 == -1)
                            {
                                outHead0 = headIndex;
                                LinkCutline0(ref meshEdges, headIndex, ref outList0);
                            }
                            else if (outHead1 == -1 && outHead0 != headIndex)
                            {
                                outHead1 = headIndex;
                                LinkCutline0(ref meshEdges, headIndex, ref outList1);
                            }
                            else if (outHead2 == -1 && outHead0 != headIndex && outHead1 != headIndex)
                            {
                                outHead2 = headIndex;
                                LinkCutline0(ref meshEdges, headIndex, ref outList2);
                            }

                            if (outHead0 != -1 && outHead1 != -1 && outHead2 != -1) { break; }
                        }
                        else
                        {
                            LinkCutline0(ref meshEdges, headIndex, ref outList0); break;
                        }
                    }
                    OutCutlines[index] = (outList0, outList1, outList2);
                }
            }

            private static bool FindCutline1(ref NativeArray<UnsafeList<float3>> list, int origin, out int result)
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
                        if (F3Equals(list[curr][0], list[i][^1]))
                        {
                            ring = F3Equals(list[i][0], ringEnd);
                            if (!ring) { curr = i; exit = false; }
                            break;
                        }
                    }
                }
                result = curr;
                return !ring;
            }

            private static void LinkCutline1(ref NativeArray<UnsafeList<float3>> list, int origin, ref UnsafeList<float3> result)
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
                        if (F3Equals(list[curr][^1], list[i][0]))
                        {
                            exit = F3Equals(ipt, list[origin][0]);
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
            public struct JobCombineCutline1 : IJobParallelFor
            {
                private static readonly object sLockMutex = new();

                [ReadOnly]
                public NativeArray<UnsafeList<float3>> InCutlines;
                [WriteOnly]
                public NativeHashSet<int> OutHeadIndexs;

                public void Execute(int index)
                {
                    FindCutline1(ref InCutlines, index, out int result);

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
                    LinkCutline1(ref InCutlines, InHeadIndexs[index], ref list);
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

        private NativeArray<UnsafeList<Float3Pair>> CombineCutline0()
        {
            var meshFilters = CollectMeshFilters(Target.InParam.RootModels);
            using var result = new NativeQueue<UnsafeList<Float3Pair>>(Allocator.TempJob);
            for (var i = 0; i != meshFilters.Length; ++i)
            {
                var meshFilter = meshFilters[i];
                var vertColors = meshFilter.sharedMesh.colors32;
                var vertPoints = meshFilter.sharedMesh.vertices;
                var triangles = meshFilter.sharedMesh.triangles;

                var pairs = new UnsafeList<Float3Pair>(1, Allocator.TempJob);
                for (var j = 0; j != triangles.Length; j += 3)
                {
                    var aIndex = triangles[j];
                    var bIndex = triangles[(j + 1) % triangles.Length];
                    var cIndex = triangles[(j + 2) % triangles.Length];
                    if (Mathm.Color32Equal(vertColors[aIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[bIndex], Target.InParam.EdgeColor))
                    {
                        pairs.Add(new Float3Pair
                        {
                            P0 = meshFilter.transform.TransformPoint(vertPoints[aIndex]),
                            P1 = meshFilter.transform.TransformPoint(vertPoints[bIndex]),
                        });
                    }
                    if (Mathm.Color32Equal(vertColors[bIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[cIndex], Target.InParam.EdgeColor))
                    {
                        pairs.Add(new Float3Pair
                        {
                            P0 = meshFilter.transform.TransformPoint(vertPoints[bIndex]),
                            P1 = meshFilter.transform.TransformPoint(vertPoints[cIndex]),
                        });
                    }
                    if (Mathm.Color32Equal(vertColors[cIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[aIndex], Target.InParam.EdgeColor))
                    {
                        pairs.Add(new Float3Pair
                        {
                            P0 = meshFilter.transform.TransformPoint(vertPoints[cIndex]),
                            P1 = meshFilter.transform.TransformPoint(vertPoints[aIndex]),
                        });
                    }
                }
                if (!pairs.IsEmpty) { result.Enqueue(pairs); }
            }
            return result.ToArray(Allocator.TempJob);
        }

        private NativeArray<(
            UnsafeList<float3>,
            UnsafeList<float3>,
            UnsafeList<float3>)> CombineCutline1(ref NativeArray<UnsafeList<Float3Pair>> cutlines0)
        {
            var cutlines1 = new NativeArray<(
                UnsafeList<float3>,
                UnsafeList<float3>,
                UnsafeList<float3>
            )>(cutlines0.Length, Allocator.TempJob);

            new JobHelper.JobCombineCutedge0()
            {
                InCutlines  = cutlines0,
                OutCutlines = cutlines1,
            }.Schedule(cutlines0.Length, 64).Complete();

            return cutlines1;
        }

        private void GenMeshEdge()
        {
            var cutlines0 = CombineCutline0();
            var cutlines1 = CombineCutline1(ref cutlines0);
            //var cutlines2 = CombineCutline2(ref cutlines1);

            //  copy
            Target.OutParam.MeshEdges = new();
            for (var i = 0; i != cutlines1.Length; ++i)
            {
                if (cutlines1[i].Item1.Length != 0)
                {
                    var genEdge = new float3[cutlines1[i].Item1.Length];
                    for (var j = 0; j != cutlines1[i].Item1.Length; ++j)
                    {
                        genEdge[j] = cutlines1[i].Item1[j];
                    }
                    Target.OutParam.MeshEdges.Add(genEdge);
                }

                if (cutlines1[i].Item2.Length != 0)
                {
                    var genEdge = new float3[cutlines1[i].Item2.Length];
                    for (var j = 0; j != cutlines1[i].Item2.Length; ++j)
                    {
                        genEdge[j] = cutlines1[i].Item2[j];
                    }
                    Target.OutParam.MeshEdges.Add(genEdge);
                }

                if (cutlines1[i].Item3.Length != 0)
                {
                    var genEdge = new float3[cutlines1[i].Item3.Length];
                    for (var j = 0; j != cutlines1[i].Item3.Length; ++j)
                    {
                        genEdge[j] = cutlines1[i].Item3[j];
                    }
                    Target.OutParam.MeshEdges.Add(genEdge);
                }
            }

            //  Dispose
            for (var i = 0; i != cutlines1.Length; ++i)
            {
                cutlines0[i].Dispose();
            }
            cutlines0.Dispose();

            for (var i = 0; i != cutlines1.Length; ++i)
            {
                cutlines1[i].Item1.Dispose();
                cutlines1[i].Item2.Dispose();
                cutlines1[i].Item3.Dispose();
            }
            cutlines1.Dispose();
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