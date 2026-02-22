/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>. 
 * 
 * You can contact SplendidCRM Software, Inc. at email address support@splendidcrm.com. 
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2011 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/
using System;
using System.IO;
using System.Data;
// Migration: Removed 'using System.Web;' — not available in .NET 10 ASP.NET Core and not referenced in this file.

namespace SplendidCRM
{
	/// <summary>
	/// Knowledge Base document utilities.
	/// Contains stub methods for loading attachment and image files within KBDocuments EditView.
	/// These methods are placeholders preserved from the original codebase for Enterprise Edition extensibility.
	/// </summary>
	public class KBDocuments
	{
		public class EditView
		{
			/// <summary>
			/// Loads an attachment file for a Knowledge Base document.
			/// </summary>
			/// <param name="gID">The unique identifier of the KB document or attachment record.</param>
			/// <param name="stm">The input stream containing the binary file content.</param>
			/// <param name="trn">The database transaction context for transactional consistency.</param>
			public static void LoadAttachmentFile(Guid gID, Stream stm, IDbTransaction trn)
			{
			}

			/// <summary>
			/// Loads an image file for a Knowledge Base document.
			/// </summary>
			/// <param name="gID">The unique identifier of the KB document or image record.</param>
			/// <param name="stm">The input stream containing the binary image content.</param>
			/// <param name="trn">The database transaction context for transactional consistency.</param>
			public static void LoadImageFile(Guid gID, Stream stm, IDbTransaction trn)
			{
			}

		}
	}
}
