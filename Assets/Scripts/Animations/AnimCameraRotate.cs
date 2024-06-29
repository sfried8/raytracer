using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimCameraRotate : RTAnimation
{
    public GameObject target;
    public override void AnimationStep()
    {
        transform.Translate(0.15f, 0.05f, 0);
        transform.LookAt(target.transform);
    }
}
