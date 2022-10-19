using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CreateAssetMenu(fileName = "MixAmoTargetAvatar")]
public class MixamoImportSetting : ScriptableObject
{
	[Header("Import")]
	[SerializeField]
	string mixamoDirectory = "Mixamo";
	public string textureDirectory = "Textures";
		
	[Header("Animation")]
	public bool loopAnimation = false;
	public bool applyRootMotion = false;

	public bool createAnimationController = false;
	public string animationDirectory = "Animations";
	
	[Header("Avatar")]
	public Avatar avatar;

	public string MixamoDirectory {
		get {
			return Application.dataPath + "/" + mixamoDirectory;
		}
	}
}


