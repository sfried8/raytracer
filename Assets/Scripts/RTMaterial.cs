using UnityEngine;

[System.Serializable]
public struct RTMaterial
{
	public enum MaterialFlag
	{
		None,
		CheckerPattern,
		InvisibleLight
	}

	public Color color;
	public Color emissionColor;
	public Color specularColor;
	public float emissionStrength;
	[Range(0, 1)] public float smoothness;
	[Range(0, 1)] public float specularProbability;
	public MaterialFlag flag;
	public Color checkerColor2;
	public float checkerScale;
	[HideInInspector]
	public float invCheckerScale;

	public void SetDefaultValues()
	{
		color = Color.white;
		emissionColor = Color.white;
		emissionStrength = 0;
		specularColor = Color.white;
		smoothness = 0;
		specularProbability = 1;
	}
	public void SetInverseCheckerScale()
	{
		invCheckerScale = 1.0f / checkerScale;
	}
}