#nullable enable
using System;
using UnityEngine;

namespace Statecraft.Game.Map
{
    /// <summary>Marks one feature's line in the true-polyline overlay (<see cref="ArmisticeLines"/>, #237): its id, the date
    /// it is shown from (ArmisticeLines.ShowAt) and whether it is drawn in the low-accuracy style.</summary>
    public sealed class ArmisticeLine : MonoBehaviour
    {
        public string Id = ""; public DateTime ShownFrom; public bool LowAccuracy;
    }
}
