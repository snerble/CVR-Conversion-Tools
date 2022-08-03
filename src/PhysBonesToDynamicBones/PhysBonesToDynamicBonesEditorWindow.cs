using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Snerble.PhysBonesToDynamicBones.Editor
{
	public partial class PhysBonesToDynamicBonesEditorWindow : EditorWindow
	{
		// These values were found through trial and error
		const float MaxElasticityCompensationAmount = 0.2f;
		const float ElasticityCompensationHalfwayPoint = 0.15f;
		const float PlaneColliderRadius = 5000f;

		static readonly Dictionary<Vector3, DynamicBone.FreezeAxis> FreezeAxisMapping = new Dictionary<Vector3, DynamicBone.FreezeAxis>()
		{
			[Vector3.zero] = DynamicBone.FreezeAxis.None,
			[Vector3.right] = DynamicBone.FreezeAxis.X,
			[Vector3.up] = DynamicBone.FreezeAxis.Y,
			[Vector3.forward] = DynamicBone.FreezeAxis.Z,
		};

		static readonly AnimationCurve MaxAngleToStiffness;

		static PhysBonesToDynamicBonesEditorWindow()
		{
			MaxAngleToStiffness = new AnimationCurve(new Keyframe[]
			{
				new Keyframe(180f, 0.0f),
				new Keyframe(129f, 0.1f),
				new Keyframe(106f, 0.2f),
				new Keyframe(89f, 0.3f),
				new Keyframe(74f, 0.4f),
				new Keyframe(60f, 0.5f),
				new Keyframe(47f, 0.6f),
				new Keyframe(35f, 0.7f),
				new Keyframe(23f, 0.8f),
				new Keyframe(11f, 0.9f),
				new Keyframe(0f, 1.0f)
			});
			SmoothCurve(MaxAngleToStiffness);
		}

		[MenuItem("Tools/Convert PhysBones to Dynamic Bones", priority = 1020)]
		public static void Create()
		{
			var window = GetWindow<PhysBonesToDynamicBonesEditorWindow>();
			window.titleContent = new GUIContent("PhysBone Converter");
			window.Show();
		}

		GameObject m_Avatar;

		ConversionInfo m_ConversionInfo;

		bool m_CompensateElasticity = true;
		bool m_CleanupColliderObjects = true;

		void OnGUI()
		{
			_ = HelpText() &&
				AvatarField() &&
				GatherConversionInfo() &&
				ValidateConversionInfo() &&
				DisplayConversionInfo() &&
				ConversionSettings() &&
				ConversionButton();
		}

		bool HelpText()
		{
			EditorGUILayout.HelpBox(
				"This utility can automatically convert all PhysBones and PhysBone " +
				"colliders to Dynamic Bones while preserving their settings as " +
				"accurately as possible.",
				MessageType.Info, true);

			EditorGUILayout.Space();

			return true;
		}

		bool AvatarField()
		{
			m_Avatar = EditorGUILayout.ObjectField(
				m_Avatar,
				typeof(GameObject),
				true) as GameObject;

			return m_Avatar;
		}

		bool GatherConversionInfo()
		{
			var prefabObj = PrefabUtility.GetCorrespondingObjectFromSource(m_Avatar);
			
			m_ConversionInfo = new ConversionInfo()
			{
				AvatarRoot = prefabObj
					? PrefabUtility.GetNearestPrefabInstanceRoot(m_Avatar)
					: m_Avatar,

				IsPrefab = prefabObj,

				IsPartOfPrefab = prefabObj && prefabObj.transform.parent,

				Bones = m_Avatar.GetComponentsInChildren<VRCPhysBoneBase>(true)
					.Select(x => new PhysBoneChain(x))
					.ToList(),

				Colliders = m_Avatar.GetComponentsInChildren<VRCPhysBoneColliderBase>(true)
					.ToList(),
			};

			m_ConversionInfo.NewLeafBoneCount = m_ConversionInfo.Bones
				.Where(x => x.PhysBone.endpointPosition != Vector3.zero)
				.Select(x => x.LeafBones.Count)
				.Sum();

			m_ConversionInfo.SkippedColliderCount = m_ConversionInfo.Bones
				.SelectMany(x => x.PhysBone.colliders)
				.Except(m_ConversionInfo.Colliders)
				.Count();

			return true;
		}

		bool ValidateConversionInfo()
		{
			if (m_ConversionInfo.Bones.Count == 0 &&
				m_ConversionInfo.Colliders.Count == 0)
			{
				EditorGUILayout.HelpBox(
					"There are no PhysBones or PhysBone colliders on this object.",
					MessageType.Info, true);

				return false;
			}

			return true;
		}

		bool DisplayConversionInfo()
		{
			if (m_ConversionInfo.Bones.Count != 0)
			{
				EditorGUILayout.HelpBox(
					$"Found {m_ConversionInfo.Bones.Count} PhysBone(s)",
					MessageType.None, true);
			}

			if (m_ConversionInfo.Colliders.Count != 0)
			{
				EditorGUILayout.HelpBox(
					$"Found {m_ConversionInfo.Colliders.Count} PhysBone collider(s)",
					MessageType.None, true);
			}

			if (m_ConversionInfo.NewLeafBoneCount != 0)
			{
				EditorGUILayout.HelpBox(
					$"{m_ConversionInfo.NewLeafBoneCount} new leaf tip bone(s) will be created",
					MessageType.None, true);
			}

			if (m_ConversionInfo.SkippedColliderCount != 0)
			{
				EditorGUILayout.HelpBox(
					$"{m_ConversionInfo.SkippedColliderCount} referenced collider(s) will not be " +
					$"converted because they are not located under this object.",
					MessageType.Warning, true);
			}

			return true;
		}

		bool ConversionSettings()
		{
			EditorGUILayout.Space();

			if (m_ConversionInfo.Bones.Count != 0)
			{
				m_CompensateElasticity = EditorGUILayout.Toggle(
						new GUIContent(
							"Compensate elasticity",
							"Attempts to compensate for bone chains by reducing their elasticity.\n" +
							"May result in unexpected behavior!"),
						m_CompensateElasticity);
			}

			if (m_ConversionInfo.Colliders.Count != 0)
			{
				m_CleanupColliderObjects = EditorGUILayout.Toggle(
						new GUIContent(
							"Cleanup colliders",
							"Removes the objects containing PhysBoneColliders if they contain no other scripts."),
						m_CleanupColliderObjects);
			}

			return true;
		}

		bool ConversionButton()
		{
			EditorGUILayout.Space();

			if (m_ConversionInfo.IsPartOfPrefab)
			{
				// Prefabs must be duplicated as a whole, so display a warning
				EditorGUILayout.HelpBox(
					$"A backup will be created of '{m_ConversionInfo.AvatarRoot.name}'",
					MessageType.Warning, true);
			}

			if (GUILayout.Button("Convert"))
			{
				CreateBackup();

				var colliderMapping = new Dictionary<VRCPhysBoneColliderBase, DynamicBoneColliderBase>();

				ConvertPhysBoneColliders(colliderMapping);
				ConvertPhysBones(colliderMapping);

				return false;
			}

			return true;
		}

		void CreateBackup()
		{
			GameObject backup = null;

			try
			{
				var previouslyActive = Selection.activeGameObject;
				var previouslySelected = Selection.objects;

				// Emulate copy-pasting manually. This keeps stuff connected their prefabs
				Selection.activeGameObject = m_ConversionInfo.AvatarRoot;
				Unsupported.CopyGameObjectsToPasteboard();
				Unsupported.PasteGameObjectsFromPasteboard();
				backup = Selection.activeGameObject;

				// Restore selection
				Selection.objects = previouslySelected;
				Selection.activeGameObject = previouslyActive;

				backup.name = $"{m_ConversionInfo.AvatarRoot.name} (PhysBone backup)";
				backup.SetActive(false);
				backup.transform.SetSiblingIndex(m_ConversionInfo.AvatarRoot.transform.GetSiblingIndex() + 1);
				EnsureUniqueName(backup);
			}
			finally
			{
				if (backup)
					Undo.RegisterCreatedObjectUndo(backup, "Created backup of avatar");
			}
		}

		void ConvertPhysBones(Dictionary<VRCPhysBoneColliderBase, DynamicBoneColliderBase> colliderMapping)
		{
			foreach (var bone in m_ConversionInfo.Bones)
			{
				var physBone = bone.PhysBone;
				var dynamicBone = physBone.transform.gameObject.AddComponent<DynamicBone>();
				Undo.RegisterCreatedObjectUndo(dynamicBone, "PhysBone to DynamicBone conversion");

				dynamicBone.m_Root = physBone.GetRootTransform();
				dynamicBone.m_Exclusions = physBone.ignoreTransforms;

				// Dynamic bones are affected by their scale relative to their root
				float sizeScalar = Mathf.Abs(dynamicBone.m_Root.lossyScale.x) / Mathf.Abs(physBone.transform.lossyScale.x);

				#region Convert end position to leaf tip bones
				if (physBone.endpointPosition != Vector3.zero)
				{
					foreach (var leaf in bone.LeafBones)
					{
						var tip = new GameObject() { name = "DynLeafBoneTip" };
						Undo.RegisterCreatedObjectUndo(tip, "Created leaf bone tip");
						tip.transform.SetParent(leaf);
						tip.transform.localRotation = Quaternion.identity;
						tip.transform.localScale = Vector3.one;
						tip.transform.localPosition = physBone.endpointPosition;
					}
				}
				#endregion

				dynamicBone.m_Elasticity = physBone.pull;
				dynamicBone.m_ElasticityDistrib = physBone.pullCurve;

				#region Compensate for bone chain length
				if (m_CompensateElasticity)
				{
					float chainLength = bone.ChainLength.Value;
					// y = -a / (1/bx + 1) + a
					// hyperbolic curve
					// a = max value (if x == infinity)
					// b = value for x when y = 0.5a
					float elasticityReduction = -MaxElasticityCompensationAmount / ((1 / ElasticityCompensationHalfwayPoint) * chainLength + 1) + MaxElasticityCompensationAmount;

					dynamicBone.m_Elasticity -= dynamicBone.m_Elasticity * elasticityReduction;
				}
				#endregion

				#region Convert spring to damping
				dynamicBone.m_Damping = 1 - physBone.spring;
				if (physBone.springCurve != null)
				{
					dynamicBone.m_DampingDistrib = new AnimationCurve(physBone.springCurve.keys
						.Select(x =>
						{
							// Invert curve
							x.value = 1 - x.value;
							x.inTangent *= -1;
							x.outTangent *= -1;
							return x;
						})
						.ToArray());
				}
				#endregion

				dynamicBone.m_Inert = physBone.immobile;
				dynamicBone.m_InertDistrib = physBone.immobileCurve;

				dynamicBone.m_Radius = physBone.radius * sizeScalar;
				dynamicBone.m_RadiusDistrib = physBone.radiusCurve;

				#region Convert limit type to freeze axis
				if (physBone.limitType == VRCPhysBoneBase.LimitType.Hinge &&
					FreezeAxisMapping.TryGetValue(physBone.staticFreezeAxis.normalized, out var axis))
				{
					dynamicBone.m_FreezeAxis = axis;
				}
				#endregion

				#region Convert max angle to stiffness
				if (physBone.limitType == VRCPhysBoneBase.LimitType.Angle)
				{
					// Remap stiffness
					dynamicBone.m_Stiffness = MaxAngleToStiffness.Evaluate(physBone.maxAngleX);

					if (physBone.maxAngleXCurve != null)
					{
						dynamicBone.m_StiffnessDistrib = new AnimationCurve(physBone.maxAngleXCurve.keys
							.Select(x =>
							{
								// Remap curve
								x.value = MaxAngleToStiffness.Evaluate(x.value);
								return x;
							})
							.ToArray());
						SmoothCurve(dynamicBone.m_StiffnessDistrib);
					}
				}
				else
				{
					dynamicBone.m_Stiffness = 0;
				}
				#endregion

				dynamicBone.m_Colliders = physBone.colliders
					.Where(x => x && colliderMapping.ContainsKey(x))
					.Select(x => colliderMapping[x])
					.ToList();
			}

			// Delete PhysBones
			foreach (var bone in m_ConversionInfo.Bones)
			{
				Undo.DestroyObjectImmediate(bone.PhysBone);
			}
			// Delete PhysBone colliders
			foreach (var collider in m_ConversionInfo.Colliders)
			{
				if (m_CleanupColliderObjects &&
					collider.transform.GetComponents<Component>().Length == 2 &&
					collider.transform.childCount == 0)
				{
					Undo.DestroyObjectImmediate(collider.transform.gameObject);
				}
				else
				{
					Undo.DestroyObjectImmediate(collider);
				}
			}
		}

		void ConvertPhysBoneColliders(Dictionary<VRCPhysBoneColliderBase, DynamicBoneColliderBase> colliderMapping)
		{
			foreach (var pcollider in m_ConversionInfo.Colliders)
			{
				var root = pcollider.GetRootTransform();
				
				GameObject container;

				// Re-use root if no other scripts are on it (besides transform)
				if (root == pcollider.transform &&
					pcollider.GetComponents<Component>().Length == 2)
				{
					container = root.gameObject;
				}
				else
				{
					container = new GameObject() { name = $"DynCol_{root.gameObject.name}" };
					container.transform.SetParent(root);
					EnsureUniqueName(container);
					Undo.RegisterCreatedObjectUndo(container, "Created collider object");
					container.transform.localPosition = Vector3.zero;
					container.transform.localRotation = Quaternion.identity;
					container.transform.localScale = Vector3.one;
				}

				var dcollider = Undo.AddComponent<DynamicBoneCollider>(container);
				colliderMapping[pcollider] = dcollider;

				#region Scalar calculation
				// This whole section is bizarre. At least it ensures a 1-1 conversion ¯\_(ツ)_/¯

				var scale = dcollider.transform.lossyScale;

				float maxScale = Mathf.Max(dcollider.transform.lossyScale.x, dcollider.transform.lossyScale.y, dcollider.transform.lossyScale.z);
				float minScale = Mathf.Min(pcollider.transform.lossyScale.x, pcollider.transform.lossyScale.y, pcollider.transform.lossyScale.z);

				// Dynamic bones colliders scale by their global scale
				float dScalar = Mathf.Max(1, dcollider.transform.lossyScale.x);
				// PhysBone colliders only scale upwards when their global scale is above 1
				float pScalar = Mathf.Min(maxScale, pcollider.transform.lossyScale.x);
				#endregion

				if (pcollider.shapeType == VRCPhysBoneColliderBase.ShapeType.Plane)
				{
					dcollider.m_Radius = PlaneColliderRadius / dScalar;
					dcollider.m_Center += new Vector3(0, -PlaneColliderRadius / scale.y);
				}
				else
				{
					dcollider.m_Radius = pcollider.radius / pScalar;

					if (pcollider.shapeType == VRCPhysBoneColliderBase.ShapeType.Capsule)
					{
						float heightWithoutCaps = pcollider.height - pcollider.radius * 2;

						float heightScalar = scale.x / scale.y;

						// Fix incorrect height calculation for the dynamic bone collider
						dcollider.m_Height = heightWithoutCaps * heightScalar + dcollider.m_Radius * 2;
					}

					dcollider.m_Bound = pcollider.insideBounds
						? DynamicBoneColliderBase.Bound.Inside
						: DynamicBoneColliderBase.Bound.Outside;
				}

				container.transform.localPosition = Vector3.Scale(pcollider.position, container.transform.localScale);
				container.transform.localRotation = pcollider.rotation;

				// This shit doesnt work for non-uniform scaling
				// brain hurty
				// i've never been good at this kind of stuff
				// it works most of the time
			}
		}

		private static void EnsureUniqueName(GameObject go)
		{
			int siblingCount;
			IEnumerable siblings;
			
			if (go.transform.parent)
			{
				siblingCount = go.transform.parent.childCount - 1;
				siblings = go.transform.parent;
			}
			else
			{
				siblingCount = go.scene.rootCount - 1;
				siblings = go.scene.GetRootGameObjects()
					.Select(x => x.transform);
			}

			var siblingNames = new string[siblingCount];

			int i = 0;
			foreach (Transform sibling in siblings)
			{
				if (sibling == go.transform)
					continue;

				siblingNames[i++] = sibling.gameObject.name;
			}

			go.name = ObjectNames.GetUniqueName(siblingNames, go.name);
		}

		private static void SmoothCurve(AnimationCurve curve)
		{
			for (int i = 0; i < curve.length; i++)
			{
				curve.SmoothTangents(i, 0f);
			}
		}

		class ConversionInfo
		{
			public GameObject AvatarRoot { get; set; }
			public bool IsPrefab { get; set; }
			public bool IsPartOfPrefab { get; set; }

			public IReadOnlyList<PhysBoneChain> Bones { get; set; }
			public IReadOnlyList<VRCPhysBoneColliderBase> Colliders { get; set; }
			public int NewLeafBoneCount { get; set; }
			public int SkippedColliderCount { get; set; }
		}

		class PhysBoneChain
		{
			public PhysBoneChain(VRCPhysBoneBase physBone)
			{
				PhysBone = physBone;
				
				var affectedTransforms = new List<Transform>();
				var leafBones = new List<Transform>();
				
				GetAffectedTransforms(physBone.GetRootTransform(), physBone.ignoreTransforms, affectedTransforms, leafBones);

				AffectedTransforms = affectedTransforms;
				LeafBones = leafBones;

				ChainLength = new Lazy<float>((Func<float>)GetChainLength);
			}

			public VRCPhysBoneBase PhysBone { get; }
			public IReadOnlyList<Transform> AffectedTransforms { get; }
			public IReadOnlyList<Transform> LeafBones { get; }

			public Lazy<float> ChainLength { get; }

			private float GetChainLength()
			{
				var root = PhysBone.GetRootTransform();
				var distances = new List<float>();

				foreach (var leaf in LeafBones)
				{
					float distance = 0;

					foreach (var t in WalkParents(leaf)
						.TakeWhile(x => x != root))
					{
						distance += t.localPosition.magnitude;
					}

					distances.Add(distance);
				}

				return distances.Max();
			}

			private static IEnumerable<Transform> WalkParents(Transform leaf)
			{
				if (leaf.parent)
				{
					return WalkParents(leaf.parent)
						.Prepend(leaf);
				}

				return new[] { leaf };
			}

			private static void GetAffectedTransforms(
				Transform root,
				IReadOnlyList<Transform> exclusions,
				IList<Transform> affectedTransforms,
				IList<Transform> leafBones)
			{
				if (!exclusions.Contains(root))
				{
					affectedTransforms.Add(root);

					int oldTransformsCount = affectedTransforms.Count;
					
					foreach (Transform child in root)
						GetAffectedTransforms(child, exclusions, affectedTransforms, leafBones);

					// If nothing was added, add root to leafBones
					if (oldTransformsCount == affectedTransforms.Count)
						leafBones.Add(root);
				}
			}
		}
	}
}