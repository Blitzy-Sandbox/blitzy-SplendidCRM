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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/QuestionTemplate.cs
// .NET Framework 4.8 → .NET 10 ASP.NET Core migration (AAP Goal 5 / Section 0.7.4)
// REMOVED: using Spring.Http;        — Spring.NET library discontinued, no .NET 10 NuGet equivalent
// REMOVED: using Spring.Rest.Client; — Spring.NET library discontinued, no .NET 10 NuGet equivalent
// KEPT:    all System.* using directives — standard library, fully compatible with .NET 10
// RestTemplate stub is defined in AbstractFacebookOperations.cs (single definition point for Impl/ namespace).
// Question and QuestionOption types are resolved via C# outer-namespace lookup (Spring.Social.Facebook.Api).
// This file is a dormant Enterprise Edition stub. MUST compile on .NET 10, NOT expected to execute.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Spring.Social.Facebook.Api.Impl
{
	/// <summary>
	/// Implementation of <see cref="IQuestionOperations"/> providing question-oriented CRUD operations
	/// against the Facebook Graph API. Supports publishing questions, adding options, listing questions
	/// and options for a given user, retrieving individual questions/options, and deleting them.
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class QuestionTemplate : AbstractFacebookOperations, IQuestionOperations
	{
		/// <summary>
		/// Initializes a new instance of <see cref="QuestionTemplate"/>.
		/// </summary>
		/// <param name="applicationNamespace">The Facebook application namespace.</param>
		/// <param name="restTemplate">The REST template stub used for Graph API calls.</param>
		/// <param name="isAuthorized">Whether the API binding was created with an access token.</param>
		public QuestionTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized)
			: base(applicationNamespace, restTemplate, isAuthorized)
		{
		}

		#region IQuestionOperations Members

		/// <summary>
		/// Publishes a question to the authenticated user's feed.
		/// Requires "publish_stream" permission.
		/// </summary>
		/// <param name="questionText">The text of the question to publish.</param>
		/// <returns>The ID of the newly created question.</returns>
		public string AskQuestion(string questionText)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("question", questionText);
			return this.Publish("me", "questions", parameters);
		}

		/// <summary>
		/// Adds an option to an existing question.
		/// Requires "publish_stream" permission.
		/// </summary>
		/// <param name="questionId">The ID of the question to add the option to.</param>
		/// <param name="optionText">The text of the option to add.</param>
		/// <returns>The ID of the newly created option.</returns>
		public string AddOption(string questionId, string optionText)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("option", optionText);
			return this.Publish(questionId, "options", parameters);
		}

		/// <summary>
		/// Retrieves all questions asked by the authenticated user.
		/// Requires "user_questions" permission.
		/// </summary>
		/// <returns>A list of <see cref="Question"/> objects asked by the current user.</returns>
		public List<Question> GetQuestions()
		{
			return GetQuestions("me");
		}

		/// <summary>
		/// Retrieves all questions asked by the specified user.
		/// Requires "user_questions" permission (own questions) or "friends_questions" (friend questions).
		/// </summary>
		/// <param name="userId">The ID of the user whose questions to retrieve, or "me" for the authenticated user.</param>
		/// <returns>A list of <see cref="Question"/> objects asked by the specified user.</returns>
		public List<Question> GetQuestions(string userId)
		{
			requireAuthorization();
			return this.FetchConnections<Question>(userId, "questions");
		}

		/// <summary>
		/// Deletes a question.
		/// Requires "publish_stream" permission.
		/// </summary>
		/// <param name="questionId">The ID of the question to delete.</param>
		public void DeleteQuestion(string questionId)
		{
			requireAuthorization();
			this.Delete(questionId);
		}

		/// <summary>
		/// Retrieves a single question by its ID.
		/// Requires "user_questions" permission (own question) or "friends_questions" (friend question).
		/// </summary>
		/// <param name="questionId">The ID of the question to retrieve.</param>
		/// <returns>The <see cref="Question"/> with the specified ID.</returns>
		public Question GetQuestion(string questionId)
		{
			requireAuthorization();
			return this.FetchObject<Question>(questionId);
		}

		/// <summary>
		/// Retrieves a single question option by its ID.
		/// Requires "user_questions" permission (own option) or "friends_questions" (friend option).
		/// </summary>
		/// <param name="optionId">The ID of the option to retrieve.</param>
		/// <returns>The <see cref="QuestionOption"/> with the specified ID.</returns>
		public QuestionOption GetOption(string optionId)
		{
			requireAuthorization();
			return this.FetchObject<QuestionOption>(optionId);
		}

		/// <summary>
		/// Retrieves all options for a specified question.
		/// Requires "user_questions" permission (own question) or "friends_questions" (friend question).
		/// </summary>
		/// <param name="questionId">The ID of the question whose options to retrieve.</param>
		/// <returns>A list of <see cref="QuestionOption"/> objects for the specified question.</returns>
		public List<QuestionOption> GetOptions(string questionId)
		{
			requireAuthorization();
			return this.FetchConnections<QuestionOption>(questionId, "options");
		}

		/// <summary>
		/// Deletes a question option.
		/// Requires "publish_stream" permission.
		/// </summary>
		/// <param name="optionId">The ID of the option to delete.</param>
		public void DeleteOption(string optionId)
		{
			requireAuthorization();
			this.Delete(optionId);
		}

		#endregion
	}
}
