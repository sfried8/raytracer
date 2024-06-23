using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bananimation : RTAnimation
{
    public override void AnimationStep()
    {
        transform.Rotate(0, 0, 3);
    }
}
