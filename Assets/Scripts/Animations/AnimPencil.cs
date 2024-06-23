using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimPencil : RTAnimation
{
    public override void AnimationStep()
    {
        transform.Translate(0.01f, 0, 0);
    }
}
