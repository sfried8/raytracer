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

	[Range(0, 5)] public float refractive_index;

	public MaterialFlag flag;
	public Color checkerColor2;
	[Range(0, 1)] public float invCheckerScale;

	public void SetDefaultValues()
	{
		color = Color.white;
		emissionColor = Color.white;
		emissionStrength = 0;
		specularColor = Color.white;
		smoothness = 0;
		specularProbability = 1;
	}
}