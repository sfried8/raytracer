using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTBounds
{
    public Vector3 min;
    public Vector3 max;
    public Vector3 center => (min + max) / 2.0f;
    public Vector3 size => max - min;
    public RTBounds(Vector3 min, Vector3 max)
    {

        this.min = min;
        this.max = max;
    }
    public RTBounds() : this(Vector3.positiveInfinity, Vector3.negativeInfinity) { }

    public void Encapsulate(Vector3 point)
    {
        this.min = Vector3.Min(min, point);
        this.max = Vector3.Max(max, point);
    }
    public void Encapsulate(Triangle triangle)
    {
        this.min = Vector3.Min(this.min, triangle.min);
        this.max = Vector3.Max(this.max, triangle.max);
    }
    public static RTBounds FromMinMax(Vector3 min, Vector3 max)
    {
        return new RTBounds(Vector3.Lerp(min, max, 0.5f), max - min);
    }
}
