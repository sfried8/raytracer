using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class RTAnimation : MonoBehaviour
{
    public int framesPerSnapshot;
    int frameNumber;
    // Start is called before the first frame update
    public bool OnFrameComplete()
    {
        return frameNumber++ >= framesPerSnapshot;
    }
    public abstract void AnimationStep();
    public void NextStep()
    {
        AnimationStep();
        frameNumber = 0;
    }
}
