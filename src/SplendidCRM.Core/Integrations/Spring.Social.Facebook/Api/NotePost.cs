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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/NotePost.cs
// Changes: Removed #if !SILVERLIGHT / #endif preprocessor directives around [Serializable];
//          [Serializable] is now applied unconditionally for .NET 10 compatibility.
//          This is a dormant integration stub — compile-only, not activated at runtime.

#nullable disable

using System;

namespace Spring.Social.Facebook.Api
{
	/// <summary>
	/// Model class representing a note Post to a user's wall.
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	[Serializable]
	public class NotePost : Post
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NotePost"/> class.
		/// </summary>
		public NotePost()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NotePost"/> class
		/// with the specified ID, author reference, and timestamps.
		/// </summary>
		/// <param name="id">The Facebook object ID for this note post.</param>
		/// <param name="from">The <see cref="Reference"/> representing the author of this note post.</param>
		/// <param name="createdTime">The date and time when this note post was created.</param>
		/// <param name="updatedTime">The date and time when this note post was last updated.</param>
		public NotePost(string id, Reference from, DateTime createdTime, DateTime updatedTime)
			: base(id, from, createdTime, updatedTime)
		{
		}

		/// <summary>
		/// Gets or sets the subject (title) of the note post.
		/// </summary>
		public string Subject { get; set; }
	}
}
