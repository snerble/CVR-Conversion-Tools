using UnityEditor;
using UnityEngine;
using ABI.CCK.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Animations;

namespace Snerble.CVR_ContactsSetup.Editor
{
	public class CVR_ContactsSetupEditorWindow : EditorWindow
	{
		const string ContactsContainerName = "Contacts";
		const string CollidersContainerName = "Colliders";
		const float FingerColliderRadiusFallback = 0.02f;
		const float HandColliderHeightFallback = 0.1f;
		//const float PointerRadius = 0.015f;
		const float PointerRadius = 0f;

		static readonly HumanBodyBones[] FootBones = new[]
		{
			HumanBodyBones.LeftFoot,
			HumanBodyBones.RightFoot,
		};

		static readonly HumanBodyBones[] HandBones = new[]
		{
			HumanBodyBones.LeftHand,
			HumanBodyBones.RightHand,
		};

		static readonly HumanBodyBones[] FingerBones = new[]
		{
			HumanBodyBones.LeftThumbDistal,
			HumanBodyBones.LeftIndexDistal,
			HumanBodyBones.LeftMiddleDistal,
			HumanBodyBones.LeftRingDistal,
			HumanBodyBones.LeftLittleDistal,

			HumanBodyBones.RightThumbDistal,
			HumanBodyBones.RightIndexDistal,
			HumanBodyBones.RightMiddleDistal,
			HumanBodyBones.RightRingDistal,
			HumanBodyBones.RightLittleDistal,
		};

		static readonly Dictionary<HumanBodyBones, string[]> ContactBones = new Dictionary<HumanBodyBones, string[]>()
		{
			[HumanBodyBones.LeftHand] = new[]
			{
				"Hand",
				"Hand L",
			},
			[HumanBodyBones.LeftThumbDistal] = new[]
			{
				"Finger",
				"Finger L",
				"Finger Thumb",
				"Finger Thumb L",
			},
			[HumanBodyBones.LeftIndexDistal] = new[]
			{
				"Finger",
				"Finger L",
				"Finger Index",
				"Finger Index L",
			},
			[HumanBodyBones.LeftMiddleDistal] = new[]
			{
				"Finger",
				"Finger L",
				"Finger Middle",
				"Finger Middle L",
			},
			[HumanBodyBones.LeftRingDistal] = new[]
			{
				"Finger",
				"Finger L",
				"Finger Ring",
				"Finger Ring L",
			},
			[HumanBodyBones.LeftLittleDistal] = new[]
			{
				"Finger",
				"Finger L",
				"Finger Pinky",
				"Finger Pinky L",
			},

			[HumanBodyBones.RightHand] = new[]
			{
				"Hand",
				"Hand R",
			},
			[HumanBodyBones.RightThumbDistal] = new[]
			{
				"Finger",
				"Finger R",
				"Finger Thumb",
				"Finger Thumb R",
			},
			[HumanBodyBones.RightIndexDistal] = new[]
			{
				"Finger",
				"Finger R",
				"Finger Index",
				"Finger Index R",
			},
			[HumanBodyBones.RightMiddleDistal] = new[]
			{
				"Finger",
				"Finger R",
				"Finger Middle",
				"Finger Middle R",
			},
			[HumanBodyBones.RightRingDistal] = new[]
			{
				"Finger",
				"Finger R",
				"Finger Ring",
				"Finger Ring R",
			},
			[HumanBodyBones.RightLittleDistal] = new[]
			{
				"Finger",
				"Finger R",
				"Finger Pinky",
				"Finger Pinky R",
			},

			[HumanBodyBones.LeftFoot] = new[]
			{
				"Foot",
				"Foot L",
			},
			[HumanBodyBones.RightFoot] = new[]
			{
				"Foot",
				"Foot R",
			},
		};

		CVRAvatar m_Avatar;
		bool m_PlaceContactsOnAvatarRoot = true;
		bool m_PlaceCollidersOnAvatarRoot = true;

		Animator m_Animator;

		bool m_HandsConfigured;
		bool m_FeetConfigured;

		float m_KnuckleDistance;
		float m_HandWidth;

		bool m_ThumbCollidersConfigured;
		bool m_IndexCollidersConfigured;
		bool m_MiddleCollidersConfigured;
		bool m_RingCollidersConfigured;
		bool m_PinkyCollidersConfigured;
		bool m_WristCollidersConfigured;

		[MenuItem("Tools/CVR Contacts Setup", priority = 1020)]
		public static void Create()
		{
			var window = GetWindow<CVR_ContactsSetupEditorWindow>();
			window.titleContent = new GUIContent("CVR Contacts Setup");
			window.Show();
		}

		void OnGUI()
		{
			_ = HelpText()
				&& AvatarField()
				&& ReadAvatarState()
				&& ContactSenderButtons()
				&& ColliderButtons()
				;
		}

		bool HelpText()
		{
			EditorGUILayout.HelpBox(
				"This utility can automatically configure CVR Pointers and " +
				"Dynamic Bone Colliders in an effort to emulate the features " +
				"from VRChat's Avatar Dynamics.",
				MessageType.Info, true);

			EditorGUILayout.Space();

			return true;
		}

		bool AvatarField()
		{
			m_Avatar = (CVRAvatar)EditorGUILayout.ObjectField(m_Avatar, typeof(CVRAvatar), true);

			if (m_Avatar)
				return m_Animator = m_Avatar.GetComponent<Animator>();

			return m_Avatar;
		}

		bool ReadAvatarState()
		{
			m_HandsConfigured =
				FindSenders(HandBones).Any()
				|| FindSenders(FingerBones).Any();

			m_FeetConfigured = FindSenders(FootBones).Any();
			
			m_ThumbCollidersConfigured = 
				FindFingerColliders(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal).Any()
				|| FindFingerColliders(HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal).Any();

			m_IndexCollidersConfigured = 
				FindFingerColliders(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexDistal).Any()
				|| FindFingerColliders(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexDistal).Any();

			m_MiddleCollidersConfigured = 
				FindFingerColliders(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleDistal).Any()
				|| FindFingerColliders(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleDistal).Any();

			m_RingCollidersConfigured = 
				FindFingerColliders(HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingDistal).Any()
				|| FindFingerColliders(HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingDistal).Any();

			m_PinkyCollidersConfigured = 
				FindFingerColliders(HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleDistal).Any()
				|| FindFingerColliders(HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleDistal).Any();
			
			m_WristCollidersConfigured = 
				FindWristColliders(HumanBodyBones.LeftHand).Any()
				|| FindWristColliders(HumanBodyBones.RightHand).Any();

			{
				var indexKnuckle = m_Animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
				var middleKnuckle = m_Animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);

				m_KnuckleDistance = indexKnuckle && middleKnuckle
					? Vector3.Distance(indexKnuckle.position, middleKnuckle.position)
					: FingerColliderRadiusFallback * 2;
			}

			{
				var indexKnuckle = m_Animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
				var middleKnuckle = m_Animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
				var ringKnuckle = m_Animator.GetBoneTransform(HumanBodyBones.RightRingProximal);
				var pinkyKnuckle = m_Animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);

				Transform lastKnuckle = null;
				if (pinkyKnuckle)
					lastKnuckle = pinkyKnuckle;
				else if (ringKnuckle)
					lastKnuckle = ringKnuckle;
				else if (middleKnuckle)
					lastKnuckle = middleKnuckle;


				m_HandWidth = indexKnuckle && lastKnuckle
					? Vector3.Distance(indexKnuckle.position, lastKnuckle.position) + m_KnuckleDistance
					: HandColliderHeightFallback;
			}

			return true;
		}

		bool ContactSenderButtons()
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Contact Senders (CVR Pointers)");

			m_PlaceContactsOnAvatarRoot = EditorGUILayout.Toggle("Place on avatar root", m_PlaceContactsOnAvatarRoot);

			bool anyConfigured = m_HandsConfigured || m_FeetConfigured;

			if (MultiActionButton(anyConfigured,
				"Remove from all", () => SetupAllSenders(false),
				"Setup all", () => SetupAllSenders(true)))
				return false;

			if (MultiActionButton(m_HandsConfigured,
				"Remove from hands", () => SetupHandSenders(false),
				"Setup hands", () => SetupHandSenders(true)))
				return false;

			if (MultiActionButton(m_FeetConfigured,
				"Remove from feet", () => SetupFootSenders(false),
				"Setup feet", () => SetupFootSenders(true)))
				return false;

			return true;
		}

		#region Contact Sender Setup
		void SetupAllSenders(bool add)
		{
			SetupHandSenders(add);
			SetupFootSenders(add);
		}

		void SetupHandSenders(bool add)
		{
			if (add)
			{
				Array.ForEach(FingerBones, AddSenders);
				Array.ForEach(HandBones, AddSenders);
			}
			else
			{
				FindSenders(FingerBones)
					.Concat(FindSenders(HandBones))
					.ToList()
					.ForEach(DestroyImmediate);
			}
		}

		void SetupFootSenders(bool add)
		{
			if (add)
			{
				Array.ForEach(FootBones, AddSenders);
			}
			else
			{
				FindSenders(FootBones)
					.ToList()
					.ForEach(DestroyImmediate);
			}
		}

		private void AddSenders(HumanBodyBones node)
		{
			var bone = m_Animator.GetBoneTransform(node);

			if (!bone || !ContactBones.TryGetValue(node, out var tags))
				return;
			
			var container = GetContactContainer(bone);
			var boneName = GetBoneName(node);

			var senderObj = new GameObject() { name = $"Ptr_{boneName}" };
			senderObj.transform.SetParent(container);
			senderObj.transform.localScale = Vector3.one;

			foreach (var tag in tags)
			{
				senderObj.AddComponent<CVRPointer>().type = tag;
			}

			Vector3 offset = Vector3.zero;
			if (Array.IndexOf(HandBones, node) != -1)
			{
				// Move two thirds along the hand and a finger thickness down
				offset = new Vector3(0, m_HandWidth * 0.66f, -m_KnuckleDistance / 2);
			}
			else if (Array.IndexOf(FingerBones, node) != -1)
			{
				// Find leaf bone and add offset subtracted by pointer radius
				if (bone.childCount != 0 && bone.GetChild(0) is Transform bone_end)
				{
					var backoff = bone_end.localPosition.normalized * PointerRadius;
					offset = bone_end.localPosition - backoff;
				}
			}

			if (m_PlaceContactsOnAvatarRoot)
			{
				var pConstr = senderObj.AddComponent<ParentConstraint>();
				pConstr.AddSource(new ConstraintSource()
				{
					sourceTransform = bone,
					weight = 1,
				});
				pConstr.translationAtRest = offset;
				pConstr.SetTranslationOffset(0, offset);
				pConstr.locked = true;
				pConstr.constraintActive = true;
			}
			else
			{
				senderObj.transform.localPosition = offset;
				senderObj.transform.localRotation = Quaternion.identity;
			}
		}

		private IEnumerable<GameObject> FindSenders(params HumanBodyBones[] nodes)
		{
			var bones = nodes
				.Select(n => m_Animator.GetBoneTransform(n))
				.Where(t => t)
				.ToList();

			foreach (var bone in bones)
			{
				var container = GetContactContainer(bone, false);

				if (!container)
					continue;

				// All CVR pointers
				var children = container.GetComponentsInChildren<CVRPointer>()
					// Either directly parented to bone or through a parent constraint
					.Where(dc => dc.transform.parent == bone
						|| (dc.GetComponent<ParentConstraint>() is ParentConstraint pc
							&& (bool)pc
							&& pc.sourceCount != 0
							&& pc.GetSource(0).sourceTransform == bone))
					.Select(lac => lac.gameObject)
					.Distinct();

				foreach (var child in children)
					yield return child;
			}
		}

		private Transform GetContactContainer(Transform contactParent, bool createNew = true)
		{
			if (!m_PlaceContactsOnAvatarRoot)
				return contactParent;

			foreach (Transform o in m_Avatar.transform)
				if (o.name.Equals(ContactsContainerName, StringComparison.InvariantCulture))
					return o;

			if (!createNew)
				return null;

			var new_o = new GameObject() { name = ContactsContainerName };
			new_o.transform.SetParent(m_Avatar.transform);

			return new_o.transform;
		}
		#endregion

		bool ColliderButtons()
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Dynamic Bone Colliders");

			m_PlaceCollidersOnAvatarRoot = EditorGUILayout.Toggle("Place on avatar root", m_PlaceCollidersOnAvatarRoot);

			bool anyHandConfigured =
				m_ThumbCollidersConfigured
				|| m_IndexCollidersConfigured
				|| m_MiddleCollidersConfigured
				|| m_RingCollidersConfigured
				|| m_PinkyCollidersConfigured
				|| m_WristCollidersConfigured;

			if (MultiActionButton(anyHandConfigured,
				"Remove from hands", () => SetupHandColliders(false),
				"Setup hands", () => SetupHandColliders(true)))
				return false;

			if (MultiActionButton(m_ThumbCollidersConfigured,
				"Remove from thumbs", () => SetupThumbColliders(false),
				"Setup thumbs", () => SetupThumbColliders(true)))
				return false;

			if (MultiActionButton(m_IndexCollidersConfigured,
				"Remove from index fingers", () => SetupIndexColliders(false),
				"Setup index fingers", () => SetupIndexColliders(true)))
				return false;

			if (MultiActionButton(m_MiddleCollidersConfigured,
				"Remove from middle fingers", () => SetupMiddleColliders(false),
				"Setup middle fingers", () => SetupMiddleColliders(true)))
				return false;

			if (MultiActionButton(m_RingCollidersConfigured,
				"Remove from ring fingers", () => SetupRingColliders(false),
				"Setup ring fingers", () => SetupRingColliders(true)))
				return false;

			if (MultiActionButton(m_PinkyCollidersConfigured,
				"Remove from pinky fingers", () => SetupPinkyColliders(false),
				"Setup pinky fingers", () => SetupPinkyColliders(true)))
				return false;

			if (MultiActionButton(m_WristCollidersConfigured,
				"Remove from wrists", () => SetupWristColliders(false),
				"Setup wrists", () => SetupWristColliders(true)))
				return false;

			return true;
		}

		#region Collider Setup
		void SetupHandColliders(bool add)
		{
			SetupThumbColliders(add);
			SetupIndexColliders(add);
			SetupMiddleColliders(add);
			SetupRingColliders(add);
			SetupPinkyColliders(add);
			SetupWristColliders(add);
		}

		void SetupThumbColliders(bool add)
		{
			SetupFingerCollider(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal, add);
			SetupFingerCollider(HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal, add);
		}
		void SetupIndexColliders(bool add)
		{
			SetupFingerCollider(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexDistal, add);
			SetupFingerCollider(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexDistal, add);
		}
		void SetupMiddleColliders(bool add)
		{
			SetupFingerCollider(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleDistal, add);
			SetupFingerCollider(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleDistal, add);
		}
		void SetupRingColliders(bool add)
		{
			SetupFingerCollider(HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingDistal, add);
			SetupFingerCollider(HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingDistal, add);
		}
		void SetupPinkyColliders(bool add)
		{
			SetupFingerCollider(HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleDistal, add);
			SetupFingerCollider(HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleDistal, add);
		}

		private void SetupFingerCollider(HumanBodyBones proximal, HumanBodyBones distal, bool add)
		{
			if (add)
				AddFingerCollider(proximal, distal);
			else
				RemoveFingerCollider(proximal, distal);

		}
		private void AddFingerCollider(HumanBodyBones proximal, HumanBodyBones distal)
		{
			var pBone = m_Animator.GetBoneTransform(proximal);
			var dBone = m_Animator.GetBoneTransform(distal);

			if (!pBone || !dBone)
				return;

			// Use end bone local position length to correct the finger length
			var dBone_end = dBone.childCount != 0
				? dBone.GetChild(0)
				: dBone;
			float fingerLengthCorrection = dBone_end.localPosition.magnitude - m_KnuckleDistance / 2;

			string fingerName = GetBoneName(proximal) + "Finger";
			var container = GetColliderContainer(dBone);
			var fingerLength = Vector3.Distance(pBone.position, dBone.position);

			var offset = new Vector3(0, fingerLengthCorrection);

			var colliderObj = new GameObject() { name = $"Col_{fingerName}" };
			colliderObj.transform.SetParent(container);
			colliderObj.transform.localScale = Vector3.one;

			var collider = colliderObj.AddComponent<DynamicBoneCollider>();
			collider.m_Direction = DynamicBoneColliderBase.Direction.Y;
			collider.m_Radius = m_KnuckleDistance / 2;
			collider.m_Height = fingerLength + m_KnuckleDistance + fingerLengthCorrection;
			collider.m_Center = new Vector3(0, -(fingerLength + fingerLengthCorrection) / 2);

			if (m_PlaceCollidersOnAvatarRoot)
			{
				var pConstr = colliderObj.AddComponent<ParentConstraint>();
				pConstr.AddSource(new ConstraintSource()
				{
					sourceTransform = dBone,
					weight = 1,
				});
				pConstr.translationAtRest = offset;
				pConstr.SetTranslationOffset(0, offset);
				pConstr.locked = true;
				pConstr.constraintActive = true;
			}
			else
			{
				colliderObj.transform.localPosition = offset;
				colliderObj.transform.localRotation = Quaternion.identity;
			}

			var lookAtConstr = colliderObj.AddComponent<LookAtConstraint>();
			lookAtConstr.AddSource(new ConstraintSource()
			{
				sourceTransform = pBone,
				weight = 1,
			});
			lookAtConstr.rotationOffset = new Vector3(-90, 0);
			lookAtConstr.locked = true;
			lookAtConstr.constraintActive = true;
		}
		private void RemoveFingerCollider(HumanBodyBones proximal, HumanBodyBones distal)
		{
			FindFingerColliders(proximal, distal)
				.ToList()
				.ForEach(DestroyImmediate);
		}
		private IEnumerable<GameObject> FindFingerColliders(HumanBodyBones proximal, HumanBodyBones distal)
		{
			var pBone = m_Animator.GetBoneTransform(proximal);
			var dBone = m_Animator.GetBoneTransform(distal);

			if (!pBone || !dBone)
				return Array.Empty<GameObject>();

			var container = GetColliderContainer(m_Animator.GetBoneTransform(HumanBodyBones.Hips), false);

			if (!container)
				return Array.Empty<GameObject>();

			// All dynamic bone colliders
			return container.GetComponentsInChildren<DynamicBoneCollider>()
				// Either directly parented to dBone or through a parent constraint
				.Where(dc => dc.transform.parent == dBone
					|| (dc.GetComponent<ParentConstraint>() is ParentConstraint pc
						&& (bool)pc
						&& pc.sourceCount != 0
						&& pc.GetSource(0).sourceTransform == dBone))
				// With a look at constraint aiming at the proximal
				.Select(dc => dc.GetComponent<LookAtConstraint>())
				.Where(lac => lac && lac.GetSource(0).sourceTransform == pBone)
				.Select(lac => lac.gameObject);
		}

		private Transform GetColliderContainer(Transform colliderParent, bool createNew = true)
		{
			if (!m_PlaceCollidersOnAvatarRoot)
				return colliderParent;
			
			foreach (Transform o in m_Avatar.transform)
				if (o.name.Equals(CollidersContainerName, StringComparison.InvariantCulture))
					return o;

			if (!createNew)
				return null;

			var new_o = new GameObject() { name = CollidersContainerName };
			new_o.transform.SetParent(m_Avatar.transform);

			return new_o.transform;
		}

		void SetupWristColliders(bool add)
		{
			SetupWristCollider(add, HumanBodyBones.LeftHand);
			SetupWristCollider(add, HumanBodyBones.RightHand);
		}

		void SetupWristCollider(bool add, HumanBodyBones hand)
		{
			if (add)
			{
				var wrist = m_Animator.GetBoneTransform(hand);
				var index = m_Animator.GetBoneTransform(hand);

				if (!wrist)
					return;

				var container = GetColliderContainer(wrist);

				var colliderObj = new GameObject() { name = $"Col_{hand}" };
				colliderObj.transform.SetParent(container);
				colliderObj.transform.localScale = Vector3.one;

				var collider = colliderObj.AddComponent<DynamicBoneCollider>();
				collider.m_Direction = DynamicBoneColliderBase.Direction.X;
				collider.m_Center = new Vector3(0, m_HandWidth / 2);
				collider.m_Radius = m_KnuckleDistance;
				collider.m_Height = m_HandWidth;

				if (m_PlaceCollidersOnAvatarRoot)
				{
					var pConstr = colliderObj.AddComponent<ParentConstraint>();
					pConstr.AddSource(new ConstraintSource()
					{
						sourceTransform = wrist,
						weight = 1,
					});
					//pConstr.translationAtRest = m_PalmPosition;
					pConstr.locked = true;
					pConstr.constraintActive = true;
				}
				else
				{
					colliderObj.transform.localPosition = Vector3.zero;
					colliderObj.transform.localRotation = Quaternion.identity;
				}
			}
			else
			{
				FindWristColliders(hand)
					.ToList()
					.ForEach(DestroyImmediate);
			}
		}

		private IEnumerable<GameObject> FindWristColliders(HumanBodyBones hand)
		{
			var wrist = m_Animator.GetBoneTransform(hand);
			var container = GetColliderContainer(m_Animator.GetBoneTransform(HumanBodyBones.Hips), false);

			if (!wrist || !container)
				return Array.Empty<GameObject>();

			// All dynamic bone colliders
			return container.GetComponentsInChildren<DynamicBoneCollider>()
				// Either directly parented the wrist or through a parent constraint
				.Where(dc => dc.transform.parent == wrist
					|| (dc.GetComponent<ParentConstraint>() is ParentConstraint pc
						&& (bool)pc
						&& pc.sourceCount != 0
						&& pc.GetSource(0).sourceTransform == wrist))
				.Select(lac => lac.gameObject);
		}
		#endregion

		private static bool MultiActionButton(
			bool state,
			string trueContent,
			Action trueAction,
			string falseContent,
			Action falseAction)
		{
			if (state && GUILayout.Button(trueContent))
			{
				trueAction();
				return true;
			}
			else if (!state && GUILayout.Button(falseContent))
			{
				falseAction();
				return true;
			}

			return false;
		}

		private static string GetBoneName(HumanBodyBones bone)
		{
			return bone.ToString()
				.Replace("Proximal", string.Empty)
				.Replace("Intermediate", string.Empty)
				.Replace("Distal", string.Empty)
				.Replace("Little", "Pinky");
		}
	}
}
