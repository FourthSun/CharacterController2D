using System;

namespace FourthSun
{
    [Flags]
    public enum CollisionFlags2D
    {
        None = 0,
        Above = 1,
        Below = 2,
        Sides = 4,
    }
}