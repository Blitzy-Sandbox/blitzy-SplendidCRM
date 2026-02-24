#region License

/*
 * Copyright 2011-2012 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;

namespace Spring.Social.Facebook.Api
{
	/// <summary>
	/// Model class representing a reference to a group member.
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	// Migration note: [Serializable] applied unconditionally — #if !SILVERLIGHT/#endif removed
	// as part of .NET Framework 4.8 → .NET 10 migration (Silverlight conditional compilation
	// is not applicable on .NET 10; attribute is safe to apply unconditionally).
	[Serializable]
	public class GroupMemberReference : Reference
	{
		public GroupMemberReference()
		{
		}

		public GroupMemberReference(string id, string name, bool administrator)
			: base(id, name)
		{
			this.Administrator = administrator;
		}

		public bool Administrator { get; set; }
	}
}
