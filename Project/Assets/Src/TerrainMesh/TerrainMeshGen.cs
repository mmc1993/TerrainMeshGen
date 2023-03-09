using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mmc
{
    public class TerrainMeshGen : MonoBehaviour
    {
        [System.Serializable]
        public struct InParam_t
        {
            public Color32 EdgeColor;

            public Vector2 MapOrigin;
            public Vector2 MapLnegth;
            public Vector2 TileSize;

            public Transform RootModels;
        }
        public InParam_t InParam;

        [System.Serializable]
        public struct OutParam_t
        {
            public List<Mathm.Line2D> MeshEdges;
            public List<Vector2[]>    MeshLines;
        }
        public OutParam_t OutParam;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(new Vector3(InParam.MapOrigin.x,                       0,                       InParam.MapOrigin.y), 10.0f);
            Gizmos.DrawSphere(new Vector3(InParam.MapOrigin.x + InParam.MapLnegth.x, 0, InParam.MapOrigin.y + InParam.MapLnegth.y), 10.0f);

            if (InParam.MapLnegth.x != 0 && InParam.TileSize.x != 0 &&
                InParam.MapLnegth.y != 0 && InParam.TileSize.y != 0)
            {
                var xTileCount = (int)(InParam.MapLnegth.x / InParam.TileSize.x);
                var yTileCount = (int)(InParam.MapLnegth.y / InParam.TileSize.y);
                for (var x = 0; x != xTileCount + 1; ++x)
                {
                    Gizmos.DrawLine(
                        new Vector3(InParam.MapOrigin.x + InParam.TileSize.x * x, 0, 0),
                        new Vector3(InParam.MapOrigin.x + InParam.TileSize.x * x, 0, InParam.MapOrigin.y + InParam.MapLnegth.y));
                }

                for (var y = 0; y != yTileCount + 1; ++y)
                {
                    Gizmos.DrawLine(
                        new Vector3(0, 0, InParam.MapOrigin.y + InParam.TileSize.y * y),
                        new Vector3(InParam.MapOrigin.x + InParam.MapLnegth.y, 0, InParam.MapOrigin.y + InParam.TileSize.y * y));
                }
            }
        }
    }
}