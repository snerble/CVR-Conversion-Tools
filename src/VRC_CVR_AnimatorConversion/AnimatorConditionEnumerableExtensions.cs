using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEditorInternal;

namespace Snerble.VRC_CVR_AnimatorConversion.Editor
{
	public static class AnimatorConditionEnumerableExtensions
	{
		public static IEnumerable<AnimatorCondition> FilterByName(
			this IEnumerable<AnimatorCondition> e,
			string parameterName,
			bool matches = true)
		{
			return e.Where(x => x.parameter.InvariantEquals(parameterName) == matches);
		}

		public static IEnumerable<AnimatorCondition> Select(
			this IEnumerable<AnimatorCondition> e,
			string parameterName,
			Func<AnimatorCondition, AnimatorCondition> action)
		{
			return e
				.Select(x =>
				{
					if (x.parameter.InvariantEquals(parameterName))
						return action(x);
					return x;
				});
		}

		public static IEnumerable<AnimatorCondition> SelectMany(
			this IEnumerable<AnimatorCondition> e,
			string parameterName,
			Func<AnimatorCondition, IEnumerable<AnimatorCondition>> action)
		{
			return e
				.SelectMany(x =>
				{
					if (x.parameter.InvariantEquals(parameterName))
						return action(x);
					return new[] { x };
				});
		}

		public static bool InvariantEquals(this string s1, string s2)
		{
			return s1.Equals(s2, StringComparison.InvariantCulture);
		}
	}
}