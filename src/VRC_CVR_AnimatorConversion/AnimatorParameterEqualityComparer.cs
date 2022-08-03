using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snerble.VRC_CVR_AnimatorConversion.Editor
{
	public class AnimatorParameterEqualityComparer : IEqualityComparer<AnimatorControllerParameter>
	{
		private static readonly AnimatorParameterEqualityComparer _default = new AnimatorParameterEqualityComparer();

		public static AnimatorParameterEqualityComparer Default => _default;

		public bool Equals(AnimatorControllerParameter x, AnimatorControllerParameter y)
		{
			return x.type == y.type &&
				x.name.Equals(y.name, StringComparison.InvariantCulture);
		}

		public int GetHashCode(AnimatorControllerParameter obj)
		{
			int hashCode = -1993617701;
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.name);
			hashCode = hashCode * -1521134295 + EqualityComparer<AnimatorControllerParameterType>.Default.GetHashCode(obj.type);
			return hashCode;
		}
	}
}