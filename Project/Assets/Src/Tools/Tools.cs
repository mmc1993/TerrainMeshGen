
using System.Collections;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine;
using System.Net;
using System.IO;
using Unity.Collections;

namespace mmc
{
    public static class Tools
    {
        //
        //  Õ®”√
        public static void Swap<T>(ref T v0, ref T v1)
        {
            var t = v0;
            v0 = v1;
            v1 = t;
        }

        public static void Swap(IList list, int i0, int i1)
        {
            var t = list[i0];
            list[i0] = list[i1];
            list[i1] = t;
        }
    }
}