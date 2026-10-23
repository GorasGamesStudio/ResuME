using System.Collections.Generic;
using GGS.ResuME.Data;
using UnityEngine;

namespace GGS.ResuME.World
{
    /// <summary>
    /// Geometry of the route as a polyline of legs: the stops sit on horizontal legs that always go
    /// right (the timeline), and a vertical leg is inserted between two stops of different places,
    /// up when the next place is further north, down otherwise (the map of France). Everything is
    /// addressed by a distance along the path, so the game logic stays one-dimensional.
    /// </summary>
    public sealed class RoutePath
    {
        #region Data Structures

        /// <summary>One straight piece of the path.</summary>
        public sealed class Leg
        {
            public float StartDistance;
            public float Length;
            public Vector2 Start;
            public Vector2 Direction;
            public bool Vertical;
            public float EndDistance => StartDistance + Length;
            public Vector2 End => Start + Direction * Length;
        }

        #endregion

        #region Fields

        private readonly List<Leg> _legs = new List<Leg>();
        private Vector2 _cursor;
        private float _distance;

        public IReadOnlyList<Leg> Legs => _legs;
        /// <summary>Total length of the path in world units.</summary>
        public float Length => _distance;

        #endregion

        #region Public Methods

        /// <summary>
        /// Appends a horizontal leg going right.
        /// </summary>
        /// <param name="length">Length in world units.</param>
        /// <returns>The distance at which the leg starts.</returns>
        public float AddHorizontal(float length)
        {
            return Add(Vector2.right, length, false);
        }

        /// <summary>
        /// Appends a vertical leg, up or down.
        /// </summary>
        /// <param name="length">Length in world units, positive.</param>
        /// <param name="up">True to go north (up on screen).</param>
        /// <returns>The distance at which the leg starts.</returns>
        public float AddVertical(float length, bool up)
        {
            return Add(up ? Vector2.up : Vector2.down, length, true);
        }

        /// <summary>
        /// World position at a distance along the path (clamped to the path).
        /// </summary>
        public Vector2 Position(float distance)
        {
            Leg leg = LegAt(distance);
            if (leg == null) return _cursor;
            float along = Mathf.Clamp(distance - leg.StartDistance, 0f, leg.Length);
            return leg.Start + leg.Direction * along;
        }

        /// <summary>
        /// Unit direction of travel at a distance along the path.
        /// </summary>
        public Vector2 Direction(float distance)
        {
            Leg leg = LegAt(distance);
            return leg != null ? leg.Direction : Vector2.right;
        }

        /// <summary>
        /// Unit vector pointing "into depth" (away from the road, where buildings stand) at a distance.
        /// Only meaningful on horizontal legs; zero on vertical ones.
        /// </summary>
        public Vector2 Depth(float distance)
        {
            Leg leg = LegAt(distance);
            return leg != null && !leg.Vertical ? Vector2.up : Vector2.zero;
        }

        /// <summary>
        /// The leg containing a distance (the last leg for distances past the end).
        /// </summary>
        public Leg LegAt(float distance)
        {
            for (int i = 0; i < _legs.Count; i++)
            {
                if (distance < _legs[i].EndDistance || i == _legs.Count - 1) return _legs[i];
            }
            return null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Appends a leg in a direction and advances the cursor.
        /// </summary>
        private float Add(Vector2 direction, float length, bool vertical)
        {
            Leg leg = new Leg { StartDistance = _distance, Length = Mathf.Max(0f, length), Start = _cursor, Direction = direction, Vertical = vertical };
            _legs.Add(leg);
            _cursor = leg.End;
            float start = _distance;
            _distance += leg.Length;
            return start;
        }

        #endregion
    }
}
