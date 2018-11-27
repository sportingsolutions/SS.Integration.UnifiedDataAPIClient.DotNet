using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SportingSolutions.Udapi.Sdk.Extensions
{
	public static class StringHelper
	{
		public static string ExtractIdFromRoutingKey(this string key)
		{
			var words = key.Split('.');
			if (words.Length == 4)
			{
				return words[2];
			}

			return null;
		}
	}
}
