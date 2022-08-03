using Snerble.CVR_ContactsSetup.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Snerble.VRC_CVR_AnimatorConversion.Editor
{
	public partial class VRC_CVR_AnimatorConversionEditorWindow : EditorWindow
	{
		const string CVRAnimatorNameFormat = "{0}_CVR";
		const float FloatConditionMargin = 0.01f;
		const string ParameterRenameFormat = "_{0}";

		static readonly int[] VRC_CVR_GestureNumberMapping = new int[]
		{
			0, // Idle
			1, // Fist
			-1, // Open hand
			4, // Point
			5, // Peace
			6, // Rock 'n roll
			3, // Finger gun
			2, // Thumbs up
		};

		[MenuItem("Tools/Convert VRC Animator to CVR", priority = 1020)]
		public static void Create()
		{
			var window = GetWindow<VRC_CVR_AnimatorConversionEditorWindow>();
			window.titleContent = new GUIContent("Animator Converter");
			window.Show();
		}

		public AnimatorController CVRAvatarAnimator;

		RuntimeAnimatorController m_RuntimeAnimatorController;
		AnimatorController m_AnimatorController;

		bool m_IsOverrideControllerSelected;
		bool m_IsMergedWithAvatarAnimator;

		string m_ConvertedControllerPath;
		string m_ConvertedOverrideControllerPath;
		string[] m_NewAssets;
		string[] m_OverwrittenAssets;

		bool m_MergeWithAvatarAnimator = true;
		bool m_PreserveOverrides = true;
		bool m_PreserveOverwrittenOverrides = true;
		
		void OnGUI()
		{
			_ = ValidateConstants() &&
				HelpText() && 
				AnimatorControllerField() &&
				ValidateAnimatorController() &&
				PrepareConversion() &&
				ConversionSettings() &&
				ConvertButton();
		}

		bool ValidateConstants([CallerFilePath] string file = null)
		{
			var scriptFileName = Path.GetFileName(file);

			var errors = new List<string>();

			if (!CVRAvatarAnimator)
			{
				errors.Add(
					$"Missing {nameof(CVRAvatarAnimator)}. Please assign it in " +
					$"the import settings for {scriptFileName}");
			}

			foreach (var error in errors)
			{
				EditorGUILayout.HelpBox(error, MessageType.Error, true);
			}

			return errors.Count == 0;
		}

		bool HelpText()
		{
			EditorGUILayout.HelpBox(
				"This utility can convert VRChat animators to work in ChilloutVR. " +
				"The gesture parameters are automatically converted to floats and " +
				"all transitions are adjusted to preseve their original behavior.",
				MessageType.Info, true);

			EditorGUILayout.Space();

			return true;
		}

		bool AnimatorControllerField()
		{
			m_RuntimeAnimatorController = EditorGUILayout.ObjectField(
				"Controller",
				m_RuntimeAnimatorController,
				typeof(RuntimeAnimatorController),
				false) as RuntimeAnimatorController;

			return m_RuntimeAnimatorController;
		}

		bool ValidateAnimatorController()
		{
			m_AnimatorController = GetAnimatorController(m_RuntimeAnimatorController);

			if (!m_AnimatorController)
			{
				if (m_RuntimeAnimatorController is AnimatorOverrideController overrideController
					&& !overrideController.runtimeAnimatorController)
				{
					EditorGUILayout.HelpBox(
						$"Selected {nameof(AnimatorOverrideController)} has no controller assigned.",
						MessageType.Error, true);
				}
				else
				{
					EditorGUILayout.HelpBox(
						$"Unable to resolve animator controller. Please make sure either " +
						$"an {nameof(AnimatorOverrideController)} or {nameof(AnimatorController)} " +
						$"is selected.",
						MessageType.Error, true);
				}

				return false;
			}

			return true;
		}

		bool PrepareConversion()
		{
			m_IsOverrideControllerSelected = m_RuntimeAnimatorController is AnimatorOverrideController;

			#region Is merged with AvatarAnimator
			{
				var layersMatch = CVRAvatarAnimator.layers
					.All(l => m_AnimatorController.layers
						.Any(l1 => l.name.Equals(l1.name, StringComparison.InvariantCulture)));

				var paramsMatch = !CVRAvatarAnimator.parameters
					.Except(m_AnimatorController.parameters, AnimatorParameterEqualityComparer.Default)
					.Any();

				m_IsMergedWithAvatarAnimator = layersMatch && paramsMatch;
			}
			#endregion

			#region Asset paths
			{
				var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_RuntimeAnimatorController));

				m_ConvertedOverrideControllerPath = AssetDatabase.GetAssetPath(m_RuntimeAnimatorController);
				m_ConvertedOverrideControllerPath = Path.Combine(
					directory,
					string.Format(CVRAnimatorNameFormat, Path.GetFileNameWithoutExtension(m_ConvertedOverrideControllerPath))
					+ Path.GetExtension(m_ConvertedOverrideControllerPath));

				m_ConvertedControllerPath = AssetDatabase.GetAssetPath(m_AnimatorController);
				m_ConvertedControllerPath = Path.Combine(
					directory,
					string.Format(CVRAnimatorNameFormat, Path.GetFileNameWithoutExtension(m_ConvertedControllerPath))
					+ Path.GetExtension(m_ConvertedControllerPath));
			}
			#endregion

			var assets = new[]
			{
				m_ConvertedControllerPath,
				m_ConvertedOverrideControllerPath,
			};

			m_OverwrittenAssets = assets
				.Distinct()
				.Where(File.Exists)
				.ToArray();

			m_NewAssets = assets
				.Except(m_OverwrittenAssets)
				.ToArray();

			return true;
		}

		bool ConversionSettings()
		{
			if (m_IsMergedWithAvatarAnimator)
			{
				EditorGUILayout.HelpBox(
					"The selected controller is already set up for ChilloutVR",
					MessageType.None, true);

				return false;
			}

			if (m_IsOverrideControllerSelected)
			{
				m_PreserveOverrides = EditorGUILayout.Toggle(
					new GUIContent(
						"Copy overrides",
						"Copy the overrides from the original override controller to the new one"),
					m_PreserveOverrides);
			}

			_ = AssetChangeInfo();

			if (m_IsOverrideControllerSelected &&
				Array.IndexOf(m_OverwrittenAssets, m_ConvertedOverrideControllerPath) != -1)
			{
				m_PreserveOverwrittenOverrides = EditorGUILayout.Toggle(
					new GUIContent(
						"Keep overrides",
						$"Preserve the overrides from the overwritten {nameof(AnimatorOverrideController)}"),
					m_PreserveOverrides);
			}

			return true;
		}

		bool AssetChangeInfo()
		{
			EditorGUILayout.Space();

			if (m_NewAssets.Length != 0)
			{
				EditorGUILayout.HelpBox(
					$"The following assets will be created:\n - {string.Join("\n - ", m_NewAssets)}",
					MessageType.None, true);
			}
			
			if (m_OverwrittenAssets.Length != 0)
			{
				EditorGUILayout.HelpBox(
					$"The following assets will be overwritten:\n - {string.Join("\n - ", m_OverwrittenAssets)}",
					MessageType.Warning, true);
			}

			return true;
		}

		bool ConvertButton()
		{
			if (GUILayout.Button("Convert"))
			{
				ConvertController();
				return false;
			}

			return true;
		}

		void ConvertController()
		{
			if (AssetDatabase.LoadAssetAtPath<AnimatorController>(m_ConvertedControllerPath))
			{
				AssetDatabase.DeleteAsset(m_ConvertedControllerPath);
				AssetDatabase.Refresh();
			}

			FileUtil.CopyFileOrDirectory(AssetDatabase.GetAssetPath(m_AnimatorController), m_ConvertedControllerPath);

			AssetDatabase.Refresh();

			// Clone the original
			var newController = AssetDatabase.LoadAssetAtPath<AnimatorController>(m_ConvertedControllerPath);

			if (!newController)
			{
				Debug.LogError("Failed to create animator controller");
				return;
			}

			var conversions = GetParameterConversions(newController);

			newController.parameters = conversions
				.Select(c => ConvertParameter(c.Parameter, c.NewType))
				.Concat(CVRAvatarAnimator.parameters
					.Where(p => !conversions.Any(c => p.name.InvariantEquals(c.Name))))
				.Concat(newController.parameters
					.Where(p => !conversions.Any(c => p.name.InvariantEquals(c.Name))))
				.ToArray();

			#region AnyState transition conversion
			foreach (var stateMachine in newController.layers
					.SelectMany(x => FindStateMachines(x.stateMachine)))
			{
				var newTransitions = stateMachine.anyStateTransitions
					.SelectMany(x => ConvertTransition(() => stateMachine.AddAnyStateTransition(x.destinationState), x, conversions))
					.ToArray();

				stateMachine.anyStateTransitions = newTransitions;
			}
			#endregion

			#region State transition conversion
			foreach (var state in newController.layers
				.SelectMany(x => FindStates(x.stateMachine)))
			{
				var newTransitions = state.transitions
					.SelectMany(x => ConvertTransition(() => state.AddTransition(x.destinationState), x, conversions))
					.ToArray();

				state.transitions = newTransitions;
			}
			#endregion

			newController.layers = CVRAvatarAnimator.layers
				.Concat(newController.layers)
				.ToArray();

			if (m_IsOverrideControllerSelected)
				ConvertOverrideController(newController);

			AssetDatabase.SaveAssets();
		}

		void ConvertOverrideController(AnimatorController newController)
		{
			var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

			if (m_PreserveOverrides)
			{
				var overrideController = m_RuntimeAnimatorController as AnimatorOverrideController;
				overrideController.GetOverrides(overrides);
			}

			if (m_PreserveOverwrittenOverrides &&
				AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(m_ConvertedOverrideControllerPath)
				is AnimatorOverrideController oldOverrideController)
			{
				// Preserve overrides from old override controller
				oldOverrideController.GetOverrides(overrides);
				AssetDatabase.DeleteAsset(m_ConvertedOverrideControllerPath);
				AssetDatabase.Refresh();
			}

			var newOverrideController = new AnimatorOverrideController(newController);
			newOverrideController.ApplyOverrides(overrides);

			AssetDatabase.CreateAsset(newController, m_ConvertedOverrideControllerPath);
		}

		private static IEnumerable<AnimatorState> FindStates(AnimatorStateMachine stateMachine)
		{
			return stateMachine.states
				.Select(x => x.state)
				.Concat(stateMachine.stateMachines
					.SelectMany(x => FindStates(x.stateMachine)));
		}
		
		private static IEnumerable<AnimatorStateMachine> FindStateMachines(AnimatorStateMachine stateMachine)
		{
			return stateMachine.stateMachines
				.SelectMany(x => FindStateMachines(x.stateMachine))
				.Prepend(stateMachine);
		}

		private IReadOnlyList<ParameterConversion> GetParameterConversions(AnimatorController controller)
		{
			return controller.parameters
				.Where(p => CVRAvatarAnimator.parameters
					.Where(p1 => !AnimatorParameterEqualityComparer.Default.Equals(p, p1))
					.Where(p1 => p.name.Equals(p1.name, StringComparison.InvariantCulture))
					.Any())
				.Select(p => new ParameterConversion
				{
					Parameter = p,
					Name = p.name,
					OldType = p.type,
					NewType = CVRAvatarAnimator.parameters
						.Single(p1 => p.name.Equals(p1.name, StringComparison.InvariantCulture))
						.type,
				})
				.ToList();
		}

		private static AnimatorControllerParameter ConvertParameter(AnimatorControllerParameter parameter, AnimatorControllerParameterType newType)
		{
			// Rename
			if (newType == AnimatorControllerParameterType.Trigger ||
				(parameter.type != AnimatorControllerParameterType.Trigger &&
				newType == AnimatorControllerParameterType.Bool))
			{
				parameter.name = string.Format(ParameterRenameFormat, parameter.name);
			}

			parameter.type = newType;

			return parameter;
		}

		private static IEnumerable<AnimatorStateTransition> ConvertTransition(
			Func<AnimatorStateTransition> transitionFactory,
			AnimatorStateTransition transition,
			IReadOnlyList<ParameterConversion> conversions)
		{
			if (!conversions.Any())
			{
				yield return transition;
				yield break;
			}

			IReadOnlyList<IReadOnlyList<AnimatorCondition>> conditionSets = new[]
			{
				transition.conditions
			};

			foreach (var conversion in conversions)
			{
				conditionSets = conditionSets
					.SelectMany(x => ConvertConditions(transition, x, conversion))
					.ToList();
			}

			if (conditionSets.Count == 1)
			{
				transition.conditions = conditionSets.Single().ToArray();
				yield return transition;
				yield break;
			}

			foreach (var conditionSet in conditionSets)
			{
				var newTransition = transitionFactory();
				MemberwiseCopy(transition, newTransition);
				newTransition.conditions = conditionSet.ToArray();
				yield return newTransition;
			}
		}

		private static IEnumerable<IReadOnlyList<AnimatorCondition>> ConvertConditions(
			AnimatorStateTransition transition,
			IReadOnlyList<AnimatorCondition> conditions,
			ParameterConversion conversion)
		{
			if (conversion.OldType == conversion.NewType || conditions.Count == 0)
			{
				yield return conditions;
				yield break;
			}

			switch (conversion.OldType)
			{
				#region Float
				case AnimatorControllerParameterType.Float when conversion.NewType == AnimatorControllerParameterType.Int:
					int? lowestValueRounded = null;
					int? highestValueRounded = null;

					var greaterThanChecks = conditions
						.FilterByName(conversion.Name)
						.Where(x => x.mode == AnimatorConditionMode.Greater)
						.ToList();

					var lessThanChecks = conditions
						.FilterByName(conversion.Name)
						.Where(x => x.mode == AnimatorConditionMode.Less)
						.ToList();

					if (greaterThanChecks.Any())
						lowestValueRounded = Mathf.RoundToInt(greaterThanChecks.Average(x => x.threshold));

					if (lessThanChecks.Any())
						lowestValueRounded = Mathf.RoundToInt(greaterThanChecks.Average(x => x.threshold));

					if (!lowestValueRounded.HasValue && !highestValueRounded.HasValue)
					{
						yield return conditions;
						yield break;
					}

					if (lowestValueRounded == highestValueRounded)
					{
						yield return conditions
							.FilterByName(conversion.Name, false)
							.Append(new AnimatorCondition
							{
								mode = AnimatorConditionMode.Equals,
								parameter = conversion.Name,
								threshold = lowestValueRounded.Value,
							})
							.ToList();
					}

					var newConditions = conditions.AsEnumerable();

					if (lowestValueRounded.HasValue)
					{
						newConditions = newConditions
							.Append(new AnimatorCondition
							{
								mode = AnimatorConditionMode.Less,
								parameter = conversion.Name,
								threshold = lowestValueRounded.Value,
							});
					}

					if (highestValueRounded.HasValue)
					{
						newConditions = newConditions
							.Append(new AnimatorCondition
							{
								mode = AnimatorConditionMode.Greater,
								parameter = conversion.Name,
								threshold = highestValueRounded.Value,
							});
					}

					yield return newConditions.ToList();
					yield break;
				#endregion

				#region Int
				case AnimatorControllerParameterType.Int when conversion.NewType == AnimatorControllerParameterType.Float:
					bool compensateGestureWeight =
						conversion.Name.InvariantEquals("GestureLeft") ||
						conversion.Name.InvariantEquals("GestureRight");

					var newEqualityChecks = conditions
						.Where(x => x.mode == AnimatorConditionMode.Equals)
						.FilterByName(conversion.Name)
						.SelectMany(x =>
						{
							// Remap
							int threshold = VRC_CVR_GestureNumberMapping.ElementAtOrDefault((int)x.threshold);

							bool compensate = compensateGestureWeight && threshold == 1;

							if (transition.destinationState &&
								(transition.destinationState.timeParameter.InvariantEquals("GestureLeftWeight") ||
								transition.destinationState.timeParameter.InvariantEquals("GestureRightWeight")))
							{
								if (compensate)
								{
									transition.destinationState.timeParameterActive = true;
									transition.destinationState.timeParameter = x.parameter;
								}
								else
								{
									transition.destinationState.timeParameterActive = false;
									transition.destinationState.timeParameter = null;
								}
							}

							// Rename parameter of the state's blend tree
							if (transition.destinationState.motion is BlendTree blendTree &&
								(blendTree.blendParameter.InvariantEquals("GestureLeftWeight") ||
								blendTree.blendParameter.InvariantEquals("GestureRightWeight")))
							{
								blendTree.blendParameter = x.parameter;
							}

							return new[]
							{
								new AnimatorCondition
								{
									mode = AnimatorConditionMode.Greater,
									parameter = x.parameter,
									threshold = threshold - FloatConditionMargin - (compensate ? 1 : 0),
								},
								new AnimatorCondition
								{
									mode = AnimatorConditionMode.Less,
									parameter = x.parameter,
									threshold = threshold + FloatConditionMargin,
								},
							};
						})
						.ToList();

					var newInequalityChecks = conditions
						.Where(x => x.mode == AnimatorConditionMode.NotEqual)
						.FilterByName(conversion.Name)
						.SelectMany(x =>
						{
							return new[]
							{
								new AnimatorCondition
								{
									mode = AnimatorConditionMode.Greater,
									parameter = x.parameter,
									threshold = x.threshold + FloatConditionMargin,
								},
								new AnimatorCondition
								{
									mode = AnimatorConditionMode.Less,
									parameter = x.parameter,
									threshold = x.threshold - FloatConditionMargin,
								},
							};
						})
						.ToList();

					var oldConditions = conditions
						// All conditions for different parameters or ones that dont check for equality (latter ones can be reused)
						.Where(x => !x.parameter.Equals(conversion.Name, StringComparison.InvariantCulture) ||
							(x.mode != AnimatorConditionMode.Equals &&
							x.mode != AnimatorConditionMode.NotEqual))
						
						// Apply margin for < and > checks
						.Select(conversion.Name, x =>
						{
							if (x.mode == AnimatorConditionMode.Less)
								x.threshold -= FloatConditionMargin;
							else if (x.mode == AnimatorConditionMode.Greater)
								x.threshold += FloatConditionMargin;

							return x;
						})
						.ToList();

					if (newEqualityChecks.Count != 0)
					{
						yield return oldConditions
							.Concat(newEqualityChecks)
							.ToList();
					}
					
					if (newInequalityChecks.Count != 0)
					{
						yield return oldConditions
							.Concat(newInequalityChecks.Where(x => x.mode == AnimatorConditionMode.Less))
							.ToList();
						yield return oldConditions
							.Concat(newInequalityChecks.Where(x => x.mode == AnimatorConditionMode.Greater))
							.ToList();
					}

					if (newEqualityChecks.Count == 0 &&
						newInequalityChecks.Count == 0)
					{
						yield return oldConditions;
					}

					yield break;
				#endregion

				#region Bool
				case AnimatorControllerParameterType.Bool:
					switch (conversion.NewType)
					{
						case AnimatorControllerParameterType.Float:
							yield return conditions.Select(conversion.Name, x =>
							{
								x.mode = AnimatorConditionMode.Greater;
								x.threshold = FloatConditionMargin;
								return x;
							}).ToList();
							yield break;

						case AnimatorControllerParameterType.Int:
							yield return conditions.Select(conversion.Name, x =>
							{
								x.mode = AnimatorConditionMode.Greater;
								x.threshold = 0;
								return x;
							}).ToList();
							yield break;
					}
					break;
				#endregion

				#region Trigger
				case AnimatorControllerParameterType.Trigger:
					switch (conversion.NewType)
					{
						case AnimatorControllerParameterType.Float:
						case AnimatorControllerParameterType.Int:
							yield return conditions.Select(conversion.Name, x =>
							{
								x.mode = AnimatorConditionMode.Greater;
								x.threshold = 0;
								return x;
							}).ToList();
							yield break;

						case AnimatorControllerParameterType.Bool:
							yield return conditions.Select(conversion.Name, x =>
							{
								x.mode = AnimatorConditionMode.If;
								return x;
							}).ToList();
							yield break;
					}
					break;
				#endregion
			}

			// Rename parameter
			yield return conditions.Select(conversion.Name, x =>
			{
				x.parameter = string.Format(ParameterRenameFormat, conversion.Name);
				return x;
			}).ToList();
		}

		private static AnimatorController GetAnimatorController(RuntimeAnimatorController controller)
		{
			switch (controller)
			{
				case AnimatorOverrideController overrideController:
					return overrideController.runtimeAnimatorController as AnimatorController;

				case AnimatorController animatorController:
					return animatorController;

				default:
					return null;
			}
		}

		private static void MemberwiseCopy<T>(T source, T target)
		{
			foreach (var prop in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
				.Where(p => p.CanWrite))
			{
				prop.SetValue(target, prop.GetValue(source));
			}
			foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
			{
				field.SetValue(target, field.GetValue(source));
			}
		}

		struct ParameterConversion
		{
			public AnimatorControllerParameter Parameter { get; set; }
			public string Name { get; set; }
			public AnimatorControllerParameterType OldType { get; set; }
			public AnimatorControllerParameterType NewType { get; set; }
		}
	}
}